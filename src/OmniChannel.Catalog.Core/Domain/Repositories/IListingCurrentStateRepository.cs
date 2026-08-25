namespace OmniChannel.Catalog.Core.Domain.Repositories;

using OmniChannel.Catalog.Core.Domain.Model.Entities;

public interface IListingCurrentStateRepository
{
    Task ApplyProprietaryAsync(ListingProprietaryState state, CancellationToken cancellationToken);
    Task ApplyObservedAsync(ListingObservedState state, CancellationToken cancellationToken);
    Task<ListingCurrentState?> GetAsync(string entityId, CancellationToken cancellationToken);
    Task<List<ListingCurrentState>> GetAllAsync(CancellationToken cancellationToken);
}