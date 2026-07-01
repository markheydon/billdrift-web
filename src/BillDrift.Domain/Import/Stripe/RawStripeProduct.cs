namespace BillDrift.Domain.Import.Stripe;

public sealed record RawStripeProduct(
    string ProductId,
    string Name,
    IReadOnlyDictionary<string, string> Metadata);
