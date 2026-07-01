using BillDrift.Domain.Common;

namespace BillDrift.Domain.Mapping;

/// <summary>
/// Maps supplier product name variants to Stripe product and price IDs for a given <see cref="CommercialKeyRoot"/>.
/// Low <see cref="MappingConfidence"/> may cause <see cref="MismatchType.MappingMissing"/> or <see cref="MismatchType.MappingAmbiguous"/> during reconciliation.
/// </summary>
/// <param name="Id">Domain-generated identifier for this mapping.</param>
/// <param name="Key">Product identity (OfferId + SkuId) shared across term and frequency variants.</param>
/// <param name="NormalizedProductName">Canonical product name used in Stripe catalogue entries.</param>
/// <param name="StripeProductId">Target Stripe product for subscription items.</param>
/// <param name="StripePricesByTerm">Stripe price IDs keyed by term and billing frequency.</param>
/// <param name="SupplierNameVariants">Known supplier name spellings that resolve to this mapping.</param>
/// <param name="Classification">Whether this is a CSP or non-CSP product.</param>
/// <param name="Confidence">How reliable this mapping is for automatic reconciliation.</param>
/// <param name="MappingSource">How this mapping was established (manual, imported, or inferred).</param>
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
