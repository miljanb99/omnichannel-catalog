namespace OmniChannel.Catalog.Core.Domain.Repositories;

using OmniChannel.Catalog.Core.Domain.Model.Entities;

public interface IAppendLogRepository<T> where T : AppendLogEntity
{
    Task InsertAsync(T entity, CancellationToken cancellationToken);
    Task<List<T>> GetAllOrderedAsync(CancellationToken cancellationToken);
}