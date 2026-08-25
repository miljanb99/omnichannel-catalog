namespace OmniChannel.Catalog.Core.Domain.Model.Entities;

public abstract class CurrentStateEntity
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string EntityId { get; set; } = null!;
    public bool Removed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastProprietaryAt { get; set; }
    public DateTime? LastObservedAt { get; set; }
}