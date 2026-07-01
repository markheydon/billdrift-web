namespace BillDrift.Domain.Import.Stripe;

/// <summary>
/// Raw Stripe subscription record from export, linking a customer to billable subscription items.
/// </summary>
/// <param name="SubscriptionId">Stripe subscription ID as exported (e.g. <c>sub_...</c>).</param>
/// <param name="CustomerId">Stripe customer ID owning this subscription.</param>
/// <param name="Status">Subscription status string as exported from Stripe.</param>
/// <param name="Metadata">Full Stripe metadata dictionary for correlation with Giacom identifiers.</param>
public sealed record RawStripeSubscription(
    string SubscriptionId,
    string CustomerId,
    string Status,
    IReadOnlyDictionary<string, string> Metadata);
