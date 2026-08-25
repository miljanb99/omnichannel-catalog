namespace OmniChannel.Catalog.Tests.Integration;

using OmniChannel.Catalog.Core.Domain.Constants;
using OmniChannel.Catalog.Core.Domain.Model.Entities;
using OmniChannel.Catalog.Core.Domain.Repositories;
using OmniChannel.Catalog.Data;

[TestFixture]
public class ReplayTests : CatalogIntegrationTest
{
    [Test]
    public async Task Replay_reconstructs_current_state_from_log()
    {
        var replayer = GetService<CatalogReplayer>();
        var listings = GetService<IListingCurrentStateRepository>();
        var listingLog = GetService<IAppendLogRepository<ListingProprietaryState>>();
        var observedLog = GetService<IAppendLogRepository<ListingObservedState>>();

        var start = DateTime.UtcNow;
        await listingLog.InsertAsync(new ListingProprietaryState
        {
            EntityId = "lst1",
            ProductId = "p1",
            VariantId = "v1",
            Channel = SalesChannel.Webshop,
            Title = "Listing lst1",
            Price = 100m,
            Available = true,
            DesiredStatus = PublishStatus.Published,
            CreatedAt = start
        }, CancellationToken.None);
        await observedLog.InsertAsync(new ListingObservedState
        {
            EntityId = "lst1",
            Channel = SalesChannel.Webshop,
            EffectiveStatus = ChannelStatus.Active,
            ObservedPrice = 100m,
            Available = true,
            ObservedAt = start.AddSeconds(1),
            CreatedAt = start.AddSeconds(1)
        }, CancellationToken.None);

        var result = await replayer.ReplayAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ListingsProprietary, Is.EqualTo(1));
            Assert.That(result.ListingsObserved, Is.EqualTo(1));
        }


        var afterFirst = await listings.GetAsync("lst1", CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterFirst!.Price.Active, Is.EqualTo(100m));
            Assert.That(afterFirst.PublishStatus, Is.EqualTo(PublishStatus.Published));
        }

    }

    [Test]
    public async Task Replay_is_idempotent()
    {
        var replayer = GetService<CatalogReplayer>();
        var listings = GetService<IListingCurrentStateRepository>();
        var listingLog = GetService<IAppendLogRepository<ListingProprietaryState>>();

        await listingLog.InsertAsync(new ListingProprietaryState
        {
            EntityId = "lst1",
            ProductId = "p1",
            VariantId = "v1",
            Channel = SalesChannel.Webshop,
            Price = 100m,
            Available = true,
            DesiredStatus = PublishStatus.Published,
            CreatedAt = DateTime.UtcNow
        }, CancellationToken.None);

        await replayer.ReplayAsync(CancellationToken.None);
        await replayer.ReplayAsync(CancellationToken.None);

        var all = await listings.GetAllAsync(CancellationToken.None);
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Price.Draft, Is.EqualTo(100m));
    }
}