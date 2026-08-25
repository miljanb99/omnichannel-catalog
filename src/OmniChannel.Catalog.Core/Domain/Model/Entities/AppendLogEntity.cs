namespace OmniChannel.Catalog.Core.Domain.Model.Entities;

public abstract class AppendLogEntity
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string EntityId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}