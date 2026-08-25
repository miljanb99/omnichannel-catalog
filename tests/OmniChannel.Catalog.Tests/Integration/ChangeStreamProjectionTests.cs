namespace OmniChannel.Catalog.Tests.Integration;

using OmniChannel.Catalog.Core.Configuration;
using OmniChannel.Catalog.Core.Domain;
using OmniChannel.Catalog.Core.Domain.Constants;
using OmniChannel.Catalog.Core.Domain.Model.Entities;
using OmniChannel.Catalog.Core.Domain.Repositories;
using OmniChannel.Catalog.Host.HostedServices;

[TestFixture]
public class ChangeStreamProjectionTests : CatalogIntegrationTest
{
    [Test]
    public async Task Projector_materializes_inserted_log_document()
    {
        var listings = GetService<IListingCurrentStateRepository>();
        var log = GetService<IAppendLogRepository<ListingProprietaryState>>();
        var projector = CreateProjector();

        await projector.StartAsync(CancellationToken.None);
        try
        {
            await Task.Delay(1000);
            await log.InsertAsync(NewListing("lst1", 100m), CancellationToken.None);
            await WaitUntilAsync(async () => await listings.GetAsync("lst1", CancellationToken.None) is not null, 10000);
        }
        finally
        {
            await projector.StopAsync(CancellationToken.None);
        }

        var current = await listings.GetAsync("lst1", CancellationToken.None);
        Assert.That(current!.Price.Draft, Is.EqualTo(100m));
    }

    [Test]
    public async Task Projector_resumes_from_token_after_restart()
    {
        var listings = GetService<IListingCurrentStateRepository>();
        var log = GetService<IAppendLogRepository<ListingProprietaryState>>();

        var first = CreateProjector();
        await first.StartAsync(CancellationToken.None);
        await Task.Delay(1000);
        await log.InsertAsync(NewListing("lstA", 100m), CancellationToken.None);
        await WaitUntilAsync(async () => await listings.GetAsync("lstA", CancellationToken.None) is not null, 10000);
        await first.StopAsync(CancellationToken.None);

        await log.InsertAsync(NewListing("lstB", 200m), CancellationToken.None);

        var second = CreateProjector();
        await second.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () => await listings.GetAsync("lstB", CancellationToken.None) is not null, 10000);
        }
        finally
        {
            await second.StopAsync(CancellationToken.None);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await listings.GetAsync("lstA", CancellationToken.None), Is.Not.Null);
            Assert.That(await listings.GetAsync("lstB", CancellationToken.None), Is.Not.Null);
        }

    }

    private ProjectListingsProprietaryService CreateProjector() =>
        new(
            GetService<IMongoContext>(),
            GetService<IListingCurrentStateRepository>(),
            GetService<IResumeTokenRepository>(),
            GetService<IOptions<MongoDbSettings>>(),
            GetService<ILogger<ProjectListingsProprietaryService>>());

    private static ListingProprietaryState NewListing(string id, decimal price) =>
        new()
        {
            EntityId = id,
            ProductId = "p1",
            VariantId = "v1",
            Channel = SalesChannel.Webshop,
            Title = "Listing " + id,
            Price = price,
            Available = true,
            DesiredStatus = PublishStatus.Published
        };
}