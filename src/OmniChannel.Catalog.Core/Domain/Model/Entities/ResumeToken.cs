namespace OmniChannel.Catalog.Core.Domain.Model.Entities;

public class ResumeToken
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string ServiceName { get; set; } = null!;
    public BsonDocument Token { get; set; } = null!;
    public DateTime Timestamp { get; set; }
}