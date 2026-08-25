namespace OmniChannel.Catalog.Tests.Integration;

using OmniChannel.Catalog.Core.Domain.Constants;
using OmniChannel.Catalog.Core.Domain.Model.Entities;
using OmniChannel.Catalog.Core.Domain.Repositories;

[TestFixture]
public class ReconciliationTests : CatalogIntegrationTest
{
    [Test]
    public async Task Proprietary_materializes_listing_with_draft()
    {
        var listings = GetService<IListingCurrentStateRepository>();
        await listings.ApplyProprietaryAsync(NewListing("lst1", price: 100m, createdAt: DateTime.UtcNow), CancellationToken.None);

        var current = await listings.GetAsync("lst1", CancellationToken.None);

        Assert.That(current, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(current!.Price.HasDraft, Is.True);
            Assert.That(current.Price.Draft, Is.EqualTo(100m));
            Assert.That(current.DesiredStatus, Is.EqualTo(PublishStatus.Published));
            Assert.That(current.PublishStatus, Is.EqualTo(PublishStatus.Pending));
        }

    }

    [Test]
    public async Task Observed_active_publishes_matching_draft()
    {
        var listings = GetService<IListingCurrentStateRepository>();
        await listings.ApplyProprietaryAsync(NewListing("lst1", price: 100m, createdAt: DateTime.UtcNow), CancellationToken.None);
        await listings.ApplyObservedAsync(new ListingObservedState
        {
            EntityId = "lst1",
            Channel = SalesChannel.Webshop,
            EffectiveStatus = ChannelStatus.Active,
            ObservedPrice = 100m,
            Available = true,
            ObservedAt = DateTime.UtcNow
        }, CancellationToken.None);

        var current = await listings.GetAsync("lst1", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(current!.Price.HasDraft, Is.False);
            Assert.That(current.Price.Active, Is.EqualTo(100m));
            Assert.That(current.PublishStatus, Is.EqualTo(PublishStatus.Published));
        }

    }

    [Test]
    public async Task Out_of_order_proprietary_is_ignored()
    {
        var listings = GetService<IListingCurrentStateRepository>();
        var newer = DateTime.UtcNow;
        var older = newer.AddSeconds(-10);

        await listings.ApplyProprietaryAsync(NewListing("lst1", price: 100m, createdAt: newer), CancellationToken.None);
        await listings.ApplyProprietaryAsync(NewListing("lst1", price: 50m, createdAt: older), CancellationToken.None);

        var current = await listings.GetAsync("lst1", CancellationToken.None);

        Assert.That(current!.Price.Draft, Is.EqualTo(100m));
    }

