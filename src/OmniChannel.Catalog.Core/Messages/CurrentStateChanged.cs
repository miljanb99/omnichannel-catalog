namespace OmniChannel.Catalog.Core.Messages;

public class CurrentStateChanged
{
    public string Group { get; set; } = null!;
    public string MethodName { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string ChangeType { get; set; } = null!;
    public object? Payload { get; set; }
}