namespace BillDrift.Domain.Import.Stripe;

/// <summary>
/// Raw Stripe subscription item from export, representing a single billable line on a subscription.
/// </summary>
/// <param name="SubscriptionItemId">Stripe subscription item ID as exported (e.g. <c>si_...</c>).</param>
/// <param name="SubscriptionId">Parent Stripe subscription ID.</param>
/// <param name="PriceId">Stripe price ID defining unit amount and billing interval.</param>
/// <param name="ProductId">Stripe product ID in the customer catalogue.</param>
/// <param name="Quantity">Billed quantity as exported from Stripe.</param>
/// <param name="Metadata">Full Stripe metadata dictionary including OfferId, SkuId, and MexId when configured.</param>
public sealed record RawStripeSubscriptionItem(
    string SubscriptionItemId,
    string SubscriptionId,
    string PriceId,
    string ProductId,
    long Quantity,
    IReadOnlyDictionary<string, string> Metadata);
