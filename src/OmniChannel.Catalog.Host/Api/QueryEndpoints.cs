namespace OmniChannel.Catalog.Host.Api;

public static class QueryEndpoints
{
    public static void MapQueryEndpoints(this WebApplication app)
    {
        var current = app.MapGroup("/api/current");
        current.MapGet("/products", async (IProductCurrentStateRepository repo, CancellationToken ct) => Results.Ok(await repo.GetAllAsync(ct)));
        current.MapGet("/variants", async (IVariantCurrentStateRepository repo, CancellationToken ct) => Results.Ok(await repo.GetAllAsync(ct)));
        current.MapGet("/listings", async (IListingCurrentStateRepository repo, CancellationToken ct) => Results.Ok(await repo.GetAllAsync(ct)));
        current.MapGet("/", async (IProductCurrentStateRepository products, IVariantCurrentStateRepository variants, IListingCurrentStateRepository listings, CancellationToken ct) =>
            Results.Ok(new
            {
                products = await products.GetAllAsync(ct),
                variants = await variants.GetAllAsync(ct),
                listings = await listings.GetAllAsync(ct)
            }));
    }
}