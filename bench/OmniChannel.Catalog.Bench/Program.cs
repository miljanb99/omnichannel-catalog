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
        double P(double p) => lat[Math.Min(lat.Count - 1, (int)(lat.Count * p))];
        return (P(0.5), P(0.95), P(0.99));
    }

    await context.ListingsProprietary.DeleteManyAsync(FilterDefinition<ListingProprietaryState>.Empty);
    await context.ListingsCurrent.DeleteManyAsync(FilterDefinition<ListingCurrentState>.Empty);
    await Task.Delay(500);

    var b = await Measure("base");
    Console.WriteLine($"baseline (prazna kolekcija):   median={b.med:F1} ms   p95={b.p95:F1} ms   p99={b.p99:F1} ms");

    await context.ListingsProprietary.DeleteManyAsync(FilterDefinition<ListingProprietaryState>.Empty);
    await context.ListingsCurrent.DeleteManyAsync(FilterDefinition<ListingCurrentState>.Empty);
    await Task.Delay(300);

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

    var s = await Measure("big");
    Console.WriteLine($"nad {total:N0} dokumenata:        median={s.med:F1} ms   p95={s.p95:F1} ms   p99={s.p99:F1} ms");

    await projector.StopAsync(CancellationToken.None);
    return;
}

Console.WriteLine($"{"scenario",-16}{"count",8}{"median ms",12}{"p95 ms",10}{"throughput/s",14}");
foreach (var n in scenarios)
{
    await context.ListingsProprietary.DeleteManyAsync(FilterDefinition<ListingProprietaryState>.Empty);
    await context.ListingsCurrent.DeleteManyAsync(FilterDefinition<ListingCurrentState>.Empty);
    await Task.Delay(400);

    var start = DateTime.UtcNow;
    for (var i = 0; i < n; i++)
    {
        await log.InsertAsync(new ListingProprietaryState
        {
            EntityId = $"b_{n}_{i}",
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
    var median = latencies[latencies.Count / 2];
    var p95 = latencies[(int)(latencies.Count * 0.95)];
    var throughput = n / (all.Max(x => x.UpdatedAt) - start).TotalSeconds;

    Console.WriteLine($"{n + " događaja",-16}{n,8}{median,12:F1}{p95,10:F1}{throughput,14:F0}");
}

await projector.StopAsync(CancellationToken.None);