    [Test]
    public async Task Reapplying_same_event_is_idempotent()
    {
        var listings = GetService<IListingCurrentStateRepository>();
        var evt = NewListing("lst1", price: 100m, createdAt: DateTime.UtcNow);

        await listings.ApplyProprietaryAsync(evt, CancellationToken.None);
        await listings.ApplyProprietaryAsync(evt, CancellationToken.None);

        var all = await listings.GetAllAsync(CancellationToken.None);
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Price.Draft, Is.EqualTo(100m));
    }

    [Test]
    public async Task Observed_rejected_sets_rejected_status()
    {
        var listings = GetService<IListingCurrentStateRepository>();
        await listings.ApplyProprietaryAsync(NewListing("lst1", price: 100m, createdAt: DateTime.UtcNow), CancellationToken.None);
        await listings.ApplyObservedAsync(new ListingObservedState
        {
            EntityId = "lst1",
            Channel = SalesChannel.MarketplaceB,
            EffectiveStatus = ChannelStatus.Rejected,
            ModerationNote = "Prekršaj pravila",
            ObservedAt = DateTime.UtcNow
        }, CancellationToken.None);

        var current = await listings.GetAsync("lst1", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(current!.PublishStatus, Is.EqualTo(PublishStatus.Rejected));
            Assert.That(current.ModerationNote, Is.EqualTo("Prekršaj pravila"));
        }

    }

    [Test]
    public async Task Withdraw_desired_sets_withdrawn_status()
    {
        var listings = GetService<IListingCurrentStateRepository>();
        var now = DateTime.UtcNow;
        await listings.ApplyProprietaryAsync(NewListing("lst1", price: 100m, createdAt: now), CancellationToken.None);
        await listings.ApplyObservedAsync(new ListingObservedState
        {
            EntityId = "lst1",
            Channel = SalesChannel.Webshop,
            EffectiveStatus = ChannelStatus.Active,
            ObservedPrice = 100m,
            Available = true,
            ObservedAt = now.AddMilliseconds(1)
        }, CancellationToken.None);

        await listings.ApplyProprietaryAsync(new ListingProprietaryState
        {
            EntityId = "lst1",
            ProductId = "p1",
            VariantId = "v1",
            Channel = SalesChannel.Webshop,
            DesiredStatus = PublishStatus.Withdrawn,
            CreatedAt = now.AddSeconds(1)
        }, CancellationToken.None);

        var current = await listings.GetAsync("lst1", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(current!.DesiredStatus, Is.EqualTo(PublishStatus.Withdrawn));
            Assert.That(current.PublishStatus, Is.EqualTo(PublishStatus.Withdrawn));
        }

    }

    [Test]
    public async Task Republish_after_withdraw_returns_to_pending()
    {
        var listings = GetService<IListingCurrentStateRepository>();
        var now = DateTime.UtcNow;
        await listings.ApplyProprietaryAsync(NewListing("lst1", price: 100m, createdAt: now), CancellationToken.None);
        await listings.ApplyObservedAsync(new ListingObservedState
        {
            EntityId = "lst1",
            Channel = SalesChannel.Webshop,
            EffectiveStatus = ChannelStatus.Active,
            ObservedPrice = 100m,
            Available = true,
            ObservedAt = now.AddMilliseconds(1)
        }, CancellationToken.None);

        await listings.ApplyProprietaryAsync(new ListingProprietaryState
        {
            EntityId = "lst1",
            ProductId = "p1",
            VariantId = "v1",
            Channel = SalesChannel.Webshop,
            DesiredStatus = PublishStatus.Withdrawn,
            CreatedAt = now.AddSeconds(1)
        }, CancellationToken.None);

        await listings.ApplyProprietaryAsync(new ListingProprietaryState
        {
            EntityId = "lst1",
            ProductId = "p1",
            VariantId = "v1",
            Channel = SalesChannel.Webshop,
            DesiredStatus = PublishStatus.Published,
            CreatedAt = now.AddSeconds(2)
        }, CancellationToken.None);

        var current = await listings.GetAsync("lst1", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(current!.DesiredStatus, Is.EqualTo(PublishStatus.Published));
            Assert.That(current.PublishStatus, Is.EqualTo(PublishStatus.Pending));
            Assert.That(current.Price.Active, Is.EqualTo(100m));
        }

    }

    [Test]
    public async Task Discard_draft_restores_channel_confirmed_state()
    {
        var listings = GetService<IListingCurrentStateRepository>();
        var now = DateTime.UtcNow;
        await listings.ApplyProprietaryAsync(NewListing("lst1", price: 100m, createdAt: now), CancellationToken.None);
        await listings.ApplyObservedAsync(new ListingObservedState
        {
            EntityId = "lst1",
            Channel = SalesChannel.Webshop,
            EffectiveStatus = ChannelStatus.Active,
            ObservedPrice = 100m,
            Available = true,
            ObservedAt = now.AddMilliseconds(1)
        }, CancellationToken.None);

        await listings.ApplyProprietaryAsync(new ListingProprietaryState
        {
            EntityId = "lst1",
            ProductId = "p1",
            VariantId = "v1",
            Channel = SalesChannel.Webshop,
            Price = 250m,
            CreatedAt = now.AddSeconds(1)
        }, CancellationToken.None);

        var pending = await listings.GetAsync("lst1", CancellationToken.None);
        Assert.That(pending!.PublishStatus, Is.EqualTo(PublishStatus.Pending));

        await listings.ApplyProprietaryAsync(new ListingProprietaryState
        {
            EntityId = "lst1",
            ProductId = "p1",
            VariantId = "v1",
            Channel = SalesChannel.Webshop,
            DiscardDraft = true,
            CreatedAt = now.AddSeconds(2)
        }, CancellationToken.None);

        var current = await listings.GetAsync("lst1", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(current!.Price.HasDraft, Is.False);
            Assert.That(current.Price.Active, Is.EqualTo(100m));
            Assert.That(current.PublishStatus, Is.EqualTo(PublishStatus.Published));
        }

    }

    [Test]
    public async Task Observed_paused_marks_listing_withdrawn()
    {
        var listings = GetService<IListingCurrentStateRepository>();
        var now = DateTime.UtcNow;
        await listings.ApplyProprietaryAsync(NewListing("lst1", price: 100m, createdAt: now), CancellationToken.None);
        await listings.ApplyObservedAsync(new ListingObservedState
        {
            EntityId = "lst1",
            Channel = SalesChannel.Webshop,
            EffectiveStatus = ChannelStatus.Active,
            ObservedPrice = 100m,
            Available = true,
            ObservedAt = now.AddMilliseconds(1)
        }, CancellationToken.None);

        await listings.ApplyObservedAsync(new ListingObservedState
        {
            EntityId = "lst1",
            Channel = SalesChannel.Webshop,
            EffectiveStatus = ChannelStatus.Paused,
            ObservedAt = now.AddSeconds(1)
        }, CancellationToken.None);

        var current = await listings.GetAsync("lst1", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(current!.EffectiveStatus, Is.EqualTo(ChannelStatus.Paused));
            Assert.That(current.PublishStatus, Is.EqualTo(PublishStatus.Withdrawn));
        }

    }

    [Test]
    public async Task Product_change_cascades_to_children()
    {
        var products = GetService<IProductCurrentStateRepository>();
        var variants = GetService<IVariantCurrentStateRepository>();
        var listings = GetService<IListingCurrentStateRepository>();

        await variants.ApplyProprietaryAsync(new VariantProprietaryState { EntityId = "v1", ProductId = "p1", Price = 100m, Stock = 5, CreatedAt = DateTime.UtcNow }, CancellationToken.None);
        await listings.ApplyProprietaryAsync(NewListing("lst1", price: 100m, createdAt: DateTime.UtcNow, productId: "p1", variantId: "v1"), CancellationToken.None);
        await products.ApplyProprietaryAsync(new ProductProprietaryState { EntityId = "p1", Title = "Proizvod X", CreatedAt = DateTime.UtcNow }, CancellationToken.None);

        var variant = await variants.GetAsync("v1", CancellationToken.None);
        Assert.That(variant!.ProductTitle, Is.EqualTo("Proizvod X"));

        await products.ApplyProprietaryAsync(new ProductProprietaryState { EntityId = "p1", Removed = true, CreatedAt = DateTime.UtcNow.AddSeconds(1) }, CancellationToken.None);

        variant = await variants.GetAsync("v1", CancellationToken.None);
        var listing = await listings.GetAsync("lst1", CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(variant!.Removed, Is.True);
            Assert.That(listing!.Removed, Is.True);
            Assert.That(listing.PublishStatus, Is.EqualTo(PublishStatus.Withdrawn));
        }

    }

    [Test]
    public async Task Concurrent_proprietary_and_observed_do_not_lose_desired_price()
    {
        var listings = GetService<IListingCurrentStateRepository>();
        for (var i = 0; i < 40; i++)
        {
            var id = "lstc" + i;
            var price = 100m + i;
            var now = DateTime.UtcNow;
            var proprietary = NewListing(id, price: price, createdAt: now);
            var observed = new ListingObservedState
            {
                EntityId = id,
                Channel = SalesChannel.Webshop,
                EffectiveStatus = ChannelStatus.Active,
                ObservedPrice = price,
                Available = true,
                ObservedAt = now.AddMilliseconds(1)
            };

            await Task.WhenAll(
                listings.ApplyProprietaryAsync(proprietary, CancellationToken.None),
                listings.ApplyObservedAsync(observed, CancellationToken.None));

            var current = await listings.GetAsync(id, CancellationToken.None);
            Assert.That(current, Is.Not.Null);
            Assert.That(current!.Price.Effective, Is.EqualTo(price));
        }
    }

    private static ListingProprietaryState NewListing(string id, decimal price, DateTime createdAt, string productId = "p1", string variantId = "v1") =>
        new()
        {
            EntityId = id,
            ProductId = productId,
            VariantId = variantId,
            Channel = SalesChannel.Webshop,
            Title = "Listing " + id,
            Price = price,
            Available = true,
            DesiredStatus = PublishStatus.Published,
            CreatedAt = createdAt
        };
}