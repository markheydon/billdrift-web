namespace BillDrift.Application.Ingestion;

/// <summary>
/// Lifecycle status of a persisted ingestion upload run.
/// </summary>
public enum IngestionRunStatus
{
    /// <summary>Upload received; parsing or persistence in progress.</summary>
    InProgress = 0,

    /// <summary>All qualifying rows extracted with no skips.</summary>
    Completed = 1,

    /// <summary>Some rows skipped or warned but at least one row emitted.</summary>
    PartialSuccess = 2,

    /// <summary>No rows emitted or the file could not be processed.</summary>
    Failed = 3
}
