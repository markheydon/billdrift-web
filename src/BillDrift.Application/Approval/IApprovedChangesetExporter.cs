using BillDrift.Domain.Approval;

namespace BillDrift.Application.Approval;

/// <summary>Exports approved changesets to durable blob storage.</summary>
public interface IApprovedChangesetExporter
{
    /// <summary>Persists the changeset and returns metadata including blob URI.</summary>
    Task<ApprovedChangeset> ExportAsync(ApprovedChangeset changeset, CancellationToken cancellationToken = default);

    /// <summary>Downloads a previously exported changeset blob as JSON.</summary>
    Task<string> DownloadAsync(string blobPath, CancellationToken cancellationToken = default);
}
