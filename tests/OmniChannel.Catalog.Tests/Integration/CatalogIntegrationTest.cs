namespace OmniChannel.Catalog.Tests.Integration;

using OmniChannel.Catalog.Core.Configuration;
using OmniChannel.Catalog.Core.Domain;
using OmniChannel.Catalog.Data;

public abstract class CatalogIntegrationTest
{
    private ServiceProvider _provider = null!;

    protected IServiceProvider Services => _provider;
    protected IMongoContext Context => _provider.GetRequiredService<IMongoContext>();
    protected MongoDbSettings Settings => _provider.GetRequiredService<IOptions<MongoDbSettings>>().Value;

    [SetUp]
    public async Task BaseSetUp()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var databaseName = "test_" + Guid.NewGuid().ToString("N");
        services.Configure<MongoDbSettings>(options =>
        {
            options.ConnectionString = MongoServer.Runner.ConnectionString;
            options.DatabaseName = databaseName;
            options.ParallelWorkers = 2;
            options.ResumeTokenSaveInterval = 1;
            options.MaxAwaitTimeMs = 200;
            options.BatchSize = 100;
            options.ChannelCapacity = 100;
        });
        services.Configure<ChannelSimulatorSettings>(options => options.Enabled = false);
        services.AddCatalogData();

        _provider = services.BuildServiceProvider();
        await CatalogInitializer.InitializeAsync(_provider.GetRequiredService<IMongoClient>(), Context, Settings, CancellationToken.None);
    }

    [TearDown]
    public async Task BaseTearDown()
    {
        await _provider.DisposeAsync();
    }

    protected T GetService<T>() where T : notnull => _provider.GetRequiredService<T>();

    protected static async Task WaitUntilAsync(Func<Task<bool>> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("Uslov nije ispunjen u zadatom vremenu");
    }
}