namespace OmniChannel.Catalog.Tests.Integration;

[SetUpFixture]
public class MongoServer
{
    public static IMongoRunner Runner { get; private set; } = null!;

    [OneTimeSetUp]
    public void StartServer()
    {
        Runner = MongoRunner.Run(new MongoRunnerOptions { UseSingleNodeReplicaSet = true });
    }

    [OneTimeTearDown]
    public void StopServer()
    {
        Runner.Dispose();
    }
}