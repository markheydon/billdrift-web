using BillDrift.Application.Reconciliation.Indexing;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;

namespace BillDrift.Application.Reconciliation.Matching;

/// <summary>
/// Resolves commercial keys using the fixed priority chain (research R2):
/// ExplicitOfferSku → StripeMetadata → MappingByRoot → NameVariantExact → NameFuzzy → Unresolved.
/// </summary>
public sealed class CommercialKeyResolver
{
    private readonly ProductMappingIndex _mappingIndex;
    private readonly DeterministicFuzzyNameMatcher _fuzzyMatcher;

    /// <summary>
    /// Creates a resolver using the product mapping index and fuzzy matcher.
    /// </summary>
    public CommercialKeyResolver(ProductMappingIndex mappingIndex, DeterministicFuzzyNameMatcher fuzzyMatcher)
    {
        _mappingIndex = mappingIndex;
        _fuzzyMatcher = fuzzyMatcher;
    }

    /// <summary>
    /// Resolves product identity from a Microsoft subscription truth line.
    /// </summary>
    public CommercialKeyResolution Resolve(MicrosoftSubscriptionLine line)
    {
        var key = CommercialKey.Create(
            line.CommercialKeyRoot.OfferId,
            line.CommercialKeyRoot.SkuId,
            line.Term,
            line.Frequency);

        return new CommercialKeyResolution(
            line.CommercialKeyRoot,
            key,
            MatchConfidence.High,
            ProductResolutionPath.ExplicitOfferSku,
            _mappingIndex.TryGetByRoot(line.CommercialKeyRoot, out var mapping) ? mapping : null);
    }

    /// <summary>
    /// Resolves product identity from a Stripe billing item.
    /// </summary>
    public CommercialKeyResolution Resolve(StripeBillingItem item, BillingFrequency? expectedFrequency = null)
    {
        var offerId = item.MappingMetadata.OfferId;
        var skuId = item.MappingMetadata.SkuId;

        if (offerId is not null && skuId is not null)
        {
            var root = CommercialKeyRoot.Create(offerId.Value, skuId.Value);
            var frequency = expectedFrequency ?? item.Frequency;
            var key = CommercialKey.Create(offerId.Value, skuId.Value, Term.Monthly, frequency);
            _mappingIndex.TryGetByRoot(root, out var mapping);
            return new CommercialKeyResolution(
                root,
                key,
                MatchConfidence.High,
                ProductResolutionPath.StripeMetadata,
                mapping);
        }

        if (offerId is not null || skuId is not null)
        {
            var partialRoot = offerId is not null && skuId is not null
                ? CommercialKeyRoot.Create(offerId.Value, skuId.Value)
                : default(CommercialKeyRoot?);

            return new CommercialKeyResolution(
                partialRoot,
                null,
                MatchConfidence.Medium,
                ProductResolutionPath.StripeMetadata,
                null);
        }

        return Unresolved();
    }

    /// <summary>
    /// Resolves product identity from a supplier cost line by product name.
    /// </summary>
    public CommercialKeyResolution Resolve(SupplierCostLine line)
    {
        return ResolveByName(line.ProductName);
    }

    /// <summary>
    /// Resolves product identity from a supplier product name string.
    /// </summary>
    public CommercialKeyResolution ResolveByName(string supplierProductName)
    {
        var exactMatches = _mappingIndex.FindByNameVariant(supplierProductName);
        if (exactMatches.Count == 1)
        {
            var mapping = exactMatches[0];
            return new CommercialKeyResolution(
                mapping.Key,
                null,
                MatchConfidence.Medium,
                ProductResolutionPath.NameVariantExact,
                mapping);
        }

        if (exactMatches.Count > 1)
        {
            return new CommercialKeyResolution(
                null,
                null,
                MatchConfidence.None,
                ProductResolutionPath.Unresolved,
                null);
        }

        var fuzzyCandidates = _fuzzyMatcher.FindCandidates(supplierProductName, _mappingIndex.AllMappings);
        if (fuzzyCandidates.Count == 1)
        {
            var mapping = fuzzyCandidates[0];
            return new CommercialKeyResolution(
                mapping.Key,
                null,
                MatchConfidence.Low,
                ProductResolutionPath.NameFuzzy,
                mapping);
        }

        if (fuzzyCandidates.Count > 1)
        {
            return new CommercialKeyResolution(
                null,
                null,
                MatchConfidence.None,
                ProductResolutionPath.Unresolved,
                null);
        }

        return Unresolved();
    }

    /// <summary>
    /// Resolves product identity from a commercial key root via product mapping lookup.
    /// </summary>
    public CommercialKeyResolution ResolveByRoot(CommercialKeyRoot root)
    {
        if (_mappingIndex.TryGetByRoot(root, out var mapping))
        {
            return new CommercialKeyResolution(
                root,
                null,
                MatchConfidence.Medium,
                ProductResolutionPath.MappingByRoot,
                mapping);
        }

        return Unresolved();
    }

    private static CommercialKeyResolution Unresolved() =>
        new(null, null, MatchConfidence.None, ProductResolutionPath.Unresolved, null);
}
