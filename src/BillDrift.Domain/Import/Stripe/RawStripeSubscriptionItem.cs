namespace BillDrift.Domain.Import.Stripe;

public sealed record RawStripeSubscriptionItem(
    string SubscriptionItemId,
    string SubscriptionId,
    string PriceId,
    string ProductId,
    long Quantity,
    IReadOnlyDictionary<string, string> Metadata);
