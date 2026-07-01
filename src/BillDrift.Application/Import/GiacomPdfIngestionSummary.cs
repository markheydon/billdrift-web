namespace BillDrift.Application.Import;

public sealed record GiacomPdfIngestionSummary(
    int LinesExtracted,
    int LinesSkipped,
    int BlocksSkipped,
    int Warnings,
    int CustomerBlockCount);
