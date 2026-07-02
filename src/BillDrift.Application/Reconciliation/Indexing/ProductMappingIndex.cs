using BillDrift.Application.Mapping;
using BillDrift.Application.Reconciliation.Matching;
using BillDrift.Domain.Common;
using BillDrift.Domain.Mapping;

namespace BillDrift.Application.Reconciliation.Indexing;

/// <summary>
/// Index of product mappings for offer/SKU and supplier name variant lookups.
/// </summary>
public sealed class ProductMappingIndex
{
    private readonly Dictionary<(OfferId, SkuId), ProductMapping> _byRoot = new();
    private readonly IReadOnlyList<ProductMapping> _allMappings;
    private readonly IProductMappingResolver _resolver;
    private readonly DeterministicFuzzyNameMatcher _fuzzyMatcher;

    /// <summary>
    /// Builds a product mapping index from reconciliation inputs.
    /// </summary>
    /// <param name="mappings">Product mappings from the input snapshot.</param>
    /// <param name="resolver">Resolver for exact supplier name variant matching.</param>
    /// <param name="fuzzyMatcher">Deterministic fuzzy name matcher for fallback resolution.</param>
    /// <returns>A populated product mapping index.</returns>
    public static ProductMappingIndex Build(
        IReadOnlyList<ProductMapping> mappings,
        IProductMappingResolver resolver,
        DeterministicFuzzyNameMatcher fuzzyMatcher)
    {
        var index = new ProductMappingIndex(mappings, resolver, fuzzyMatcher);
        foreach (var mapping in mappings)
        {
            var key = (mapping.Key.OfferId, mapping.Key.SkuId);
            if (!index._byRoot.ContainsKey(key))
            {
                index._byRoot[key] = mapping;
            }
        }

        return index;
    }

    private ProductMappingIndex(
        IReadOnlyList<ProductMapping> allMappings,
        IProductMappingResolver resolver,
        DeterministicFuzzyNameMatcher fuzzyMatcher)
    {
        _allMappings = allMappings;
        _resolver = resolver;
        _fuzzyMatcher = fuzzyMatcher;
    }

    /// <summary>
    /// All product mappings in the index.
    /// </summary>
    public IReadOnlyList<ProductMapping> AllMappings => _allMappings;

    /// <summary>
    /// Attempts to retrieve a unique product mapping by commercial key root.
    /// </summary>
    public bool TryGetByRoot(CommercialKeyRoot root, out ProductMapping mapping) =>
        _byRoot.TryGetValue((root.OfferId, root.SkuId), out mapping!);

    /// <summary>
    /// Finds mappings matching a supplier product name via exact normalized variant lookup.
    /// </summary>
    public IReadOnlyList<ProductMapping> FindByNameVariant(string supplierName)
    {
        var result = _resolver.Resolve(supplierName, _allMappings);
        return result.Status switch
        {
            MappingResolutionStatus.Found => [result.Mapping!],
            _ => []
        };
    }

    /// <summary>
    /// Finds fuzzy name match candidates above the deterministic threshold.
    /// </summary>
    public IReadOnlyList<ProductMapping> FindFuzzyCandidates(string supplierName) =>
        _fuzzyMatcher.FindCandidates(supplierName, _allMappings);
}
