namespace OmniChannel.Catalog.Data;

using OmniChannel.Catalog.Data.Repositories;

public static class ServiceCollectionExtensions
{
    private static bool _bsonConfigured;

    public static IServiceCollection AddCatalogData(this IServiceCollection services)
    {
        ConfigureBson();

        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            var clientSettings = MongoClientSettings.FromConnectionString(settings.ConnectionString);
            clientSettings.RetryReads = true;
            return new MongoClient(clientSettings);
        });

        services.AddSingleton<IMongoContext, MongoContext>();
        services.AddSingleton<KeyedAsyncLock>();
        services.AddSingleton<IResumeTokenRepository, ResumeTokenRepository>();
        services.AddSingleton<IProductCurrentStateRepository, ProductCurrentStateRepository>();
        services.AddSingleton<IVariantCurrentStateRepository, VariantCurrentStateRepository>();
        services.AddSingleton<IListingCurrentStateRepository, ListingCurrentStateRepository>();

        services.AddSingleton<IAppendLogRepository<ProductProprietaryState>>(sp =>
            new AppendLogRepository<ProductProprietaryState>(sp.GetRequiredService<IMongoContext>().ProductsProprietary));
        services.AddSingleton<IAppendLogRepository<VariantProprietaryState>>(sp =>
            new AppendLogRepository<VariantProprietaryState>(sp.GetRequiredService<IMongoContext>().VariantsProprietary));
        services.AddSingleton<IAppendLogRepository<ListingProprietaryState>>(sp =>
            new AppendLogRepository<ListingProprietaryState>(sp.GetRequiredService<IMongoContext>().ListingsProprietary));
        services.AddSingleton<IAppendLogRepository<ListingObservedState>>(sp =>
            new AppendLogRepository<ListingObservedState>(sp.GetRequiredService<IMongoContext>().ListingsObserved));

        services.AddSingleton<CatalogReplayer>();
        return services;
    }

    public static void ConfigureBson()
    {
        if (_bsonConfigured)
        {
            return;
        }

        ConventionRegistry.Register("camelCase", new ConventionPack { new CamelCaseElementNameConvention() }, _ => true);
        BsonSerializer.TryRegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
        BsonSerializer.TryRegisterSerializer(new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));
        _bsonConfigured = true;
    }
}