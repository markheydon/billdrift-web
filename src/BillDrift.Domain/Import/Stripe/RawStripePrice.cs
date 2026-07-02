using BillDrift.Domain.Common;

namespace BillDrift.Domain.Import.Stripe;

/// <summary>
/// Raw Stripe price record from export, defining unit amount and recurring billing interval for a product.
/// </summary>
public sealed record RawStripePrice(
    RawImportId Id,
    string PriceId,
    string ProductId,
    long? UnitAmount,
    string Currency,
    string? RecurringInterval,
    long? RecurringIntervalCount,
    string? Description,
    int SourceRowNumber,
    IReadOnlyDictionary<string, string> Metadata);
