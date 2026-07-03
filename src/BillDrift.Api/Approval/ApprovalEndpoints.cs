using BillDrift.Application.Approval;
using BillDrift.Application.History;
using BillDrift.Application.Reconciliation;
using BillDrift.Application.Reconciliation.ExceptionSurfacing;
using BillDrift.Domain.Approval;
using BillDrift.Domain.Reconciliation;
using Microsoft.AspNetCore.Mvc;

namespace BillDrift.Api.Approval;

/// <summary>REST endpoints for the reconciliation approval workflow.</summary>
public static class ApprovalEndpoints
{
    /// <summary>Maps approval API endpoints.</summary>
    public static IEndpointRouteBuilder MapApprovalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reconciliation/{runId:guid}/approvals")
            .WithTags("Approval");

        group.MapPost("/ingest", IngestAsync);
        group.MapPost("/ingest-from-run", IngestFromRunAsync);
        group.MapGet("/", GetQueueAsync);
        group.MapGet("/{proposalId:guid}", GetProposalAsync);
        group.MapPost("/{proposalId:guid}/approve", ApproveAsync);
        group.MapPost("/{proposalId:guid}/reject", RejectAsync);
        group.MapPost("/bulk-approve/preview", PreviewBulkApproveAsync);
        group.MapPost("/bulk-approve", BulkApproveAsync);
        group.MapPost("/export", ExportAsync);
        group.MapGet("/export/{exportId:guid}/download", DownloadExportAsync);
        group.MapGet("/audit", GetAuditAsync);

