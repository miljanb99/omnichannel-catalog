namespace OmniChannel.Catalog.Core.Domain.Model.Entities;

using OmniChannel.Catalog.Core.Domain.Model;

public class ListingCurrentState : CurrentStateEntity
{
    public string ProductId { get; set; } = null!;
    public string VariantId { get; set; } = null!;
    public string Channel { get; set; } = null!;
    public DualStateField<string> Title { get; set; } = new();
    public DualStateField<decimal> Price { get; set; } = new();
    public DualStateField<bool> Available { get; set; } = new();
    public string DesiredStatus { get; set; } = Constants.PublishStatus.Draft;
    public string? EffectiveStatus { get; set; }
    public decimal? ObservedPrice { get; set; }
    public string? ModerationNote { get; set; }
    public string PublishStatus { get; set; } = Constants.PublishStatus.Draft;
}