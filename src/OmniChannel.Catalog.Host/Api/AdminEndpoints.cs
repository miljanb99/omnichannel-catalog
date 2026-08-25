namespace OmniChannel.Catalog.Host.Api;

using OmniChannel.Catalog.Data;
using OmniChannel.Catalog.Host.Infrastructure;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin");

        admin.MapPost("/replay", async (CatalogReplayer replayer, CancellationToken ct) =>
            Results.Ok(await replayer.ReplayAsync(ct)));

        admin.MapPost("/drop-resume-tokens", async (IMongoContext context, CancellationToken ct) =>
        {
            var deleted = await context.ResumeTokens.DeleteManyAsync(FilterDefinition<ResumeToken>.Empty, ct);
            return Results.Ok(new { deleted = deleted.DeletedCount });
        });

        admin.MapPost("/reset", async (IMongoContext context, CancellationToken ct) =>
        {
            await context.ProductsProprietary.DeleteManyAsync(FilterDefinition<ProductProprietaryState>.Empty, ct);
            await context.VariantsProprietary.DeleteManyAsync(FilterDefinition<VariantProprietaryState>.Empty, ct);
            await context.ListingsProprietary.DeleteManyAsync(FilterDefinition<ListingProprietaryState>.Empty, ct);
            await context.ListingsObserved.DeleteManyAsync(FilterDefinition<ListingObservedState>.Empty, ct);
            await context.ProductsCurrent.DeleteManyAsync(FilterDefinition<ProductCurrentState>.Empty, ct);
            await context.VariantsCurrent.DeleteManyAsync(FilterDefinition<VariantCurrentState>.Empty, ct);
            await context.ListingsCurrent.DeleteManyAsync(FilterDefinition<ListingCurrentState>.Empty, ct);
            await context.ResumeTokens.DeleteManyAsync(FilterDefinition<ResumeToken>.Empty, ct);
            return Results.Ok(new { reset = true });
        });

        admin.MapGet("/datasets", (IWebHostEnvironment env) => Results.Ok(CatalogDataset.List(env)));

        admin.MapGet("/simulator", (SimulatorSwitch state) => Results.Ok(new { enabled = state.Enabled }));

        admin.MapPost("/simulator", (SimulatorToggleRequest request, SimulatorSwitch state) =>
        {
            state.Set(request.Enabled);
            return Results.Ok(new { enabled = state.Enabled });
        });

        admin.MapPost("/seed", async (
            string? dataset,
            IWebHostEnvironment env,
            IAppendLogRepository<ProductProprietaryState> productLog,
            IAppendLogRepository<VariantProprietaryState> variantLog,
            IAppendLogRepository<ListingProprietaryState> listingLog,
            CancellationToken ct) =>
        {
            var rows = CatalogDataset.Load(env, dataset);
            if (rows.Count == 0)
            {
                return Results.BadRequest(new { error = "Nema dostupnog dataset-a (proveri folder catalogs/)" });
            }

            var created = new List<object>();
            foreach (var group in rows.GroupBy(r => r.Product))
            {
                var head = group.First();
                var productId = $"pr_{ObjectId.GenerateNewId()}";
                await productLog.InsertAsync(new ProductProprietaryState
                {
                    EntityId = productId,
                    Title = head.Product,
                    Description = head.Description,
                    Category = head.Category,
                    BasePrice = group.Min(r => r.Price)
                }, ct);

                foreach (var row in group)
                {
                    var variantId = $"var_{ObjectId.GenerateNewId()}";
                    await variantLog.InsertAsync(new VariantProprietaryState
                    {
                        EntityId = variantId,
                        ProductId = productId,
                        Sku = $"{row.Product.Replace(' ', '-')}-{row.Size}-{row.Color}",
                        Size = row.Size,
                        Color = row.Color,
                        Price = row.Price,
                        Stock = row.Stock
                    }, ct);

                    foreach (var channel in row.Channels)
                    {
                        var listingId = $"lst_{ObjectId.GenerateNewId()}";
                        await listingLog.InsertAsync(new ListingProprietaryState
                        {
                            EntityId = listingId,
                            ProductId = productId,
                            VariantId = variantId,
                            Channel = channel,
                            Title = $"{row.Product} {row.Size}/{row.Color}",
                            Price = row.Price,
                            Available = row.Stock > 0,
                            DesiredStatus = PublishStatus.Published
                        }, ct);
                        created.Add(new { listingId, channel });
                    }
                }
            }

            return Results.Ok(new { dataset = dataset ?? CatalogDataset.List(env).FirstOrDefault(), count = created.Count });
        });
    }
}