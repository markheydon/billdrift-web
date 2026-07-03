using BillDrift.Application.CatalogueReconciliation;
using BillDrift.Domain.CatalogueReconciliation;

namespace BillDrift.Web.Services;

/// <inheritdoc />
public sealed class CatalogueReconciliationApiClient(HttpClient httpClient) : ICatalogueReconciliationApiClient
{
    /// <inheritdoc />
    public async Task<CatalogueReconciliationRun> StartRunAsync(
        CatalogueReconciliationRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/catalogue-reconciliation/runs", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var runId = response.Headers.Location?.Segments.LastOrDefault();
        if (runId is not null && Guid.TryParse(runId, out var id))
        {
            return (await GetRunAsync(id, cancellationToken))!;
        }

        return (await response.Content.ReadFromJsonAsync<CatalogueReconciliationRun>(cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogueRunListItem>> ListRunsAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        var wrapper = await httpClient.GetFromJsonAsync<CatalogueRunListResponse>(
            $"api/catalogue-reconciliation/runs?limit={limit}",
            cancellationToken);

        return wrapper?.Runs ?? [];
    }

    /// <inheritdoc />
    public async Task<CatalogueReconciliationRun?> GetRunAsync(Guid catalogueRunId, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<CatalogueReconciliationRun>(
            $"api/catalogue-reconciliation/runs/{catalogueRunId}",
            cancellationToken);

    /// <inheritdoc />
    public async Task<CatalogueApprovalIngestionResult> IngestApprovalsAsync(
        Guid catalogueRunId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync(
            $"api/catalogue-reconciliation/runs/{catalogueRunId}/ingest-approvals",
            null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CatalogueApprovalIngestionResult>(cancellationToken))!;
    }

    private sealed record CatalogueRunListResponse(IReadOnlyList<CatalogueRunListItem> Runs);
}
