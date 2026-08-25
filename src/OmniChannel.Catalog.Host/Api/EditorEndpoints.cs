namespace OmniChannel.Catalog.Host.Api;

public static class EditorEndpoints
{
    public static void MapEditorEndpoints(this WebApplication app)
    {
        var products = app.MapGroup("/api/products");
        products.MapPost("/", async (ProductRequest request, IAppendLogRepository<ProductProprietaryState> log, CancellationToken ct) =>
        {
            var id = NewId("pr");
            await log.InsertAsync(new ProductProprietaryState
            {
                EntityId = id,
                Title = request.Title,
                Description = request.Description,
                Category = request.Category,
                BasePrice = request.BasePrice
            }, ct);
            return Results.Ok(new { id });
        });
        products.MapPut("/{id}", async (string id, ProductRequest request, IAppendLogRepository<ProductProprietaryState> log, CancellationToken ct) =>
        {
            await log.InsertAsync(new ProductProprietaryState
            {
                EntityId = id,
                Title = request.Title,
                Description = request.Description,
                Category = request.Category,
                BasePrice = request.BasePrice
            }, ct);
            return Results.Accepted();
        });
        products.MapDelete("/{id}", async (string id, IAppendLogRepository<ProductProprietaryState> log, CancellationToken ct) =>
        {
            await log.InsertAsync(new ProductProprietaryState { EntityId = id, Removed = true }, ct);
            return Results.Accepted();
        });

        var variants = app.MapGroup("/api/variants");
        variants.MapPost("/", async (CreateVariantRequest request, IAppendLogRepository<VariantProprietaryState> log, CancellationToken ct) =>
        {
            var id = NewId("var");
            await log.InsertAsync(new VariantProprietaryState
            {
                EntityId = id,
                ProductId = request.ProductId,
                Sku = request.Sku,
                Size = request.Size,
                Color = request.Color,
                Price = request.Price,
                Stock = request.Stock
            }, ct);
            return Results.Ok(new { id });
        });
        variants.MapPut("/{id}", async (string id, UpdateVariantRequest request, IAppendLogRepository<VariantProprietaryState> log, IVariantCurrentStateRepository current, CancellationToken ct) =>
        {
            var existing = await current.GetAsync(id, ct);
            await log.InsertAsync(new VariantProprietaryState
            {
                EntityId = id,
                ProductId = existing?.ProductId ?? string.Empty,
                Sku = request.Sku,
                Size = request.Size,
                Color = request.Color,
                Price = request.Price,
                Stock = request.Stock
            }, ct);
            return Results.Accepted();
        });
        variants.MapDelete("/{id}", async (string id, IAppendLogRepository<VariantProprietaryState> log, IVariantCurrentStateRepository current, CancellationToken ct) =>
        {
            var existing = await current.GetAsync(id, ct);
            await log.InsertAsync(new VariantProprietaryState { EntityId = id, ProductId = existing?.ProductId ?? string.Empty, Removed = true }, ct);
            return Results.Accepted();
        });

        var listings = app.MapGroup("/api/listings");
        listings.MapPost("/", async (CreateListingRequest request, IAppendLogRepository<ListingProprietaryState> log, CancellationToken ct) =>
        {
            var id = NewId("lst");
            await log.InsertAsync(new ListingProprietaryState
            {
                EntityId = id,
                ProductId = request.ProductId,
                VariantId = request.VariantId,
                Channel = request.Channel,
                Title = request.Title,
                Price = request.Price,
                Available = request.Available,
                DesiredStatus = request.DesiredStatus ?? PublishStatus.Published
            }, ct);
            return Results.Ok(new { id });
        });
        listings.MapPut("/{id}", async (string id, UpdateListingRequest request, IAppendLogRepository<ListingProprietaryState> log, IListingCurrentStateRepository current, CancellationToken ct) =>
        {
            var existing = await current.GetAsync(id, ct);
            await log.InsertAsync(new ListingProprietaryState
            {
                EntityId = id,
                ProductId = existing?.ProductId ?? string.Empty,
                VariantId = existing?.VariantId ?? string.Empty,
                Channel = existing?.Channel ?? string.Empty,
                Title = request.Title,
                Price = request.Price,
                Available = request.Available,
                DesiredStatus = request.DesiredStatus
            }, ct);
            return Results.Accepted();
        });
        listings.MapPost("/{id}/discard-draft", async (string id, IAppendLogRepository<ListingProprietaryState> log, IListingCurrentStateRepository current, CancellationToken ct) =>
        {
            var existing = await current.GetAsync(id, ct);
            await log.InsertAsync(new ListingProprietaryState
            {
                EntityId = id,
                ProductId = existing?.ProductId ?? string.Empty,
                VariantId = existing?.VariantId ?? string.Empty,
                Channel = existing?.Channel ?? string.Empty,
                DiscardDraft = true
            }, ct);
            return Results.Accepted();
        });
        listings.MapDelete("/{id}", async (string id, IAppendLogRepository<ListingProprietaryState> log, IListingCurrentStateRepository current, CancellationToken ct) =>
        {
            var existing = await current.GetAsync(id, ct);
            await log.InsertAsync(new ListingProprietaryState
            {
                EntityId = id,
                ProductId = existing?.ProductId ?? string.Empty,
                VariantId = existing?.VariantId ?? string.Empty,
                Channel = existing?.Channel ?? string.Empty,
                Removed = true
            }, ct);
            return Results.Accepted();
        });

        var channel = app.MapGroup("/api/channel");
        channel.MapPost("/{listingId}/reject", async (string listingId, RejectListingRequest request, IAppendLogRepository<ListingObservedState> log, IListingCurrentStateRepository current, CancellationToken ct) =>
        {
            var existing = await current.GetAsync(listingId, ct);
            await log.InsertAsync(new ListingObservedState
            {
                EntityId = listingId,
                Channel = existing?.Channel ?? SalesChannel.MarketplaceB,
                EffectiveStatus = ChannelStatus.Rejected,
                ModerationNote = request.Note ?? "Rejected by channel policy",
                ObservedAt = DateTime.UtcNow
            }, ct);
            return Results.Accepted();
        });
        channel.MapPost("/{listingId}/observe", async (string listingId, ObserveListingRequest request, IAppendLogRepository<ListingObservedState> log, IListingCurrentStateRepository current, CancellationToken ct) =>
        {
            var existing = await current.GetAsync(listingId, ct);
            await log.InsertAsync(new ListingObservedState
            {
                EntityId = listingId,
                Channel = existing?.Channel ?? SalesChannel.Webshop,
                EffectiveStatus = request.EffectiveStatus,
                ObservedPrice = request.ObservedPrice,
                Available = request.Available,
                ModerationNote = request.ModerationNote,
                ObservedAt = DateTime.UtcNow
            }, ct);
            return Results.Accepted();
        });
    }

    private static string NewId(string prefix) => $"{prefix}_{ObjectId.GenerateNewId()}";
}