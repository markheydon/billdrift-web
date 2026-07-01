namespace BillDrift.Domain.Import.Stripe;

/// <summary>
/// Raw Stripe product record from export, representing an entry in the customer billing catalogue.
/// </summary>
/// <param name="ProductId">Stripe product ID as exported (e.g. <c>prod_...</c>).</param>
/// <param name="Name">Product display name in Stripe.</param>
/// <param name="Metadata">Full Stripe metadata dictionary for correlation with Giacom commercial keys.</param>
public sealed record RawStripeProduct(
    string ProductId,
    string Name,
    IReadOnlyDictionary<string, string> Metadata);
