namespace OmniChannel.Catalog.Core.Domain.Model;

public class EntityDelta<T>
{
    public string EntityId { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string ChangeType { get; set; } = null!;
    public Dictionary<string, object?> ChangedFields { get; set; } = [];
    public T? Document { get; set; }
}