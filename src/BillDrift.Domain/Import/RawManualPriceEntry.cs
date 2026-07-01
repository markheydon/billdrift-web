using BillDrift.Domain.Common;

namespace BillDrift.Domain.Import;

/// <summary>
/// Raw manual price override entry that takes precedence over catalogue prices for the same <see cref="CommercialKey"/>.
/// </summary>
/// <param name="Id">Composite idempotency key for re-import deduplication.</param>
/// <param name="OfferIdRaw">Offer ID text; may be partial when overriding by product name only.</param>
/// <param name="SkuIdRaw">SKU ID text; may be partial when overriding by product name only.</param>
/// <param name="TermRaw">Contract term text to be mapped during normalization.</param>
/// <param name="FrequencyRaw">Billing frequency text to be mapped during normalization.</param>
/// <param name="WholesaleRaw">Wholesale override text, if specified.</param>
/// <param name="RrpRaw">RRP override text (required for manual entries).</param>
/// <param name="Reason">Operator-provided justification for the override.</param>
/// <param name="EffectiveDate">Date from which this override applies.</param>
/// <param name="EnteredAt">Timestamp when the override was recorded.</param>
public sealed record RawManualPriceEntry(
    RawImportId Id,
    string? OfferIdRaw,
    string? SkuIdRaw,
    string TermRaw,
    string FrequencyRaw,
    string? WholesaleRaw,
    string RrpRaw,
    string Reason,
    DateOnly EffectiveDate,
    DateTimeOffset EnteredAt);
