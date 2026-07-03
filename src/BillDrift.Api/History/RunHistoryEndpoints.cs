using BillDrift.Application.Approval;
using BillDrift.Application.History;
using BillDrift.Domain.Common;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;
using Microsoft.AspNetCore.Mvc;

namespace BillDrift.Api.History;

/// <summary>REST endpoints for reconciliation run history.</summary>
public static class RunHistoryEndpoints
{
    /// <summary>Maps run history API endpoints.</summary>
    public static IEndpointRouteBuilder MapRunHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/run-history").WithTags("RunHistory");

        group.MapGet("/", ListRunsAsync);
        group.MapGet("/{runId:guid}", GetRunDetailAsync);
        group.MapPost("/", PersistRunAsync);
        group.MapGet("/{runId:guid}/inputs/{domain}", GetInputAsync);
        group.MapPost("/compare", CompareRunsAsync);
        group.MapPost("/compare/export", ExportComparisonAsync);
        group.MapGet("/trends/drift", GetDriftTrendsAsync);
        group.MapGet("/trends/pricing", GetPricingDriftAsync);
        group.MapGet("/{runId:guid}/audit", GetAuditAsync);

        return endpoints;
    }

    private static async Task<IResult> ListRunsAsync(
        DateOnly? billingPeriodStart,
        DateOnly? billingPeriodEnd,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        RunArchiveStatus? status,
        bool? cleanRunsOnly,
        bool? includeArchived,
        int? pageSize,
        string? continuationToken,
        RunHistoryService service,
        CancellationToken cancellationToken)
    {
        var filter = new RunHistoryListFilter(
            billingPeriodStart,
            billingPeriodEnd,
            fromDate,
            toDate,
            status,
            cleanRunsOnly,
            includeArchived ?? false);

        var effectivePageSize = pageSize is null or <= 0 ? 50 : pageSize.Value;
        var response = await service.ListRunsAsync(filter, effectivePageSize, continuationToken, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetRunDetailAsync(
        Guid runId,
        bool? includeResults,
        bool? includeMatchGroups,
        RunHistoryService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var detail = await service.GetRunDetailAsync(
                RunId.FromGuid(runId),
                includeResults ?? false,
                includeMatchGroups ?? false,
                cancellationToken);
            return Results.Ok(detail);
        }
        catch (RunNotFoundException)
        {
            return Results.NotFound();
        }
        catch (RunArchiveIntegrityException ex)
        {
            return Results.Problem(ex.Message, statusCode: 500);
        }
    }

    private static async Task<IResult> PersistRunAsync(
        [FromBody] PersistRunRequest request,
        RunArchiveService archiveService,
        CancellationToken cancellationToken)
    {
        try
        {
            var record = await archiveService.PersistAsync(request, cancellationToken);
            return Results.Created($"/api/run-history/{record.RunId.Value}", record);
        }
        catch (RunAlreadyArchivedException ex)
        {
            return Results.Conflict(new { title = "Run already archived", status = 409, detail = ex.Message });
        }
    }

    private static async Task<IResult> GetInputAsync(
        Guid runId,
        string domain,
        RunHistoryService service,
        IRunBlobArchiveStore blobStore,
        CancellationToken cancellationToken)
    {
        if (!TryParseDomain(domain, out var domainType))
        {
            return Results.BadRequest("Invalid domain.");
        }

        try
        {
            var json = await blobStore.LoadInputBlobAsync(RunId.FromGuid(runId), domainType, cancellationToken);
            return Results.Content(json, "application/json");
        }
        catch (RunNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> CompareRunsAsync(
        [FromBody] CompareRunsRequest request,
        RunHistoryService service,
        IOperatorContext operatorContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await service.CompareRunsAsync(request, operatorContext.OperatorId, cancellationToken);
            return Results.Ok(report);
        }
        catch (RunNotFoundException ex)
        {
            return Results.NotFound(new { title = "Run not found", status = 404, detail = ex.Message });
        }
        catch (RunsNotComparableException ex)
        {
            return Results.UnprocessableEntity(new { title = "Runs not comparable", status = 422, detail = ex.Message });
        }
    }

    private static async Task<IResult> ExportComparisonAsync(
        [FromBody] CompareRunsRequest request,
        RunHistoryService service,
        IOperatorContext operatorContext,
        CancellationToken cancellationToken)
    {
        var exportRunId = request.LaterRunId;
        var response = await service.ExportComparisonAsync(request, exportRunId, operatorContext.OperatorId, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetDriftTrendsAsync(
        DateTimeOffset fromDate,
        DateTimeOffset toDate,
        int? minOccurrences,
        MismatchType? mismatchType,
        string? customerMexId,
        RunHistoryService service,
        IOperatorContext operatorContext,
        CancellationToken cancellationToken)
    {
        var response = await service.GetDriftTrendsAsync(
            fromDate,
            toDate,
            minOccurrences is null or <= 0 ? 2 : minOccurrences.Value,
            mismatchType,
            customerMexId,
            operatorContext.OperatorId,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetPricingDriftAsync(
        string offerId,
        string skuId,
        Term term,
        BillingFrequency frequency,
        DateTimeOffset fromDate,
        DateTimeOffset toDate,
        RunHistoryService service,
        CancellationToken cancellationToken)
    {
        var key = CommercialKey.Create(OfferId.Create(offerId), SkuId.Create(skuId), term, frequency);
        var response = await service.GetPricingDriftTimelineAsync(key, fromDate, toDate, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetAuditAsync(
        Guid runId,
        RunHistoryService service,
        CancellationToken cancellationToken)
    {
        var response = await service.GetAuditEventsAsync(RunId.FromGuid(runId), cancellationToken);
        return Results.Ok(response);
    }

    private static bool TryParseDomain(string domain, out InputDomainType domainType)
    {
        domainType = default;
        return domain.ToLowerInvariant() switch
        {
            "supplier-cost" => Set(InputDomainType.SupplierCost, out domainType),
            "subscription-truth" => Set(InputDomainType.SubscriptionTruth, out domainType),
            "intended-pricing" => Set(InputDomainType.IntendedPricing, out domainType),
            "stripe-billing" => Set(InputDomainType.StripeBilling, out domainType),
            "product-mappings" => Set(InputDomainType.ProductMappings, out domainType),
            _ => false
        };
    }

    private static bool Set(InputDomainType value, out InputDomainType result)
    {
        result = value;
        return true;
    }
}
