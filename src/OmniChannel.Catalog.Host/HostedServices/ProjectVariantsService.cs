namespace OmniChannel.Catalog.Host.HostedServices;

using OmniChannel.Catalog.Host.Infrastructure;

public class ProjectVariantsService(
    IMongoContext context,
    IVariantCurrentStateRepository repository,
    IResumeTokenRepository resumeTokens,
    IOptions<MongoDbSettings> settings,
    ILogger<ProjectVariantsService> logger)
    : ChangeStreamProcessor<VariantProprietaryState>(resumeTokens, settings, logger)
{
    protected override string ServiceName => nameof(ProjectVariantsService);
    protected override IMongoCollection<VariantProprietaryState> Collection => context.VariantsProprietary;

    protected override PipelineDefinition<ChangeStreamDocument<VariantProprietaryState>, ChangeStreamDocument<VariantProprietaryState>> BuildPipeline() =>
        new EmptyPipelineDefinition<ChangeStreamDocument<VariantProprietaryState>>()
            .Match(change => change.OperationType == ChangeStreamOperationType.Insert);

    protected override string GetEntityId(ChangeStreamDocument<VariantProprietaryState> change) => change.FullDocument.EntityId;

    protected override Task HandleAsync(ChangeStreamDocument<VariantProprietaryState> change, CancellationToken cancellationToken) =>
        repository.ApplyProprietaryAsync(change.FullDocument, cancellationToken);
}