using BillDrift.Domain.Common;

namespace BillDrift.Domain.Import;

public sealed record RawPriceListRow(
    RawImportId Id,
    string OfferIdRaw,
    string SkuIdRaw,
    string TermRaw,
    string FrequencyRaw,
    string WholesaleRaw,
    string RrpRaw,
    string? MarginRaw,
    string? MarginPercentRaw,
    string StatusRaw,
    string SourceDocumentId,
    int RowNumber);
