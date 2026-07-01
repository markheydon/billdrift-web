using BillDrift.Domain.Common;

namespace BillDrift.Domain.Billing;

/// <summary>
/// Flattened view of a Stripe subscription item for reconciliation — one row per billable item in customer billing (source of truth).
/// Compared against supplier cost, subscription truth, and intended pricing to detect billing drift.
/// </summary>
/// <param name="Id">Domain-generated identifier assigned during normalization.</param>
/// <param name="Customer">Customer identity with MexId and optional Stripe customer ID.</param>
/// <param name="SubscriptionId">Stripe subscription containing this item.</param>
/// <param name="SubscriptionItemId">Stripe subscription item ID for proposed quantity or price changes.</param>
/// <param name="ProductId">Stripe product in the customer catalogue.</param>
/// <param name="PriceId">Stripe price defining unit amount and billing interval.</param>
/// <param name="Quantity">Current billed quantity on the subscription item.</param>
/// <param name="Frequency">Normalized billing frequency of the Stripe price.</param>
/// <param name="UnitAmount">Unit price charged per billing interval.</param>
/// <param name="MappingMetadata">Giacom correlation metadata from Stripe item metadata.</param>
/// <param name="Source">Traceability link back to the raw Stripe export.</param>
public sealed record StripeBillingItem(
    StripeBillingItemId Id,
    CustomerIdentity Customer,
    StripeSubscriptionId SubscriptionId,
    StripeSubscriptionItemId SubscriptionItemId,
    StripeProductId ProductId,
    StripePriceId PriceId,
    long Quantity,
    BillingFrequency Frequency,
    Money UnitAmount,
    StripeMappingMetadata MappingMetadata,
    SourceReference Source);
