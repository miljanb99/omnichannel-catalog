namespace OmniChannel.Catalog.Data;

public record ReplayResult(int Products, int Variants, int ListingsProprietary, int ListingsObserved);

public class CatalogReplayer(
    IMongoContext context,
    IProductCurrentStateRepository products,
    IVariantCurrentStateRepository variants,
    IListingCurrentStateRepository listings,
    IAppendLogRepository<ProductProprietaryState> productLog,
    IAppendLogRepository<VariantProprietaryState> variantLog,
    IAppendLogRepository<ListingProprietaryState> listingProprietaryLog,
    IAppendLogRepository<ListingObservedState> listingObservedLog)
{
    public async Task<ReplayResult> ReplayAsync(CancellationToken cancellationToken)
    {
        await context.ProductsCurrent.DeleteManyAsync(FilterDefinition<ProductCurrentState>.Empty, cancellationToken);
        await context.VariantsCurrent.DeleteManyAsync(FilterDefinition<VariantCurrentState>.Empty, cancellationToken);
        await context.ListingsCurrent.DeleteManyAsync(FilterDefinition<ListingCurrentState>.Empty, cancellationToken);

        var productEvents = await productLog.GetAllOrderedAsync(cancellationToken);
        var variantEvents = await variantLog.GetAllOrderedAsync(cancellationToken);
        var listingProprietaryEvents = await listingProprietaryLog.GetAllOrderedAsync(cancellationToken);
        var listingObservedEvents = await listingObservedLog.GetAllOrderedAsync(cancellationToken);

        var timeline = new List<(DateTime At, Func<Task> Apply)>();
        timeline.AddRange(productEvents.Select(e => (e.CreatedAt, (Func<Task>)(() => products.ApplyProprietaryAsync(e, cancellationToken)))));
        timeline.AddRange(variantEvents.Select(e => (e.CreatedAt, (Func<Task>)(() => variants.ApplyProprietaryAsync(e, cancellationToken)))));
        timeline.AddRange(listingProprietaryEvents.Select(e => (e.CreatedAt, (Func<Task>)(() => listings.ApplyProprietaryAsync(e, cancellationToken)))));
        timeline.AddRange(listingObservedEvents.Select(e => (e.CreatedAt, (Func<Task>)(() => listings.ApplyObservedAsync(e, cancellationToken)))));

        foreach (var (At, Apply) in timeline.OrderBy(t => t.At))
        {
            await Apply();
        }

        return new ReplayResult(productEvents.Count, variantEvents.Count, listingProprietaryEvents.Count, listingObservedEvents.Count);
    }
}