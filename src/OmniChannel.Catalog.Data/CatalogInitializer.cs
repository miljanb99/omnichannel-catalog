namespace OmniChannel.Catalog.Data;

public static class CatalogInitializer
{
    public static async Task InitializeAsync(IMongoClient client, IMongoContext context, MongoDbSettings settings, CancellationToken cancellationToken)
    {
        var database = client.GetDatabase(settings.DatabaseName);

        string[] currentStateCollections =
        [
            settings.ProductsCurrentStateCollectionName,
            settings.VariantsCurrentStateCollectionName,
            settings.ListingsCurrentStateCollectionName
        ];

        string[] logCollections =
        [
            settings.ProductsProprietaryStatesCollectionName,
            settings.VariantsProprietaryStatesCollectionName,
            settings.ListingsProprietaryStatesCollectionName,
            settings.ListingsObservedStatesCollectionName
        ];

        var existing = (await (await database.ListCollectionNamesAsync(cancellationToken: cancellationToken)).ToListAsync(cancellationToken)).ToHashSet();

        foreach (var name in currentStateCollections.Concat(logCollections).Append(settings.ResumeTokensCollectionName))
        {
            if (!existing.Contains(name))
            {
                await database.CreateCollectionAsync(name, cancellationToken: cancellationToken);
            }
        }

        foreach (var name in currentStateCollections)
        {
            await database.RunCommandAsync<BsonDocument>(
                new BsonDocument { { "collMod", name }, { "changeStreamPreAndPostImages", new BsonDocument("enabled", true) } },
                cancellationToken: cancellationToken);
        }

        await EnsureUniqueEntityIdAsync(context.ProductsCurrent, cancellationToken);
        await EnsureUniqueEntityIdAsync(context.VariantsCurrent, cancellationToken);
        await EnsureUniqueEntityIdAsync(context.ListingsCurrent, cancellationToken);

        await context.ResumeTokens.Indexes.CreateOneAsync(
            new CreateIndexModel<ResumeToken>(Builders<ResumeToken>.IndexKeys.Ascending(t => t.ServiceName), new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);
    }

    private static async Task EnsureUniqueEntityIdAsync<T>(IMongoCollection<T> collection, CancellationToken cancellationToken) where T : CurrentStateEntity =>
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<T>(Builders<T>.IndexKeys.Ascending(x => x.EntityId), new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);
}