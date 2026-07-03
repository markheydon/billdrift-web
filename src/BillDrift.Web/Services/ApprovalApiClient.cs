using BillDrift.Application.Approval;
using BillDrift.Domain.Approval;

namespace BillDrift.Web.Services;

/// <summary>Typed HTTP client for approval API endpoints.</summary>
public interface IApprovalApiClient
{
    Task<ApprovalIngestionResult> IngestAsync(ApprovalIngestionRequest request, CancellationToken cancellationToken = default);

    Task<ApprovalQueueViewModel> GetQueueAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<ApprovalDecision> ApproveAsync(Guid runId, Guid proposalId, bool acknowledgeStale = false, CancellationToken cancellationToken = default);

    Task<ApprovalDecision> RejectAsync(Guid runId, Guid proposalId, string reason, CancellationToken cancellationToken = default);

    Task<object> ExportAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalAuditEvent>> GetAuditAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<ApprovalIngestionResult> IngestFromRunAsync(Guid runId, bool includeInvestigationItems = true, CancellationToken cancellationToken = default);

    Task<BulkApprovePreview> PreviewBulkApproveAsync(
        Guid runId,
        string customerMexId,
        IReadOnlyList<Guid> proposalIds,
        CancellationToken cancellationToken = default);

    Task<BulkApproveResult> BulkApproveAsync(
        Guid runId,
        string confirmationToken,
        string customerMexId,
        IReadOnlyList<Guid> proposalIds,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class ApprovalApiClient(HttpClient httpClient) : IApprovalApiClient
{
    // Development-only operator hint. The API trusts this header only when running in the
    // Development environment; in every other environment the operator is resolved from the
    // authenticated principal server-side, so this value cannot grant approval rights.
    private const string OperatorHeader = "X-Operator-Id";

    /// <inheritdoc />
    public async Task<ApprovalIngestionResult> IngestAsync(
        ApprovalIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/reconciliation/{request.Run.Id.Value}/approvals/ingest")
        {
            Content = JsonContent.Create(request)
        };

        message.Headers.Add(OperatorHeader, "web-operator");
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApprovalIngestionResult>(cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<ApprovalQueueViewModel> GetQueueAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"api/reconciliation/{runId}/approvals");
        message.Headers.Add(OperatorHeader, "web-operator");
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApprovalQueueViewModel>(cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<ApprovalDecision> ApproveAsync(
        Guid runId,
        Guid proposalId,
        bool acknowledgeStale = false,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/reconciliation/{runId}/approvals/{proposalId}/approve")
        {
            Content = JsonContent.Create(new { acknowledgeStale })
        };

        message.Headers.Add(OperatorHeader, "web-operator");
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApprovalDecision>(cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<ApprovalDecision> RejectAsync(
        Guid runId,
        Guid proposalId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/reconciliation/{runId}/approvals/{proposalId}/reject")
        {
            Content = JsonContent.Create(new { reason })
        };

        message.Headers.Add(OperatorHeader, "web-operator");
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApprovalDecision>(cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<object> ExportAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"api/reconciliation/{runId}/approvals/export")
        {
            Content = JsonContent.Create(new { customerMexId = (string?)null })
        };

        message.Headers.Add(OperatorHeader, "web-operator");
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<object>(cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApprovalAuditEvent>> GetAuditAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"api/reconciliation/{runId}/approvals/audit");
        message.Headers.Add(OperatorHeader, "web-operator");
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ApprovalAuditEvent>>(cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<ApprovalIngestionResult> IngestFromRunAsync(
        Guid runId,
        bool includeInvestigationItems = true,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/reconciliation/{runId}/approvals/ingest-from-run")
        {
            Content = JsonContent.Create(new { includeInvestigationItems })
        };

        message.Headers.Add(OperatorHeader, "web-operator");
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApprovalIngestionResult>(cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<BulkApprovePreview> PreviewBulkApproveAsync(
        Guid runId,
        string customerMexId,
        IReadOnlyList<Guid> proposalIds,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/reconciliation/{runId}/approvals/bulk-approve/preview")
        {
            Content = JsonContent.Create(new { customerMexId, proposalIds })
        };

        message.Headers.Add(OperatorHeader, "web-operator");
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<BulkApprovePreviewResponse>(cancellationToken);
        return new BulkApprovePreview(
            payload!.ConfirmationToken,
            payload.Summary.Count,
            payload.Summary.SubscriptionActions,
            payload.Summary.CatalogueActions);
    }

    /// <inheritdoc />
    public async Task<BulkApproveResult> BulkApproveAsync(
        Guid runId,
        string confirmationToken,
        string customerMexId,
        IReadOnlyList<Guid> proposalIds,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/reconciliation/{runId}/approvals/bulk-approve")
        {
            Content = JsonContent.Create(new { confirmationToken, customerMexId, proposalIds })
        };

        message.Headers.Add(OperatorHeader, "web-operator");
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BulkApproveResult>(cancellationToken))!;
    }

    private sealed record BulkApprovePreviewResponse(
        string ConfirmationToken,
        BulkApprovePreviewSummary Summary);

    private sealed record BulkApprovePreviewSummary(
        int Count,
        int SubscriptionActions,
        int CatalogueActions);
}
