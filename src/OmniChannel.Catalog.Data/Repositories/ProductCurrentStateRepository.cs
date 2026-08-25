namespace OmniChannel.Catalog.Data.Repositories;

using OmniChannel.Catalog.Core.Domain.Model;

public class ProductCurrentStateRepository(IMongoContext context, KeyedAsyncLock locks) : IProductCurrentStateRepository
{
    private readonly IMongoCollection<ProductCurrentState> _products = context.ProductsCurrent;
    private readonly IMongoCollection<VariantCurrentState> _variants = context.VariantsCurrent;
    private readonly IMongoCollection<ListingCurrentState> _listings = context.ListingsCurrent;

    public async Task ApplyProprietaryAsync(ProductProprietaryState state, CancellationToken cancellationToken)
    {
        using var _ = await locks.AcquireAsync(state.EntityId, cancellationToken);

        var current = await _products.Find(x => x.EntityId == state.EntityId).FirstOrDefaultAsync(cancellationToken)
            ?? new ProductCurrentState { Id = ObjectId.GenerateNewId(), EntityId = state.EntityId, CreatedAt = state.CreatedAt };

        if (current.LastProprietaryAt.HasValue && state.CreatedAt < current.LastProprietaryAt.Value)
        {
            return;
        }

        if (state.Title != null)
        {
            current.Title = DualStateField<string>.FromActive(state.Title);
        }

        if (state.Description != null)
        {
            current.Description = DualStateField<string>.FromActive(state.Description);
        }

        if (state.BasePrice.HasValue)
        {
            current.BasePrice = DualStateField<decimal>.FromActive(state.BasePrice.Value);
        }

        if (state.Category != null)
        {
            current.Category = state.Category;
        }

        current.Removed = state.Removed;
        current.LastProprietaryAt = state.CreatedAt;
        current.UpdatedAt = DateTime.UtcNow;

        await _products.ReplaceOneAsync(x => x.EntityId == state.EntityId, current, new ReplaceOptions { IsUpsert = true }, cancellationToken);

        if (state.Title != null)
        {
            await _variants.UpdateManyAsync(
                v => v.ProductId == state.EntityId,
                Builders<VariantCurrentState>.Update.Set(v => v.ProductTitle, state.Title),
                cancellationToken: cancellationToken);
        }

        if (state.Removed)
        {
            await CascadeRemoveAsync(state.EntityId, cancellationToken);
        }
    }

    public async Task<ProductCurrentState?> GetAsync(string entityId, CancellationToken cancellationToken) =>
        await _products.Find(x => x.EntityId == entityId).FirstOrDefaultAsync(cancellationToken);

    public async Task<List<ProductCurrentState>> GetAllAsync(CancellationToken cancellationToken) =>
        await _products.Find(FilterDefinition<ProductCurrentState>.Empty).ToListAsync(cancellationToken);

    private async Task CascadeRemoveAsync(string productId, CancellationToken cancellationToken)
    {
        await _variants.UpdateManyAsync(
            v => v.ProductId == productId,
            Builders<VariantCurrentState>.Update.Set(v => v.Removed, true).Set(v => v.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);
        await _listings.UpdateManyAsync(
            l => l.ProductId == productId,
            Builders<ListingCurrentState>.Update
                .Set(l => l.Removed, true)
                .Set(l => l.PublishStatus, Core.Domain.Constants.PublishStatus.Withdrawn)
                .Set(l => l.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);
    }
}