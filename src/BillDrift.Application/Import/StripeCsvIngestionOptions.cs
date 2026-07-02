namespace BillDrift.Application.Import;

/// <summary>
/// Options controlling Stripe CSV ingestion behaviour such as status filtering and file size limits.
/// </summary>
/// <param name="IncludeInactiveSubscriptions">When false, only active-status subscriptions are emitted.</param>
/// <param name="MaxFileSizeBytes">Maximum allowed size per CSV file in bytes.</param>
public sealed record StripeCsvIngestionOptions(
    bool IncludeInactiveSubscriptions = false,
    long MaxFileSizeBytes = 10_485_760);
