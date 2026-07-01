using BillDrift.Domain.Common;
using BillDrift.Domain.Mapping;

namespace BillDrift.Application.Mapping;

public interface IProductMappingResolver
{
    ProductMappingResolution Resolve(string supplierProductName, IReadOnlyList<ProductMapping> mappings);
}

public sealed record ProductMappingResolution(
    ProductMapping? Mapping,
    MappingResolutionStatus Status);

public enum MappingResolutionStatus
{
    Found,
    NotFound,
    Ambiguous
}

public sealed class ProductMappingResolver : IProductMappingResolver
{
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
