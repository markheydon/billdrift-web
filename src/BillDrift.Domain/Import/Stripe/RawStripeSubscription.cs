namespace BillDrift.Domain.Import.Stripe;

public sealed record RawStripeSubscription(
    string SubscriptionId,
    string CustomerId,
    string Status,
    IReadOnlyDictionary<string, string> Metadata);
