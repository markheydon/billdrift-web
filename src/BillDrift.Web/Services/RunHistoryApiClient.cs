using BillDrift.Application.History;
using BillDrift.Domain.Common;
using BillDrift.Domain.History;

namespace BillDrift.Web.Services;

/// <summary>HTTP client for run history API endpoints.</summary>
public interface IRunHistoryApiClient
{
    Task<RunHistoryListResponse> ListRunsAsync(RunHistoryListFilter filter, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<RunDetailViewModel> GetRunDetailAsync(Guid runId, bool includeResults = false, CancellationToken cancellationToken = default);

    Task<RunComparisonReport> CompareRunsAsync(CompareRunsRequest request, CancellationToken cancellationToken = default);

    Task<DriftTrendsResponse> GetDriftTrendsAsync(DateTimeOffset fromDate, DateTimeOffset toDate, int minOccurrences = 2, CancellationToken cancellationToken = default);

    Task<PricingDriftTimelineResponse> GetPricingDriftTimelineAsync(CommercialKey key, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken = default);

    Task<RunAuditResponse> GetAuditEventsAsync(Guid runId, CancellationToken cancellationToken = default);
}

/// <summary>Typed HTTP client implementation for run history API.</summary>
public sealed class RunHistoryApiClient(HttpClient httpClient) : IRunHistoryApiClient
{
    /// <inheritdoc />
    public async Task<RunHistoryListResponse> ListRunsAsync(
        RunHistoryListFilter filter,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"pageSize={pageSize}" };
        if (filter.BillingPeriodStart is not null)
        {
            query.Add($"billingPeriodStart={filter.BillingPeriodStart.Value:O}");
        }

        if (filter.BillingPeriodEnd is not null)
        {
            query.Add($"billingPeriodEnd={filter.BillingPeriodEnd.Value:O}");
        }

        if (filter.Status is not null)
        {
            query.Add($"status={filter.Status}");
        }

        if (filter.CleanRunsOnly == true)
        {
            query.Add("cleanRunsOnly=true");
        }

        if (filter.IncludeArchived)
        {
            query.Add("includeArchived=true");
        }

        var url = $"/api/run-history?{string.Join('&', query)}";
        return await httpClient.GetFromJsonAsync<RunHistoryListResponse>(url, cancellationToken)
            ?? new RunHistoryListResponse([], null);
    }

    /// <inheritdoc />
    public async Task<RunDetailViewModel> GetRunDetailAsync(
        Guid runId,
        bool includeResults = false,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/run-history/{runId}?includeResults={includeResults.ToString().ToLowerInvariant()}";
        return await httpClient.GetFromJsonAsync<RunDetailViewModel>(url, cancellationToken)
            ?? throw new InvalidOperationException("Run detail not found.");
    }

    /// <inheritdoc />
    public async Task<RunComparisonReport> CompareRunsAsync(
        CompareRunsRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/run-history/compare", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RunComparisonReport>(cancellationToken)
            ?? throw new InvalidOperationException("Comparison failed.");
    }

    /// <inheritdoc />
    public async Task<DriftTrendsResponse> GetDriftTrendsAsync(
        DateTimeOffset fromDate,
        DateTimeOffset toDate,
        int minOccurrences = 2,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/run-history/trends/drift?fromDate={Uri.EscapeDataString(fromDate.ToString("O"))}&toDate={Uri.EscapeDataString(toDate.ToString("O"))}&minOccurrences={minOccurrences}";
        return await httpClient.GetFromJsonAsync<DriftTrendsResponse>(url, cancellationToken)
            ?? new DriftTrendsResponse([], fromDate, toDate);
    }

    /// <inheritdoc />
    public async Task<PricingDriftTimelineResponse> GetPricingDriftTimelineAsync(
        CommercialKey key,
        DateTimeOffset fromDate,
        DateTimeOffset toDate,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/run-history/trends/pricing?offerId={Uri.EscapeDataString(key.OfferId.Value)}&skuId={Uri.EscapeDataString(key.SkuId.Value)}&term={key.Term}&frequency={key.Frequency}&fromDate={Uri.EscapeDataString(fromDate.ToString("O"))}&toDate={Uri.EscapeDataString(toDate.ToString("O"))}";
        return await httpClient.GetFromJsonAsync<PricingDriftTimelineResponse>(url, cancellationToken)
            ?? new PricingDriftTimelineResponse(key, []);
    }

    /// <inheritdoc />
    public async Task<RunAuditResponse> GetAuditEventsAsync(Guid runId, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<RunAuditResponse>($"/api/run-history/{runId}/audit", cancellationToken)
        ?? new RunAuditResponse([]);
}
