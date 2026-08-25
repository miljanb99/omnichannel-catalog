namespace OmniChannel.Catalog.Core.Handlers.Support;

using OmniChannel.Catalog.Core.Domain.Constants;
using OmniChannel.Catalog.Core.Domain.Model;

public static class EntityDeltaFactory
{
    public static EntityDelta<T> Create<T>(ChangeStreamDocument<T> change, string entityType, string entityId) where T : class
    {
        var changeType = change.OperationType switch
        {
            ChangeStreamOperationType.Insert => ChangeType.Created,
            ChangeStreamOperationType.Update or ChangeStreamOperationType.Replace => ChangeType.Updated,
            ChangeStreamOperationType.Delete => ChangeType.Deleted,
            _ => ChangeType.Updated
        };

        var changedFields = new Dictionary<string, object?>();

        if (changeType != ChangeType.Deleted)
        {
            if (change.OperationType == ChangeStreamOperationType.Update && change.UpdateDescription?.UpdatedFields != null)
            {
                foreach (var element in change.UpdateDescription.UpdatedFields)
                {
                    changedFields[element.Name] = BsonTypeMapper.MapToDotNetValue(element.Value);
                }
            }
            else if (change.FullDocument != null)
            {
                var document = change.FullDocument.ToBsonDocument();
                foreach (var element in document)
                {
                    changedFields[element.Name] = BsonTypeMapper.MapToDotNetValue(element.Value);
                }
            }
        }

        return new EntityDelta<T>
        {
            EntityId = entityId,
            EntityType = entityType,
            ChangeType = changeType,
            ChangedFields = changedFields,
            Document = change.FullDocument ?? change.FullDocumentBeforeChange
        };
    }
}