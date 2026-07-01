using BillDrift.Domain.Common;

namespace BillDrift.Domain.Import;

/// <summary>
/// Raw line extracted from a Giacom billing PDF, preserving source field values before normalization.
/// Stringly-typed fields defer parsing to the Application layer to maintain import fidelity.
/// </summary>
/// <param name="Id">Composite idempotency key for re-import deduplication.</param>
/// <param name="MexIdRaw">MexId as extracted from the PDF without validation.</param>
/// <param name="ProductNameRaw">Product name exactly as written on the invoice.</param>
/// <param name="QuantityRaw">Quantity text to be parsed during normalization.</param>
/// <param name="ChargeTypeRaw">Charge type text (e.g. "Recurring", "Pro-rated adjustment").</param>
/// <param name="PeriodStartRaw">Billing period start as extracted text, if present.</param>
/// <param name="PeriodEndRaw">Billing period end as extracted text, if present.</param>
/// <param name="LineCostRaw">Line cost text to be parsed into <see cref="Money"/> during normalization.</param>
/// <param name="SupplierReferenceIds">All reference column values from the PDF for correlation.</param>
/// <param name="SourceDocumentId">Blob path or upload ID identifying the source PDF.</param>
/// <param name="ExtractedAt">Timestamp when this line was imported.</param>
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
