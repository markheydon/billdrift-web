namespace BillDrift.Application.Import;

/// <summary>
/// Input for parsing a Giacom Subscription Management CSV export.
/// </summary>
/// <param name="Content">Readable CSV stream; buffered by the ingester for hashing and parsing.</param>
/// <param name="OriginalFileName">Original upload filename for traceability.</param>
/// <param name="Options">Parser limits and behaviour flags.</param>
public sealed record SubscriptionManagementCsvIngestionRequest(
    Stream Content,
    string? OriginalFileName = null,
    SubscriptionManagementCsvIngestionOptions? Options = null);
