namespace OmniChannel.Catalog.Host.Infrastructure;

using OmniChannel.Catalog.Data;

public class MongoIndexInitializer(
    IMongoClient client,
    IMongoContext context,
    IOptions<MongoDbSettings> settings,
    ILogger<MongoIndexInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await CatalogInitializer.InitializeAsync(client, context, settings.Value, cancellationToken);
        logger.LogInformation("Mongo indexes and pre-images ensured");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}