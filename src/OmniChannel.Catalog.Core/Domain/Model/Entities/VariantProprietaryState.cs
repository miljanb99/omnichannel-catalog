namespace OmniChannel.Catalog.Core.Domain.Model.Entities;

public class VariantProprietaryState : AppendLogEntity
{
    public string ProductId { get; set; } = null!;
    public string? Sku { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
    public decimal? Price { get; set; }
    public int? Stock { get; set; }
    public bool Removed { get; set; }
}