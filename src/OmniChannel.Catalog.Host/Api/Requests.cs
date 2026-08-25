namespace OmniChannel.Catalog.Host.Api;

public record ProductRequest(string? Title, string? Description, string? Category, decimal? BasePrice);

public record CreateVariantRequest(string ProductId, string? Sku, string? Size, string? Color, decimal? Price, int? Stock);

public record UpdateVariantRequest(string? Sku, string? Size, string? Color, decimal? Price, int? Stock);

public record CreateListingRequest(string ProductId, string VariantId, string Channel, string? Title, decimal? Price, bool? Available, string? DesiredStatus);

public record UpdateListingRequest(string? Title, decimal? Price, bool? Available, string? DesiredStatus);

public record RejectListingRequest(string? Note);

public record SimulatorToggleRequest(bool Enabled);

public record ObserveListingRequest(string EffectiveStatus, decimal? ObservedPrice, bool? Available, string? ModerationNote);