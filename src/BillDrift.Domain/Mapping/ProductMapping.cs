using BillDrift.Domain.Common;

namespace BillDrift.Domain.Mapping;

public sealed record ProductMapping(
    ProductMappingId Id,
    CommercialKeyRoot Key,
    string NormalizedProductName,
    StripeProductId StripeProductId,
    IReadOnlyDictionary<PriceTermKey, StripePriceId> StripePricesByTerm,
    IReadOnlyList<SupplierNameVariant> SupplierNameVariants,
    ProductClassification Classification,
    MappingConfidence Confidence,
    MappingSource MappingSource);
