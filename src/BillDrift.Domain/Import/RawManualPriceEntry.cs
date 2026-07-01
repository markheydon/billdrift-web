using BillDrift.Domain.Common;

namespace BillDrift.Domain.Import;

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
