namespace OmniChannel.Catalog.Tests;

using OmniChannel.Catalog.Core.Domain.Model;

[TestFixture]
public class DualStateFieldTests
{
    [Test]
    public void FromActive_sets_active_without_draft()
    {
        var field = DualStateField<decimal>.FromActive(100m);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(field.Active, Is.EqualTo(100m));
            Assert.That(field.HasDraft, Is.False);
            Assert.That(field.Effective, Is.EqualTo(100m));
        }

    }

    [Test]
    public void SetDraft_marks_pending_and_effective_is_draft()
    {
        var field = DualStateField<decimal>.FromActive(100m);

        field.SetDraft(120m);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(field.HasDraft, Is.True);
            Assert.That(field.Active, Is.EqualTo(100m));
            Assert.That(field.Draft, Is.EqualTo(120m));
            Assert.That(field.Effective, Is.EqualTo(120m));
        }

    }

    [Test]
    public void Publish_promotes_draft_to_active()
    {
        var field = DualStateField<decimal>.FromActive(100m);
        field.SetDraft(120m);

        field.Publish();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(field.Active, Is.EqualTo(120m));
            Assert.That(field.HasDraft, Is.False);
            Assert.That(field.Effective, Is.EqualTo(120m));
        }

    }

    [Test]
    public void DiscardDraft_keeps_active()
    {
        var field = DualStateField<decimal>.FromActive(100m);
        field.SetDraft(120m);

        field.DiscardDraft();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(field.Active, Is.EqualTo(100m));
            Assert.That(field.HasDraft, Is.False);
            Assert.That(field.Effective, Is.EqualTo(100m));
        }

    }
}