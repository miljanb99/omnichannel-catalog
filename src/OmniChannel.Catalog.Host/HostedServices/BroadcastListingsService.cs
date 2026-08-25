namespace OmniChannel.Catalog.Host.HostedServices;

using OmniChannel.Catalog.Host.Infrastructure;
using OmniChannel.Catalog.Host.Realtime;

public class BroadcastListingsService(
    IMongoContext context,
    IHubContext<CatalogHub> hub,
    IResumeTokenRepository resumeTokens,
    IOptions<MongoDbSettings> settings,
    ILogger<BroadcastListingsService> logger)
    : StateChangeBroadcaster<ListingCurrentState>(hub, resumeTokens, settings, logger)
{
    protected override string ServiceName => nameof(BroadcastListingsService);
    protected override string EntityTypeName => EntityType.Listing;
    protected override string MethodName => "listingUpdate";
    protected override IMongoCollection<ListingCurrentState> Collection => context.ListingsCurrent;
}