namespace OmniChannel.Catalog.Core.Domain.Repositories;

using OmniChannel.Catalog.Core.Domain.Model.Entities;

public interface IProductCurrentStateRepository
{
    Task ApplyProprietaryAsync(ProductProprietaryState state, CancellationToken cancellationToken);
    Task<ProductCurrentState?> GetAsync(string entityId, CancellationToken cancellationToken);
    Task<List<ProductCurrentState>> GetAllAsync(CancellationToken cancellationToken);
}