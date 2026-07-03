using BillDrift.Application.CatalogueReconciliation;
using BillDrift.Domain.CatalogueReconciliation;

namespace BillDrift.Web.Services;

/// <summary>HTTP client for catalogue reconciliation API endpoints.</summary>
public interface ICatalogueReconciliationApiClient
{
    Task<CatalogueReconciliationRun> StartRunAsync(CatalogueReconciliationRunRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogueRunListItem>> ListRunsAsync(int limit = 20, CancellationToken cancellationToken = default);

    Task<CatalogueReconciliationRun?> GetRunAsync(Guid catalogueRunId, CancellationToken cancellationToken = default);

    Task<CatalogueApprovalIngestionResult> IngestApprovalsAsync(Guid catalogueRunId, CancellationToken cancellationToken = default);
}
