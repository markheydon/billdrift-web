namespace BillDrift.Application.Import;

/// <summary>
/// A single Stripe CSV file supplied to the ingestion pipeline.
/// </summary>
/// <param name="FileKind">Which export type the stream contains.</param>
/// <param name="Content">Readable CSV bytes.</param>
/// <param name="OriginalFileName">Optional filename for audit; not used for identity.</param>
public sealed record StripeCsvFileInput(
    StripeCsvFileKind FileKind,
    Stream Content,
    string? OriginalFileName = null);
