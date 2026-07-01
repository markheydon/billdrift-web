namespace BillDrift.Application.Import;

/// <summary>
/// Roll-up metrics from a Giacom PDF ingestion run, used to assess coverage and data quality at a glance.
/// </summary>
/// <param name="LinesExtracted">Count of <see cref="BillDrift.Domain.Import.RawGiacomBillingLine"/> records successfully emitted.</param>
/// <param name="LinesSkipped">Count of individual billing lines skipped due to parse or validation issues.</param>
/// <param name="BlocksSkipped">Count of entire customer blocks skipped (e.g. missing header or MEX ID).</param>
/// <param name="Warnings">Count of non-fatal issues logged (e.g. unparseable billing period on an otherwise extracted line).</param>
/// <param name="CustomerBlockCount">Total customer blocks identified in the document, including skipped blocks.</param>
public sealed record GiacomPdfIngestionSummary(
    int LinesExtracted,
    int LinesSkipped,
    int BlocksSkipped,
    int Warnings,
    int CustomerBlockCount);
