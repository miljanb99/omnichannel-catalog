namespace OmniChannel.Catalog.Data.Repositories;

public class ResumeTokenRepository(IMongoContext context) : IResumeTokenRepository
{
    private readonly IMongoCollection<ResumeToken> _collection = context.ResumeTokens;

    public async Task SaveAsync(string serviceName, BsonDocument token, CancellationToken cancellationToken)
    {
        var filter = Builders<ResumeToken>.Filter.Eq(t => t.ServiceName, serviceName);
        var update = Builders<ResumeToken>.Update
            .Set(t => t.ServiceName, serviceName)
            .Set(t => t.Token, token)
            .Set(t => t.Timestamp, DateTime.UtcNow)
            .SetOnInsert(t => t.Id, ObjectId.GenerateNewId());
        await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<BsonDocument?> GetLatestAsync(string serviceName, CancellationToken cancellationToken)
    {
        var token = await _collection
            .Find(t => t.ServiceName == serviceName)
            .SortByDescending(t => t.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);
        return token?.Token;
    }

    public async Task DeleteAsync(string serviceName, CancellationToken cancellationToken) =>
        await _collection.DeleteManyAsync(t => t.ServiceName == serviceName, cancellationToken);
}