namespace OmniChannel.Catalog.Data.Repositories;

using OmniChannel.Catalog.Core.Domain.Model;

public class VariantCurrentStateRepository(IMongoContext context, KeyedAsyncLock locks) : IVariantCurrentStateRepository
{
    private readonly IMongoCollection<VariantCurrentState> _variants = context.VariantsCurrent;
    private readonly IMongoCollection<ListingCurrentState> _listings = context.ListingsCurrent;

    public async Task ApplyProprietaryAsync(VariantProprietaryState state, CancellationToken cancellationToken)
    {
        using var _ = await locks.AcquireAsync(state.EntityId, cancellationToken);

        var current = await _variants.Find(x => x.EntityId == state.EntityId).FirstOrDefaultAsync(cancellationToken)
            ?? new VariantCurrentState { Id = ObjectId.GenerateNewId(), EntityId = state.EntityId, ProductId = state.ProductId, CreatedAt = state.CreatedAt };

        if (current.LastProprietaryAt.HasValue && state.CreatedAt < current.LastProprietaryAt.Value)
        {
            return;
        }

        current.ProductId = state.ProductId;
        if (state.Sku != null)
        {
            current.Sku = state.Sku;
        }

        if (state.Size != null)
        {
            current.Size = state.Size;
        }

        if (state.Color != null)
        {
            current.Color = state.Color;
        }

        if (state.Price.HasValue)
        {
            current.Price = DualStateField<decimal>.FromActive(state.Price.Value);
        }

        if (state.Stock.HasValue)
        {
            current.Stock = DualStateField<int>.FromActive(state.Stock.Value);
        }

        current.Removed = state.Removed;
        current.LastProprietaryAt = state.CreatedAt;
        current.UpdatedAt = DateTime.UtcNow;

        await _variants.ReplaceOneAsync(x => x.EntityId == state.EntityId, current, new ReplaceOptions { IsUpsert = true }, cancellationToken);

        if (state.Removed)
        {
            await _listings.UpdateManyAsync(
                l => l.VariantId == state.EntityId,
                Builders<ListingCurrentState>.Update
                    .Set(l => l.Removed, true)
                    .Set(l => l.PublishStatus, Core.Domain.Constants.PublishStatus.Withdrawn)
                    .Set(l => l.UpdatedAt, DateTime.UtcNow),
                cancellationToken: cancellationToken);
        }
    }

    public async Task<VariantCurrentState?> GetAsync(string entityId, CancellationToken cancellationToken) =>
        await _variants.Find(x => x.EntityId == entityId).FirstOrDefaultAsync(cancellationToken);

    public async Task<List<VariantCurrentState>> GetAllAsync(CancellationToken cancellationToken) =>
        await _variants.Find(FilterDefinition<VariantCurrentState>.Empty).ToListAsync(cancellationToken);
}