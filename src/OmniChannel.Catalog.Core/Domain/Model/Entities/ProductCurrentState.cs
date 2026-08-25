namespace OmniChannel.Catalog.Core.Domain.Model.Entities;

using OmniChannel.Catalog.Core.Domain.Model;

public class ProductCurrentState : CurrentStateEntity
{
    public DualStateField<string> Title { get; set; } = new();
    public DualStateField<string> Description { get; set; } = new();
    public DualStateField<decimal> BasePrice { get; set; } = new();
    public string? Category { get; set; }
}