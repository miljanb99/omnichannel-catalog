namespace OmniChannel.Catalog.Host.HostedServices;

using OmniChannel.Catalog.Host.Infrastructure;
using OmniChannel.Catalog.Host.Realtime;

public class BroadcastVariantsService(
    IMongoContext context,
    IHubContext<CatalogHub> hub,
    IResumeTokenRepository resumeTokens,
    IOptions<MongoDbSettings> settings,
    ILogger<BroadcastVariantsService> logger)
    : StateChangeBroadcaster<VariantCurrentState>(hub, resumeTokens, settings, logger)
{
    protected override string ServiceName => nameof(BroadcastVariantsService);
    protected override string EntityTypeName => EntityType.Variant;
    protected override string MethodName => "variantUpdate";
    protected override IMongoCollection<VariantCurrentState> Collection => context.VariantsCurrent;
}