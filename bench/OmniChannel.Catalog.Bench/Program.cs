using OmniChannel.Catalog.Core.Configuration;
using OmniChannel.Catalog.Core.Domain;
using OmniChannel.Catalog.Core.Domain.Constants;
using OmniChannel.Catalog.Core.Domain.Model;
using OmniChannel.Catalog.Core.Domain.Model.Entities;
using OmniChannel.Catalog.Core.Domain.Repositories;
using OmniChannel.Catalog.Data;
using OmniChannel.Catalog.Host.HostedServices;

var connection = args.Length > 0 && args[0] != "scale" ? args[0] : "mongodb://localhost:27018/?replicaSet=rs0&directConnection=true";
var scenarios = new[] { 100, 1000, 5000, 10000, 20000 };
const int warmupEvents = 500;
const int repetitions = 3;

var services = new ServiceCollection();
services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
services.Configure<MongoDbSettings>(options =>
{
    options.ConnectionString = connection;
    options.DatabaseName = "benchCatalog";
    options.ParallelWorkers = 8;
    options.ResumeTokenSaveInterval = 500;
    options.MaxAwaitTimeMs = 100;
    options.BatchSize = 1000;
    options.ChannelCapacity = 5000;
});
services.AddCatalogData();

await using var provider = services.BuildServiceProvider();
var context = provider.GetRequiredService<IMongoContext>();
var settings = provider.GetRequiredService<IOptions<MongoDbSettings>>().Value;
await CatalogInitializer.InitializeAsync(provider.GetRequiredService<IMongoClient>(), context, settings, CancellationToken.None);

var log = provider.GetRequiredService<IAppendLogRepository<ListingProprietaryState>>();
var listings = provider.GetRequiredService<IListingCurrentStateRepository>();

var projector = new ProjectListingsProprietaryService(
    context,
    listings,
    provider.GetRequiredService<IResumeTokenRepository>(),
    provider.GetRequiredService<IOptions<MongoDbSettings>>(),
    provider.GetRequiredService<ILogger<ProjectListingsProprietaryService>>());

await projector.StartAsync(CancellationToken.None);
await Task.Delay(1500);

async Task ClearAsync(int settleMs)
{
    await context.ListingsProprietary.DeleteManyAsync(FilterDefinition<ListingProprietaryState>.Empty);
    await context.ListingsCurrent.DeleteManyAsync(FilterDefinition<ListingCurrentState>.Empty);
    await Task.Delay(settleMs);
}

static double Percentile(List<double> sorted, double p) =>
    sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * p))];

static double MedianOf(List<double> values)
{
    var sorted = values.OrderBy(x => x).ToList();
    return sorted[sorted.Count / 2];
}

