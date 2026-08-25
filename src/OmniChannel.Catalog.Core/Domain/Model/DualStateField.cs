namespace OmniChannel.Catalog.Core.Domain.Model;

public class DualStateField<T>
{
    [BsonElement("active")]
    public T? Active { get; set; }

    [BsonElement("draft")]
    public T? Draft { get; set; }

    [BsonElement("hasDraft")]
    public bool HasDraft { get; set; }

    public static DualStateField<T> FromActive(T? value) =>
        new() { Active = value, HasDraft = false };

    public void SetDraft(T? value)
    {
        Draft = value;
        HasDraft = true;
    }

    public void Publish()
    {
        if (!HasDraft)
        {
            return;
        }

        Active = Draft;
        Draft = default;
        HasDraft = false;
    }

    public void DiscardDraft()
    {
        Draft = default;
        HasDraft = false;
    }

    public T? Effective => HasDraft ? Draft : Active;
}