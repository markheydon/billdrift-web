using BillDrift.Domain.Common;

namespace BillDrift.Domain.CatalogueReconciliation;

/// <summary>Normalized Stripe product snapshot for catalogue reconciliation.</summary>
public sealed record StripeCatalogueProduct(
    StripeProductId ProductId,
    string Name,
    OfferId? OfferId,
    SkuId? SkuId,
    bool IsActive,
    IReadOnlyDictionary<string, string> Metadata);