        return endpoints;
    }

    private static async Task<IResult> IngestAsync(
        Guid runId,
        [FromBody] ApprovalIngestionRequest request,
        ApprovalService service,
        CancellationToken cancellationToken)
    {
        if (request.Run.Id != RunId.FromGuid(runId))
        {
            return Results.BadRequest("Run ID in route must match request.Run.Id.");
        }

        var result = await service.IngestAsync(request, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> IngestFromRunAsync(
        Guid runId,
        [FromBody] IngestApprovalsFromRunRequest? request,
        ApprovalService service,
        RunHistoryService historyService,
        ExceptionSurfacingService surfacing,
        CancellationToken cancellationToken)
    {
        var run = await historyService.LoadArchivedReconciliationRunAsync(RunId.FromGuid(runId), cancellationToken);
        if (run is null)
        {
            return Results.NotFound();
        }

        if (run.ProposedChanges.Count == 0)
        {
            return Results.UnprocessableEntity(new { title = "No proposals", detail = "Run has no proposals to ingest." });
        }

        var exceptions = surfacing.Surface(run);
        var ingestRequest = new ApprovalIngestionRequest(
            run,
            exceptions,
            Classifications: null,
            request?.IncludeInvestigationItems ?? true);

        var result = await service.IngestAsync(ingestRequest, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetQueueAsync(
        Guid runId,
        string? customerMexId,
        bool? includeCatalogue,
        bool? includeInvestigation,
        ApprovalService service,
        CancellationToken cancellationToken)
    {
        var options = new ApprovalQueueOptions(
            customerMexId is null ? null : Domain.Common.MexId.Create(customerMexId),
            includeCatalogue ?? true,
            includeInvestigation ?? true);

        var queue = await service.GetQueueAsync(RunId.FromGuid(runId), options, cancellationToken);
        return Results.Ok(queue);
    }

    private static async Task<IResult> GetProposalAsync(
        Guid runId,
        Guid proposalId,
        ApprovalService service,
        CancellationToken cancellationToken)
    {
        var queue = await service.GetQueueAsync(RunId.FromGuid(runId), cancellationToken: cancellationToken);
        var proposal = queue.CustomerGroups
            .SelectMany(g => g.SubscriptionProposals
                .Concat(g.CatalogueProposals)
                .Concat(g.InvestigationItems))
            .FirstOrDefault(p => p.Id == ApprovalProposalId.FromGuid(proposalId));

        return proposal is null ? Results.NotFound() : Results.Ok(proposal);
    }

    private static async Task<IResult> ApproveAsync(
        Guid runId,
        Guid proposalId,
        [FromBody] ApproveRequest? request,
        ApprovalService service,
        IOperatorContext operatorContext,
        CancellationToken cancellationToken)
    {
        if (!operatorContext.CanApprove)
        {
            return Results.Forbid();
        }

        try
        {
            var decision = await service.ApproveAsync(
                new ApproveProposalCommand(
                    RunId.FromGuid(runId),
                    ApprovalProposalId.FromGuid(proposalId),
                    request?.AcknowledgeStale ?? false),
                cancellationToken);

            return Results.Ok(decision);
        }
        catch (ApprovalValidationException ex)
        {
            return Results.UnprocessableEntity(new { error = ex.Message });
        }
        catch (ApprovalStateException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
        catch (ApprovalNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> RejectAsync(
        Guid runId,
        Guid proposalId,
        [FromBody] RejectRequest? request,
        ApprovalService service,
        IOperatorContext operatorContext,
        CancellationToken cancellationToken)
    {
        if (!operatorContext.CanApprove)
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(request?.Reason))
        {
            return Results.BadRequest(new { error = "Rejection reason is required." });
        }

        try
        {
            var decision = await service.RejectAsync(
                new RejectProposalCommand(
                    RunId.FromGuid(runId),
                    ApprovalProposalId.FromGuid(proposalId),
                    request.Reason),
                cancellationToken);

            return Results.Ok(decision);
        }
        catch (ApprovalStateException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
        catch (ApprovalNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> PreviewBulkApproveAsync(
        Guid runId,
        [FromBody] BulkPreviewRequest request,
        ApprovalService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var preview = await service.PreviewBulkApproveAsync(
                RunId.FromGuid(runId),
                Domain.Common.MexId.Create(request.CustomerMexId),
                request.ProposalIds.Select(ApprovalProposalId.FromGuid).ToList(),
                cancellationToken);

            return Results.Ok(new
            {
                preview.ConfirmationToken,
                summary = new
                {
                    count = preview.Count,
                    subscriptionActions = preview.SubscriptionActions,
                    catalogueActions = preview.CatalogueActions
                }
            });
        }
        catch (ApprovalValidationException ex)
        {
            return Results.UnprocessableEntity(new { error = ex.Message });
        }
    }

    private static async Task<IResult> BulkApproveAsync(
        Guid runId,
        [FromBody] BulkApproveRequest request,
        ApprovalService service,
        IOperatorContext operatorContext,
        CancellationToken cancellationToken)
    {
        if (!operatorContext.CanApprove)
        {
            return Results.Forbid();
        }

        try
        {
            var result = await service.BulkApproveAsync(
                new BulkApproveCommand(
                    RunId.FromGuid(runId),
                    Domain.Common.MexId.Create(request.CustomerMexId),
                    request.ProposalIds.Select(ApprovalProposalId.FromGuid).ToList(),
                    request.ConfirmationToken),
                cancellationToken);

            return Results.Ok(result);
        }
        catch (ApprovalStateException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ExportAsync(
        Guid runId,
        [FromBody] ExportRequest? request,
        ApprovalService service,
        IOperatorContext operatorContext,
        CancellationToken cancellationToken)
    {
        if (!operatorContext.CanApprove)
        {
            return Results.Forbid();
        }

        try
        {
            var changeset = await service.ExportApprovedChangesetAsync(
                new ExportChangesetCommand(
                    RunId.FromGuid(runId),
                    request?.CustomerMexId is null ? null : Domain.Common.MexId.Create(request.CustomerMexId)),
                cancellationToken);

            return Results.Ok(new
            {
                changeset.ExportId,
                changeset.RunId,
                entryCount = changeset.Entries.Count,
                downloadUrl = $"/api/reconciliation/{runId}/approvals/export/{changeset.ExportId}/download"
            });
        }
        catch (ApprovalValidationException ex)
        {
            return Results.UnprocessableEntity(new { error = ex.Message });
        }
    }

    private static async Task<IResult> DownloadExportAsync(
        Guid runId,
        Guid exportId,
        [FromServices] IApprovedChangesetExporter exporter,
        CancellationToken cancellationToken)
    {
        var blobPath = $"{runId}/{exportId}.json";
        var json = await exporter.DownloadAsync(blobPath, cancellationToken);
        return Results.Content(json, "application/json");
    }

    private static async Task<IResult> GetAuditAsync(
        Guid runId,
        Guid? proposalId,
        ApprovalService service,
        CancellationToken cancellationToken)
    {
        var events = await service.GetAuditHistoryAsync(
            RunId.FromGuid(runId),
            proposalId is null ? null : ApprovalProposalId.FromGuid(proposalId.Value),
            cancellationToken);

        return Results.Ok(events);
    }

    private sealed record ApproveRequest(bool AcknowledgeStale);

    private sealed record RejectRequest(string Reason);

    private sealed record BulkPreviewRequest(string CustomerMexId, IReadOnlyList<Guid> ProposalIds);

    private sealed record BulkApproveRequest(
        string ConfirmationToken,
        string CustomerMexId,
        IReadOnlyList<Guid> ProposalIds);

    private sealed record ExportRequest(string? CustomerMexId);
}
