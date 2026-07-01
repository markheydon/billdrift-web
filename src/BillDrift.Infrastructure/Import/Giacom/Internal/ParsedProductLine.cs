namespace BillDrift.Infrastructure.Import.Giacom.Internal;

public sealed record ParsedProductLine(
    int BlockIndex,
    int LineIndex,
    int PageNumber,
    string ProductNameRaw,
    string? QuantityRaw,
    string? ChargeTypeRaw,
    string? PeriodStartRaw,
    string? PeriodEndRaw,
    string? LineCostRaw,
    IReadOnlyList<string> SupplierReferenceIds,
    bool IsContinuationMerged);
