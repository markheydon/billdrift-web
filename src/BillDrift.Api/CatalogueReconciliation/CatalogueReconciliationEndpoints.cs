using BillDrift.Application.CatalogueReconciliation;
using BillDrift.Domain.CatalogueReconciliation;
using BillDrift.Domain.Mapping;

namespace BillDrift.Api.CatalogueReconciliation;

/// <summary>Minimal API endpoints for catalogue reconciliation runs.</summary>
public static class CatalogueReconciliationEndpoints
{
    /// <summary>Maps catalogue reconciliation routes.</summary>
    public static IEndpointRouteBuilder MapCatalogueReconciliationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalogue-reconciliation");

        group.MapPost("/runs", async (
            CatalogueReconciliationRunRequest request,
            ICatalogueReconciliationService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var run = await service.RunAsync(request, cancellationToken);
                return Results.Created($"/api/catalogue-reconciliation/runs/{run.RunId.Value:D}", new
                {
                    catalogueRunId = run.RunId.Value,
                    executedAt = run.ExecutedAt,
                    summary = run.Summary
                });
            }
            catch (CatalogueReconciliationValidationException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid catalogue reconciliation request");
            }
        });

        group.MapGet("/runs", async (
            int? limit,
            ICatalogueReconciliationService service,
            CancellationToken cancellationToken) =>
        {
            var runs = await service.ListRunsAsync(limit ?? 20, cancellationToken);
            return Results.Ok(new { runs });
        });

        group.MapGet("/runs/{catalogueRunId:guid}", async (
            Guid catalogueRunId,
            ICatalogueReconciliationService service,
            CancellationToken cancellationToken) =>
        {
            var run = await service.GetRunAsync(CatalogueRunId.FromGuid(catalogueRunId), cancellationToken);
            return run is null ? Results.NotFound() : Results.Ok(run);
        });

        group.MapPost("/runs/{catalogueRunId:guid}/ingest-approvals", async (
            Guid catalogueRunId,
            ICatalogueReconciliationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.IngestApprovalsAsync(
                CatalogueRunId.FromGuid(catalogueRunId),
                cancellationToken);

            return Results.Ok(result);
        });

        return app;
    }
}

/// <summary>API request body for triggering a catalogue reconciliation run.</summary>
public sealed record CatalogueReconciliationApiRequest(
    Guid? StripeIngestionRunId,
    Guid? PricingIngestionRunId,
    IReadOnlyList<ProductMapping> ProductMappings,
    IReadOnlyList<StripeCatalogueProduct>? StripeProducts,
    IReadOnlyList<StripeCataloguePrice>? StripePrices,
    CatalogueReconciliationOptions? Options,
    bool IngestToApprovalQueue);
