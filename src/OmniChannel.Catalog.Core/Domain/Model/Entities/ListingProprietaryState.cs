namespace OmniChannel.Catalog.Core.Domain.Model.Entities;

public class ListingProprietaryState : AppendLogEntity
{
    public string ProductId { get; set; } = null!;
    public string VariantId { get; set; } = null!;
    public string Channel { get; set; } = null!;
    public string? Title { get; set; }
    public decimal? Price { get; set; }
    public bool? Available { get; set; }
    public string? DesiredStatus { get; set; }
    public bool DiscardDraft { get; set; }
    public bool Removed { get; set; }
}