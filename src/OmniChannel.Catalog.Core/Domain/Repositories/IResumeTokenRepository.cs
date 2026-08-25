namespace OmniChannel.Catalog.Core.Domain.Repositories;

public interface IResumeTokenRepository
{
    Task SaveAsync(string serviceName, BsonDocument token, CancellationToken cancellationToken);
    Task<BsonDocument?> GetLatestAsync(string serviceName, CancellationToken cancellationToken);
    Task DeleteAsync(string serviceName, CancellationToken cancellationToken);
}