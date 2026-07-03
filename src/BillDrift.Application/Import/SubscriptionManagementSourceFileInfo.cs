namespace BillDrift.Application.Import;

/// <summary>
/// Metadata about the ingested Subscription Management CSV source file.
/// </summary>
/// <param name="SourceDocumentId">SHA-256 hex fingerprint of the CSV bytes.</param>
/// <param name="OriginalFileName">Original upload filename when known.</param>
/// <param name="RowCount">Number of data rows read from the CSV.</param>
public sealed record SubscriptionManagementSourceFileInfo(
    string SourceDocumentId,
    string? OriginalFileName,
    int RowCount);
