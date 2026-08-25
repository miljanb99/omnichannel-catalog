namespace OmniChannel.Catalog.Host.HostedServices;

using OmniChannel.Catalog.Host.Infrastructure;
using OmniChannel.Catalog.Host.Realtime;

public class BroadcastProductsService(
    IMongoContext context,
    IHubContext<CatalogHub> hub,
    IResumeTokenRepository resumeTokens,
    IOptions<MongoDbSettings> settings,
    ILogger<BroadcastProductsService> logger)
    : StateChangeBroadcaster<ProductCurrentState>(hub, resumeTokens, settings, logger)
{
    protected override string ServiceName => nameof(BroadcastProductsService);
    protected override string EntityTypeName => EntityType.Product;
    protected override string MethodName => "productUpdate";
    protected override IMongoCollection<ProductCurrentState> Collection => context.ProductsCurrent;
}