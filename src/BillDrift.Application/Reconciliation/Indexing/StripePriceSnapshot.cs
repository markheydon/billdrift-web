using BillDrift.Domain.Common;

namespace BillDrift.Application.Reconciliation.Indexing;

/// <summary>
/// Read-only view of a Stripe price extracted during catalogue indexing.
/// </summary>
/// <param name="PriceId">Stripe price identifier.</param>
/// <param name="ProductId">Parent Stripe product identifier.</param>
/// <param name="UnitAmount">Unit price charged per billing interval.</param>
/// <param name="Interval">Billing frequency of this price.</param>
/// <param name="Currency">ISO currency code for the unit amount.</param>
public sealed record StripePriceSnapshot(
    StripePriceId PriceId,
    StripeProductId ProductId,
    Money UnitAmount,
    BillingFrequency Interval,
    CurrencyCode Currency);
