namespace OmniChannel.Catalog.Core.Configuration;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;

    public string ProductsProprietaryStatesCollectionName { get; set; } = "productsProprietaryStates";
    public string ProductsObservedStatesCollectionName { get; set; } = "productsObservedStates";
    public string ProductsCurrentStateCollectionName { get; set; } = "productsCurrentState";

    public string VariantsProprietaryStatesCollectionName { get; set; } = "variantsProprietaryStates";
    public string VariantsObservedStatesCollectionName { get; set; } = "variantsObservedStates";
    public string VariantsCurrentStateCollectionName { get; set; } = "variantsCurrentState";

    public string ListingsProprietaryStatesCollectionName { get; set; } = "listingsProprietaryStates";
    public string ListingsObservedStatesCollectionName { get; set; } = "listingsObservedStates";
    public string ListingsCurrentStateCollectionName { get; set; } = "listingsCurrentState";

    public string ResumeTokensCollectionName { get; set; } = "resumeTokens";

    public int BatchSize { get; set; } = 1000;
    public int MaxAwaitTimeMs { get; set; } = 1000;
    public int ChannelCapacity { get; set; } = 1000;
    public int ParallelWorkers { get; set; } = 8;
    public int ResumeTokenSaveInterval { get; set; } = 50;
    public int MaxReconnectAttempts { get; set; } = 5;
    public int ReconnectDelayMs { get; set; } = 2000;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 100;
}