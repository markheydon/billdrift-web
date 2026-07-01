using BillDrift.Domain.Mapping;

namespace BillDrift.Application.Mapping;

/// <summary>
/// Resolves a supplier product name to a <see cref="ProductMapping"/> with <see cref="BillDrift.Domain.Common.CommercialKey"/> and Stripe identifiers.
/// Used during reconciliation when matching Giacom or subscription-management lines to catalogue entries.
/// </summary>
public interface IProductMappingResolver
{
    /// <summary>
    /// Looks up a supplier product name against known name variants and returns the match outcome.
    /// </summary>
    /// <param name="supplierProductName">Raw product name from a supplier billing or subscription line.</param>
    /// <param name="mappings">Catalogue of product mappings with normalized supplier name variants.</param>
    /// <returns>Resolution result indicating a unique match, no match, or ambiguous multiple matches.</returns>
    ProductMappingResolution Resolve(string supplierProductName, IReadOnlyList<ProductMapping> mappings);
}

/// <summary>
/// Outcome of resolving a supplier product name to a catalogue <see cref="ProductMapping"/>.
/// </summary>
/// <param name="Mapping">Matched mapping when <see cref="Status"/> is <see cref="MappingResolutionStatus.Found"/>; otherwise <c>null</c>.</param>
/// <param name="Status">Whether resolution succeeded, found no candidate, or found multiple candidates.</param>
public sealed record ProductMappingResolution(
    ProductMapping? Mapping,
    MappingResolutionStatus Status);

/// <summary>
/// Indicates how a supplier product name resolved against the product mapping catalogue.
/// </summary>
public enum MappingResolutionStatus
{
    /// <summary>Exactly one mapping matched the normalized supplier product name.</summary>
    Found,

    /// <summary>No mapping contained a matching supplier name variant.</summary>
    NotFound,

    /// <summary>Multiple mappings matched, requiring manual disambiguation before reconciliation can proceed.</summary>
    Ambiguous
}

/// <summary>
/// Default implementation of <see cref="IProductMappingResolver"/> using case-insensitive normalized name variant matching.
/// </summary>
public sealed class ProductMappingResolver : IProductMappingResolver
{
    /// <inheritdoc />
    public ProductMappingResolution Resolve(string supplierProductName, IReadOnlyList<ProductMapping> mappings)
    {
        var normalized = supplierProductName.Trim().ToLowerInvariant();
        var candidates = mappings
            .Where(m => m.SupplierNameVariants.Any(v => v.NormalizedName == normalized))
            .ToList();

        return candidates.Count switch
        {
            0 => new ProductMappingResolution(null, MappingResolutionStatus.NotFound),
            1 => new ProductMappingResolution(candidates[0], MappingResolutionStatus.Found),
            _ => new ProductMappingResolution(null, MappingResolutionStatus.Ambiguous)
        };
    }
}
