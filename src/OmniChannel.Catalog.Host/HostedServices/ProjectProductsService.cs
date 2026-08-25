    namespace OmniChannel.Catalog.Host.HostedServices;

using OmniChannel.Catalog.Host.Infrastructure;

public class ProjectProductsService(
    IMongoContext context,
    IProductCurrentStateRepository repository,
    IResumeTokenRepository resumeTokens,
    IOptions<MongoDbSettings> settings,
    ILogger<ProjectProductsService> logger)
    : ChangeStreamProcessor<ProductProprietaryState>(resumeTokens, settings, logger)
{
    protected override string ServiceName => nameof(ProjectProductsService);
    protected override IMongoCollection<ProductProprietaryState> Collection => context.ProductsProprietary;

    protected override PipelineDefinition<ChangeStreamDocument<ProductProprietaryState>, ChangeStreamDocument<ProductProprietaryState>> BuildPipeline() =>
        new EmptyPipelineDefinition<ChangeStreamDocument<ProductProprietaryState>>()
            .Match(change => change.OperationType == ChangeStreamOperationType.Insert);

    protected override string GetEntityId(ChangeStreamDocument<ProductProprietaryState> change) => change.FullDocument.EntityId;

    protected override Task HandleAsync(ChangeStreamDocument<ProductProprietaryState> change, CancellationToken cancellationToken) =>
        repository.ApplyProprietaryAsync(change.FullDocument, cancellationToken);
}