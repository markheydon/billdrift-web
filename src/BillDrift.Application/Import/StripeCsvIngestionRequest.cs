namespace BillDrift.Application.Import;

/// <summary>
/// Input bundle for Stripe CSV ingestion; at least a subscriptions file is required.
/// </summary>
/// <param name="Files">One to three CSV streams (subscriptions required).</param>
/// <param name="Options">Filtering and intake limits.</param>
public sealed record StripeCsvIngestionRequest(
    IReadOnlyList<StripeCsvFileInput> Files,
    StripeCsvIngestionOptions? Options = null);
