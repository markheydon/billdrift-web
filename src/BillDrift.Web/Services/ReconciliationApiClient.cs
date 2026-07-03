using BillDrift.Application.Reconciliation;

namespace BillDrift.Web.Services;

/// <summary>Typed HTTP client for reconciliation orchestration endpoints.</summary>
public interface IReconciliationApiClient
{
    Task<ReconciliationRunResponse> StartRunAsync(StartReconciliationRunRequest request, CancellationToken cancellationToken = default);

    Task<ReconciliationRunResponse?> GetRunAsync(Guid runId, bool includeResults = true, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MarginLineViewModel>> GetMarginLinesAsync(Guid runId, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class ReconciliationApiClient(HttpClient httpClient) : IReconciliationApiClient
{
    /// <inheritdoc />
    public async Task<ReconciliationRunResponse> StartRunAsync(
        StartReconciliationRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/reconciliation/runs", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReconciliationRunResponse>(cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<ReconciliationRunResponse?> GetRunAsync(
        Guid runId,
        bool includeResults = true,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"api/reconciliation/runs/{runId}?includeResults={includeResults}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReconciliationRunResponse>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MarginLineViewModel>> GetMarginLinesAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"api/reconciliation/runs/{runId}/margin", cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<MarginLineViewModel>>(cancellationToken))!;
    }
}
