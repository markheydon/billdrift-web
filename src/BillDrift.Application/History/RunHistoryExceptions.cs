using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.History;

/// <summary>Thrown when attempting to re-persist a run that already has an archived record.</summary>
public sealed class RunAlreadyArchivedException(RunId runId) : Exception($"Run {runId.Value} already has an archived record and cannot be re-persisted. Retry with a new run.")
{
    /// <summary>The run identifier that is already archived.</summary>
    public RunId RunId { get; } = runId;
}

/// <summary>Thrown when blob content hash does not match manifest.</summary>
public sealed class RunArchiveIntegrityException(string message) : Exception(message);

/// <summary>Thrown when a run record is not found.</summary>
public sealed class RunNotFoundException(RunId runId) : Exception($"Run {runId.Value} was not found.")
{
    /// <summary>The run identifier that was not found.</summary>
    public RunId RunId { get; } = runId;
}

/// <summary>Thrown when two runs cannot be compared.</summary>
public sealed class RunsNotComparableException(string message) : Exception(message);
