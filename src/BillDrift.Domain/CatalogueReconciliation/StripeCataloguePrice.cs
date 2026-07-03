using BillDrift.Domain.Common;

namespace BillDrift.Domain.CatalogueReconciliation;

/// <summary>Normalized Stripe price snapshot for catalogue reconciliation.</summary>
public sealed record StripeCataloguePrice(
    StripePriceId PriceId,
    StripeProductId ProductId,
    Money UnitAmount,
    BillingFrequency Frequency,
    Term? Term,
    bool IsActive);
