using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;

namespace BillDrift.Application.History;

/// <summary>Blob archive persistence for reconciliation run snapshots.</summary>
public interface IRunBlobArchiveStore
{
    Task<RunArchiveWriteResult> WriteRunArchiveAsync(
        ReconciliationRun run,
        RunArchiveContext context,
        CancellationToken cancellationToken = default);

    Task<RunResultsSnapshot> LoadResultsSnapshotAsync(RunId runId, CancellationToken cancellationToken = default);

    Task<string> LoadInputBlobAsync(RunId runId, InputDomainType domain, CancellationToken cancellationToken = default);

    Task VerifyManifestIntegrityAsync(RunId runId, CancellationToken cancellationToken = default);

    Task<(string BlobPath, string ContentHash)> ExportComparisonReportAsync(
        RunId runId,
        RunComparisonReport report,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of writing run archive blobs.</summary>
public sealed record RunArchiveWriteResult(
    string ManifestBlobPath,
    IReadOnlyList<InputSnapshotMetadata> InputSnapshots,
    RunSummaryMetrics SummaryMetrics,
    string ResultsContentHash);
