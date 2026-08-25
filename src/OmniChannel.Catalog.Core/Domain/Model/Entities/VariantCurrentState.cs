namespace OmniChannel.Catalog.Core.Domain.Model.Entities;

using OmniChannel.Catalog.Core.Domain.Model;

public class VariantCurrentState : CurrentStateEntity
{
    public string ProductId { get; set; } = null!;
    public string? Sku { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
    public DualStateField<decimal> Price { get; set; } = new();
    public DualStateField<int> Stock { get; set; } = new();
    public string? ProductTitle { get; set; }
}