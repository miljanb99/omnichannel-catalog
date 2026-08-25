namespace OmniChannel.Catalog.Host.HostedServices;

using OmniChannel.Catalog.Host.Infrastructure;

public class ProjectListingsProprietaryService(
    IMongoContext context,
    IListingCurrentStateRepository repository,
    IResumeTokenRepository resumeTokens,
    IOptions<MongoDbSettings> settings,
    ILogger<ProjectListingsProprietaryService> logger)
    : ChangeStreamProcessor<ListingProprietaryState>(resumeTokens, settings, logger)
{
    protected override string ServiceName => nameof(ProjectListingsProprietaryService);
    protected override IMongoCollection<ListingProprietaryState> Collection => context.ListingsProprietary;

    protected override PipelineDefinition<ChangeStreamDocument<ListingProprietaryState>, ChangeStreamDocument<ListingProprietaryState>> BuildPipeline() =>
        new EmptyPipelineDefinition<ChangeStreamDocument<ListingProprietaryState>>()
            .Match(change => change.OperationType == ChangeStreamOperationType.Insert);

    protected override string GetEntityId(ChangeStreamDocument<ListingProprietaryState> change) => change.FullDocument.EntityId;

    protected override Task HandleAsync(ChangeStreamDocument<ListingProprietaryState> change, CancellationToken cancellationToken) =>
        repository.ApplyProprietaryAsync(change.FullDocument, cancellationToken);
}