namespace OmniChannel.Catalog.Core.Domain.Model.Entities;

public class ProductProprietaryState : AppendLogEntity
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public decimal? BasePrice { get; set; }
    public bool Removed { get; set; }
}