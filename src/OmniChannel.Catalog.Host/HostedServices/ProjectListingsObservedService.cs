namespace OmniChannel.Catalog.Host.HostedServices;

using OmniChannel.Catalog.Host.Infrastructure;

public class ProjectListingsObservedService(
    IMongoContext context,
    IListingCurrentStateRepository repository,
    IResumeTokenRepository resumeTokens,
    IOptions<MongoDbSettings> settings,
    ILogger<ProjectListingsObservedService> logger)
    : ChangeStreamProcessor<ListingObservedState>(resumeTokens, settings, logger)
{
    protected override string ServiceName => nameof(ProjectListingsObservedService);
    protected override IMongoCollection<ListingObservedState> Collection => context.ListingsObserved;

    protected override PipelineDefinition<ChangeStreamDocument<ListingObservedState>, ChangeStreamDocument<ListingObservedState>> BuildPipeline() =>
        new EmptyPipelineDefinition<ChangeStreamDocument<ListingObservedState>>()
            .Match(change => change.OperationType == ChangeStreamOperationType.Insert);

    protected override string GetEntityId(ChangeStreamDocument<ListingObservedState> change) => change.FullDocument.EntityId;

    protected override Task HandleAsync(ChangeStreamDocument<ListingObservedState> change, CancellationToken cancellationToken) =>
        repository.ApplyObservedAsync(change.FullDocument, cancellationToken);
}