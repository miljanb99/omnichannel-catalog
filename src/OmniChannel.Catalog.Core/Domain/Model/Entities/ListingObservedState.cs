namespace OmniChannel.Catalog.Core.Domain.Model.Entities;

public class ListingObservedState : AppendLogEntity
{
    public string Channel { get; set; } = null!;
    public string EffectiveStatus { get; set; } = null!;
    public decimal? ObservedPrice { get; set; }
    public bool? Available { get; set; }
    public string? ModerationNote { get; set; }
    public DateTime ObservedAt { get; set; }
}