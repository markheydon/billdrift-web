using BillDrift.Application.History;
using BillDrift.Application.Reconciliation;
using BillDrift.Domain.Reconciliation;
using Microsoft.AspNetCore.Mvc;

namespace BillDrift.Api.Reconciliation;

/// <summary>REST endpoints for reconciliation orchestration.</summary>
public static class ReconciliationEndpoints
{
    /// <summary>Maps reconciliation orchestration API endpoints.</summary>
    public static IEndpointRouteBuilder MapReconciliationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reconciliation").WithTags("Reconciliation");

        group.MapPost("/runs", StartRunAsync);
        group.MapGet("/runs/{runId:guid}", GetRunAsync);
        group.MapGet("/runs/{runId:guid}/margin", GetMarginAsync);

        return endpoints;
    }

    private static async Task<IResult> StartRunAsync(
        [FromBody] StartReconciliationRunRequest request,
        ReconciliationOrchestrationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await service.ExecuteAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (ReconciliationOrchestrationException ex)
        {
            return Results.BadRequest(new { title = "Invalid request", detail = ex.Message });
        }
        catch (RunAlreadyArchivedException ex)
        {
            return Results.Conflict(new { title = "Run already archived", detail = ex.Message });
        }
    }

    private static async Task<IResult> GetRunAsync(
        Guid runId,
        bool? includeResults,
        ReconciliationOrchestrationService service,
        CancellationToken cancellationToken)
    {
        var response = await service.GetRunAsync(
            RunId.FromGuid(runId),
            includeResults ?? true,
            cancellationToken);

        return response is null ? Results.NotFound() : Results.Ok(response);
    }

    private static async Task<IResult> GetMarginAsync(
        Guid runId,
        ReconciliationOrchestrationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var lines = await service.GetMarginLinesAsync(RunId.FromGuid(runId), cancellationToken);
            return Results.Ok(lines);
        }
        catch (RunNotFoundException)
        {
            return Results.NotFound();
        }
    }
}
