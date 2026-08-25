namespace OmniChannel.Catalog.Host.Infrastructure;

using OmniChannel.Catalog.Core.Handlers.Support;
using OmniChannel.Catalog.Host.Realtime;

public abstract class StateChangeBroadcaster<TEntity>(
    IHubContext<CatalogHub> hub,
    IResumeTokenRepository resumeTokens,
    IOptions<MongoDbSettings> settings,
    ILogger logger) : ChangeStreamProcessor<TEntity>(resumeTokens, settings, logger) where TEntity : CurrentStateEntity
{
    protected abstract string EntityTypeName { get; }
    protected abstract string MethodName { get; }
    protected override bool IncludePreImage => true;

    protected override PipelineDefinition<ChangeStreamDocument<TEntity>, ChangeStreamDocument<TEntity>> BuildPipeline() =>
        new EmptyPipelineDefinition<ChangeStreamDocument<TEntity>>()
            .Match(change =>
                change.OperationType == ChangeStreamOperationType.Insert ||
                change.OperationType == ChangeStreamOperationType.Update ||
                change.OperationType == ChangeStreamOperationType.Replace ||
                change.OperationType == ChangeStreamOperationType.Delete);

    protected override string GetEntityId(ChangeStreamDocument<TEntity> change) =>
        change.FullDocument?.EntityId
            ?? change.FullDocumentBeforeChange?.EntityId
            ?? change.DocumentKey.GetValue("_id", BsonNull.Value).ToString()!;

    protected override async Task HandleAsync(ChangeStreamDocument<TEntity> change, CancellationToken cancellationToken)
    {
        var entityId = GetEntityId(change);
        var delta = EntityDeltaFactory.Create(change, EntityTypeName, entityId);
        var message = new CurrentStateChanged
        {
            Group = CatalogHub.Group,
            MethodName = MethodName,
            EntityType = EntityTypeName,
            EntityId = entityId,
            ChangeType = delta.ChangeType,
            Payload = delta.Document
        };
        await hub.Clients.Group(CatalogHub.Group).SendAsync(MethodName, message, cancellationToken);
    }
}