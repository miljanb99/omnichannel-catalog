namespace OmniChannel.Catalog.Data.Repositories;

using OmniChannel.Catalog.Core.Domain.Constants;

public class ListingCurrentStateRepository(IMongoContext context, KeyedAsyncLock locks) : IListingCurrentStateRepository
{
    private readonly IMongoCollection<ListingCurrentState> _listings = context.ListingsCurrent;

    public async Task ApplyProprietaryAsync(ListingProprietaryState state, CancellationToken cancellationToken)
    {
        using var _ = await locks.AcquireAsync(state.EntityId, cancellationToken);

        var current = await _listings.Find(x => x.EntityId == state.EntityId).FirstOrDefaultAsync(cancellationToken)
            ?? new ListingCurrentState
            {
                Id = ObjectId.GenerateNewId(),
                EntityId = state.EntityId,
                ProductId = state.ProductId,
                VariantId = state.VariantId,
                Channel = state.Channel,
                CreatedAt = state.CreatedAt
            };

        if (current.LastProprietaryAt.HasValue && state.CreatedAt < current.LastProprietaryAt.Value)
        {
            return;
        }

        if (state.DiscardDraft)
        {
            current.Title.DiscardDraft();
            current.Price.DiscardDraft();
            current.Available.DiscardDraft();
            current.LastProprietaryAt = state.CreatedAt;
            current.UpdatedAt = DateTime.UtcNow;
            current.PublishStatus = current.EffectiveStatus != null
                ? MapPublishStatus(current.EffectiveStatus, current)
                : PublishStatus.Draft;
            await _listings.ReplaceOneAsync(x => x.EntityId == state.EntityId, current, new ReplaceOptions { IsUpsert = true }, cancellationToken);
            return;
        }

        current.ProductId = state.ProductId;
        current.VariantId = state.VariantId;
        current.Channel = state.Channel;
        if (state.Title != null)
        {
            current.Title.SetDraft(state.Title);
        }

        if (state.Price.HasValue)
        {
            current.Price.SetDraft(state.Price.Value);
        }

        if (state.Available.HasValue)
        {
            current.Available.SetDraft(state.Available.Value);
        }

        if (state.DesiredStatus != null)
        {
            current.DesiredStatus = state.DesiredStatus;
        }

        current.Removed = state.Removed;
        current.LastProprietaryAt = state.CreatedAt;
        current.UpdatedAt = DateTime.UtcNow;
        current.PublishStatus = state.Removed || current.DesiredStatus == PublishStatus.Withdrawn
            ? PublishStatus.Withdrawn
            : HasPendingDraft(current) || current.PublishStatus == PublishStatus.Withdrawn
                ? PublishStatus.Pending
                : current.PublishStatus;

        await _listings.ReplaceOneAsync(x => x.EntityId == state.EntityId, current, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task ApplyObservedAsync(ListingObservedState state, CancellationToken cancellationToken)
    {
        using var _ = await locks.AcquireAsync(state.EntityId, cancellationToken);

        var current = await _listings.Find(x => x.EntityId == state.EntityId).FirstOrDefaultAsync(cancellationToken)
            ?? new ListingCurrentState
            {
                Id = ObjectId.GenerateNewId(),
                EntityId = state.EntityId,
                Channel = state.Channel,
                CreatedAt = state.ObservedAt
            };

        if (current.LastObservedAt.HasValue && state.ObservedAt < current.LastObservedAt.Value)
        {
            return;
        }

        current.EffectiveStatus = state.EffectiveStatus;
        current.ObservedPrice = state.ObservedPrice;
        current.ModerationNote = state.ModerationNote;
        current.LastObservedAt = state.ObservedAt;

        if (state.EffectiveStatus == ChannelStatus.Active)
        {
            if (current.Price.HasDraft && state.ObservedPrice.HasValue && current.Price.Draft == state.ObservedPrice.Value)
            {
                current.Price.Publish();
            }

            if (current.Available.HasDraft && state.Available.HasValue && current.Available.Draft == state.Available.Value)
            {
                current.Available.Publish();
            }

            if (current.Title.HasDraft)
            {
                current.Title.Publish();
            }
        }

        current.PublishStatus = MapPublishStatus(state.EffectiveStatus, current);
        current.UpdatedAt = DateTime.UtcNow;

        await _listings.ReplaceOneAsync(x => x.EntityId == state.EntityId, current, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<ListingCurrentState?> GetAsync(string entityId, CancellationToken cancellationToken) =>
        await _listings.Find(x => x.EntityId == entityId).FirstOrDefaultAsync(cancellationToken);

    public async Task<List<ListingCurrentState>> GetAllAsync(CancellationToken cancellationToken) =>
        await _listings.Find(FilterDefinition<ListingCurrentState>.Empty).ToListAsync(cancellationToken);

    private static bool HasPendingDraft(ListingCurrentState current) =>
        current.Title.HasDraft || current.Price.HasDraft || current.Available.HasDraft;

    private static string MapPublishStatus(string effectiveStatus, ListingCurrentState current) =>
        effectiveStatus switch
        {
            ChannelStatus.Active => HasPendingDraft(current) ? PublishStatus.Pending : PublishStatus.Published,
            ChannelStatus.PendingReview => PublishStatus.Pending,
            ChannelStatus.Rejected => PublishStatus.Rejected,
            ChannelStatus.Paused => PublishStatus.Withdrawn,
            _ => current.PublishStatus
        };
}