if (args.Contains("scale"))
{
    var si = Array.IndexOf(args, "scale");
    var prefill = args.Length > si + 1 ? int.Parse(args[si + 1]) : 10_000_000;
    var batch = args.Length > si + 2 ? int.Parse(args[si + 2]) : 2000;
    var client = provider.GetRequiredService<IMongoClient>();
    var db = client.GetDatabase(settings.DatabaseName);

    async Task<(double med, double p95, double p99)> Measure(string tag)
    {
        var ids = Enumerable.Range(0, batch).Select(i => $"hot_{tag}_{i}").ToList();
        var idSet = ids.ToHashSet();
        foreach (var id in ids)
        {
            await log.InsertAsync(new ListingProprietaryState
            {
                EntityId = id, ProductId = "p", VariantId = "v", Channel = SalesChannel.Webshop,
                Title = "hot", Price = 10m, Available = true, DesiredStatus = PublishStatus.Published
            }, CancellationToken.None);
        }

        var f = Builders<ListingCurrentState>.Filter.In(x => x.EntityId, idSet);
        while (await context.ListingsCurrent.CountDocumentsAsync(f) < batch)
        {
            await Task.Delay(50);
        }

        var lat = (await context.ListingsCurrent.Find(f).ToListAsync())
            .Where(d => d.LastProprietaryAt.HasValue)
            .Select(d => (d.UpdatedAt - d.LastProprietaryAt!.Value).TotalMilliseconds)
            .OrderBy(x => x).ToList();
        return (Percentile(lat, 0.5), Percentile(lat, 0.95), Percentile(lat, 0.99));
    }

    async Task<(double med, double p95, double p99)> MeasureRepeated(string tag, bool clearBetween)
    {
        List<double> med = [], p95 = [], p99 = [];
        for (var r = 0; r < repetitions; r++)
        {
            if (clearBetween)
            {
                await ClearAsync(300);
            }

            var m = await Measure($"{tag}{r}");
            med.Add(m.med);
            p95.Add(m.p95);
            p99.Add(m.p99);
            Console.WriteLine($"    {tag} iteracija {r + 1}/{repetitions}: median={m.med:F1} p95={m.p95:F1} p99={m.p99:F1}");
        }

        return (MedianOf(med), MedianOf(p95), MedianOf(p99));
    }

    await ClearAsync(500);
    Console.WriteLine($"zagrevanje ({batch} događaja, rezultat se odbacuje) ...");
    await Measure("warm");

    var b = await MeasureRepeated("base", clearBetween: true);
    Console.WriteLine($"baseline (prazna kolekcija):   median={b.med:F1} ms   p95={b.p95:F1} ms   p99={b.p99:F1} ms");

    await ClearAsync(300);

    Console.WriteLine($"punjenje {prefill:N0} dokumenata u listingsCurrentState ...");
    const int chunk = 20000;
    var past = DateTime.UtcNow.AddHours(-1);
    var swp = System.Diagnostics.Stopwatch.StartNew();
    for (var start = 0; start < prefill; start += chunk)
    {
        var cnt = Math.Min(chunk, prefill - start);
        var buf = new List<ListingCurrentState>(cnt);
        for (var i = 0; i < cnt; i++)
        {
            buf.Add(new ListingCurrentState
            {
                Id = ObjectId.GenerateNewId(), EntityId = $"pre_{start + i}",
                ProductId = "p", VariantId = "v", Channel = SalesChannel.Webshop,
                Title = DualStateField<string>.FromActive("x"),
                Price = DualStateField<decimal>.FromActive(10m),
                Available = DualStateField<bool>.FromActive(true),
                DesiredStatus = PublishStatus.Published, PublishStatus = PublishStatus.Published,
                CreatedAt = past, UpdatedAt = past, LastProprietaryAt = past
            });
        }

        await context.ListingsCurrent.InsertManyAsync(buf, new InsertManyOptions { IsOrdered = false });
        if ((start / chunk) % 25 == 0)
        {
            Console.WriteLine($"  {start + cnt:N0} / {prefill:N0}   ({swp.Elapsed.TotalSeconds:F0}s)");
        }
    }

    var total = await context.ListingsCurrent.CountDocumentsAsync(FilterDefinition<ListingCurrentState>.Empty);
    var stats = await db.RunCommandAsync<BsonDocument>(new BsonDocument { { "collStats", settings.ListingsCurrentStateCollectionName } });
    Console.WriteLine($"napunjeno: {total:N0} dokumenata, ~{stats["size"].ToInt64() / 1024 / 1024:N0} MB, za {swp.Elapsed.TotalSeconds:F0}s");

    var s = await MeasureRepeated("big", clearBetween: false);
    Console.WriteLine($"nad {total:N0} dokumenata:        median={s.med:F1} ms   p95={s.p95:F1} ms   p99={s.p99:F1} ms");

    Console.WriteLine("pražnjenje kolekcije radi kontrolnog merenja ...");
    var swd = System.Diagnostics.Stopwatch.StartNew();
    await ClearAsync(1000);
    Console.WriteLine($"  obrisano za {swd.Elapsed.TotalSeconds:F0}s");

    var b2 = await MeasureRepeated("base2", clearBetween: true);
    Console.WriteLine($"kontrola (prazna, posle):      median={b2.med:F1} ms   p95={b2.p95:F1} ms   p99={b2.p99:F1} ms");

    await projector.StopAsync(CancellationToken.None);
    return;
}

async Task<(double Median, double P95, double P99, double Throughput)> RunScenario(int n, string tag)
{
    await ClearAsync(400);

    var start = DateTime.UtcNow;
    for (var i = 0; i < n; i++)
    {
        await log.InsertAsync(new ListingProprietaryState
        {
            EntityId = $"{tag}_{i}",
            ProductId = "p",
            VariantId = "v",
            Channel = SalesChannel.Webshop,
            Title = "bench",
            Price = 10m,
            Available = true,
            DesiredStatus = PublishStatus.Published
        }, CancellationToken.None);
    }

    while (await context.ListingsCurrent.CountDocumentsAsync(FilterDefinition<ListingCurrentState>.Empty) < n)
    {
        await Task.Delay(50);
    }

    var all = await listings.GetAllAsync(CancellationToken.None);
    var latencies = all
        .Where(x => x.LastProprietaryAt.HasValue)
        .Select(x => (x.UpdatedAt - x.LastProprietaryAt!.Value).TotalMilliseconds)
        .OrderBy(x => x)
        .ToList();

    return (Percentile(latencies, 0.5), Percentile(latencies, 0.95), Percentile(latencies, 0.99),
        n / (all.Max(x => x.UpdatedAt) - start).TotalSeconds);
}

Console.WriteLine($"zagrevanje ({warmupEvents} događaja, rezultat se odbacuje) ...");
await RunScenario(warmupEvents, "warm");

Console.WriteLine($"{"scenario",-16}{"count",8}{"median ms",12}{"p95 ms",10}{"p99 ms",10}{"throughput/s",14}");
foreach (var n in scenarios)
{
    List<double> med = [], p95 = [], p99 = [], thr = [];
    for (var r = 0; r < repetitions; r++)
    {
        var x = await RunScenario(n, $"b_{n}_{r}");
        med.Add(x.Median);
        p95.Add(x.P95);
        p99.Add(x.P99);
        thr.Add(x.Throughput);
    }

    Console.WriteLine($"{n + " događaja",-16}{n,8}{MedianOf(med),12:F1}{MedianOf(p95),10:F1}{MedianOf(p99),10:F1}{MedianOf(thr),14:F0}");
}

await projector.StopAsync(CancellationToken.None);