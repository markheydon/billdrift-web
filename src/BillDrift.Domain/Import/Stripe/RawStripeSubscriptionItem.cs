using BillDrift.Domain.Common;

namespace BillDrift.Domain.Import.Stripe;

/// <summary>
/// Raw Stripe subscription item from export, representing a single billable line on a subscription.
/// </summary>
public sealed record RawStripeSubscriptionItem(
    RawImportId Id,
    string SubscriptionItemId,
    string SubscriptionId,
    string PriceId,
    string ProductId,
    string CustomerId,
    long Quantity,
    string? ProductName,
    string SubscriptionStatus,
    string? UnitAmountRaw,
    string? IntervalRaw,
    int SourceRowNumber,
    IReadOnlyDictionary<string, string> Metadata);
