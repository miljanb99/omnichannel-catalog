namespace OmniChannel.Catalog.Data;

public class MongoContext : IMongoContext
{
    private readonly IMongoDatabase _database;
    private readonly MongoDbSettings _settings;

    public MongoContext(IMongoClient client, IOptions<MongoDbSettings> settings)
    {
        _settings = settings.Value;
        _database = client.GetDatabase(_settings.DatabaseName);
    }

    public IMongoCollection<ProductProprietaryState> ProductsProprietary =>
        _database.GetCollection<ProductProprietaryState>(_settings.ProductsProprietaryStatesCollectionName);

    public IMongoCollection<ProductCurrentState> ProductsCurrent =>
        _database.GetCollection<ProductCurrentState>(_settings.ProductsCurrentStateCollectionName);

    public IMongoCollection<VariantProprietaryState> VariantsProprietary =>
        _database.GetCollection<VariantProprietaryState>(_settings.VariantsProprietaryStatesCollectionName);

    public IMongoCollection<VariantCurrentState> VariantsCurrent =>
        _database.GetCollection<VariantCurrentState>(_settings.VariantsCurrentStateCollectionName);

    public IMongoCollection<ListingProprietaryState> ListingsProprietary =>
        _database.GetCollection<ListingProprietaryState>(_settings.ListingsProprietaryStatesCollectionName);

    public IMongoCollection<ListingObservedState> ListingsObserved =>
        _database.GetCollection<ListingObservedState>(_settings.ListingsObservedStatesCollectionName);

    public IMongoCollection<ListingCurrentState> ListingsCurrent =>
        _database.GetCollection<ListingCurrentState>(_settings.ListingsCurrentStateCollectionName);

    public IMongoCollection<ResumeToken> ResumeTokens =>
        _database.GetCollection<ResumeToken>(_settings.ResumeTokensCollectionName);
}