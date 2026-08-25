namespace OmniChannel.Catalog.Data.Repositories;

public class AppendLogRepository<T>(IMongoCollection<T> collection) : IAppendLogRepository<T> where T : AppendLogEntity
{
    public async Task InsertAsync(T entity, CancellationToken cancellationToken)
    {
        if (entity.Id == ObjectId.Empty)
        {
            entity.Id = ObjectId.GenerateNewId();
        }

        if (entity.CreatedAt == default)
        {
            entity.CreatedAt = DateTime.UtcNow;
        }

        await collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
    }

    public async Task<List<T>> GetAllOrderedAsync(CancellationToken cancellationToken) =>
        await collection
            .Find(FilterDefinition<T>.Empty)
            .Sort(Builders<T>.Sort.Ascending(e => e.CreatedAt).Ascending(e => e.Id))
            .ToListAsync(cancellationToken);
}