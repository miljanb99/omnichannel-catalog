using OmniChannel.Catalog.Core.Configuration;
using OmniChannel.Catalog.Data;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDb"));
builder.Services.AddCatalogData();

using var host = builder.Build();
var replayer = host.Services.GetRequiredService<CatalogReplayer>();

Console.WriteLine("Replay: rekonstrukcija tekućeg stanja iz append log-a…");
var result = await replayer.ReplayAsync(CancellationToken.None);
Console.WriteLine($"Gotovo. products={result.Products} variants={result.Variants} listingsProprietary={result.ListingsProprietary} listingsObserved={result.ListingsObserved}");