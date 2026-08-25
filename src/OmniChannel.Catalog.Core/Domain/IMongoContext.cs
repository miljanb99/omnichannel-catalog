namespace OmniChannel.Catalog.Core.Domain;

using OmniChannel.Catalog.Core.Domain.Model.Entities;

public interface IMongoContext
{
    IMongoCollection<ProductProprietaryState> ProductsProprietary { get; }
    IMongoCollection<ProductCurrentState> ProductsCurrent { get; }

    IMongoCollection<VariantProprietaryState> VariantsProprietary { get; }
    IMongoCollection<VariantCurrentState> VariantsCurrent { get; }

    IMongoCollection<ListingProprietaryState> ListingsProprietary { get; }
    IMongoCollection<ListingObservedState> ListingsObserved { get; }
    IMongoCollection<ListingCurrentState> ListingsCurrent { get; }

    IMongoCollection<ResumeToken> ResumeTokens { get; }
}