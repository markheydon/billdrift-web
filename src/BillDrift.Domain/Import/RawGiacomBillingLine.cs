using BillDrift.Domain.Common;

namespace BillDrift.Domain.Import;

public sealed record RawGiacomBillingLine(
    RawImportId Id,
    string MexIdRaw,
    string ProductNameRaw,
    string QuantityRaw,
    string ChargeTypeRaw,
    string? PeriodStartRaw,
    string? PeriodEndRaw,
    string LineCostRaw,
    IReadOnlyList<string> SupplierReferenceIds,
    string SourceDocumentId,
    DateTimeOffset ExtractedAt);
