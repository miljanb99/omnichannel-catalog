namespace OmniChannel.Catalog.Core.Domain.Repositories;

using OmniChannel.Catalog.Core.Domain.Model.Entities;

public interface IVariantCurrentStateRepository
{
    Task ApplyProprietaryAsync(VariantProprietaryState state, CancellationToken cancellationToken);
    Task<VariantCurrentState?> GetAsync(string entityId, CancellationToken cancellationToken);
    Task<List<VariantCurrentState>> GetAllAsync(CancellationToken cancellationToken);
}