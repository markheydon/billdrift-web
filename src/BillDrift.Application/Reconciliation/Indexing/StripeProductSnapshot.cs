using BillDrift.Domain.Common;

namespace BillDrift.Application.Reconciliation.Indexing;

/// <summary>
/// Read-only view of a Stripe product extracted during catalogue indexing.
/// </summary>
/// <param name="ProductId">Stripe product identifier.</param>
/// <param name="Name">Product display name.</param>
/// <param name="OfferId">Microsoft CSP offer ID from metadata, when present.</param>
/// <param name="SkuId">Microsoft CSP SKU ID from metadata, when present.</param>
public sealed record StripeProductSnapshot(
    StripeProductId ProductId,
    string Name,
    OfferId? OfferId,
    SkuId? SkuId);
