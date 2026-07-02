using BillDrift.Application.Classification;
using BillDrift.Domain.Classification;
using Microsoft.AspNetCore.Mvc;

namespace BillDrift.Api.Classification;

/// <summary>
/// Minimal REST endpoints for classification overrides and operator configuration.
/// </summary>
public static class ClassificationEndpoints
{
    /// <summary>
    /// Maps classification API endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapClassificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api");

        group.MapGet("/classifications/{stableKey}", GetClassificationAsync);
        group.MapPut("/classifications/{stableKey}/override", ApplyOverrideAsync);
        group.MapDelete("/classifications/{stableKey}/override", ClearOverrideAsync);
        group.MapGet("/classification-config/internal-mex-ids", GetInternalMexIdsAsync);
        group.MapPut("/classification-config/internal-mex-ids", UpdateInternalMexIdsAsync);
        group.MapGet("/classification-config/product-category-rules", GetProductCategoryRulesAsync);
        group.MapPut("/classification-config/product-category-rules", UpdateProductCategoryRulesAsync);

        return endpoints;
    }

    private static async Task<IResult> GetClassificationAsync(
        string stableKey,
        ClassificationService service,
        CancellationToken cancellationToken)
    {
        var classification = await service.GetClassificationAsync(stableKey, cancellationToken);
        return classification is null
            ? Results.NotFound()
            : Results.Ok(classification);
    }

    private static async Task<IResult> ApplyOverrideAsync(
        string stableKey,
        [FromBody] ApplyOverrideRequest request,
        ClassificationService service,
        CancellationToken cancellationToken)
    {
        var itemRef = ReconciliationItemRef.Create(
            request.Kind,
            stableKey,
            Domain.Common.MexId.Create(request.CustomerMexId));

        var classificationOverride = new ClassificationOverride(
            itemRef,
            request.Classification,
            request.Notes,
            request.OperatorId,
            DateTimeOffset.UtcNow);

        var result = await service.ApplyOverrideAsync(classificationOverride, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ClearOverrideAsync(
        string stableKey,
        [FromBody] ClearOverrideRequest request,
        ClassificationService service,
        CancellationToken cancellationToken)
    {
        var itemRef = ReconciliationItemRef.Create(
            request.Kind,
            stableKey,
            Domain.Common.MexId.Create(request.CustomerMexId));

        await service.ClearOverrideAsync(itemRef, request.OperatorId, cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetInternalMexIdsAsync(
        ClassificationService service,
        CancellationToken cancellationToken)
    {
        var config = await service.GetConfigurationAsync(cancellationToken);
        return Results.Ok(config.InternalMexIds.Select(id => id.Value).ToList());
    }

    private static async Task<IResult> UpdateInternalMexIdsAsync(
        [FromBody] IReadOnlyList<string> mexIds,
        ClassificationService service,
        CancellationToken cancellationToken)
    {
        var config = await service.GetConfigurationAsync(cancellationToken);
        var updated = config with
        {
            InternalMexIds = mexIds.Select(Domain.Common.MexId.Create).ToList()
        };
        await service.UpdateConfigurationAsync(updated, cancellationToken);
        return Results.Ok(updated.InternalMexIds.Select(id => id.Value));
    }

    private static async Task<IResult> GetProductCategoryRulesAsync(
        ClassificationService service,
        CancellationToken cancellationToken)
    {
        var config = await service.GetConfigurationAsync(cancellationToken);
        return Results.Ok(config.ProductCategoryRules);
    }

    private static async Task<IResult> UpdateProductCategoryRulesAsync(
        [FromBody] IReadOnlyList<ProductCategoryRule> rules,
        ClassificationService service,
        CancellationToken cancellationToken)
    {
        var config = await service.GetConfigurationAsync(cancellationToken);
        var updated = config with { ProductCategoryRules = rules };
        await service.UpdateConfigurationAsync(updated, cancellationToken);
        return Results.Ok(updated.ProductCategoryRules);
    }

    private sealed record ApplyOverrideRequest(
        ReconciliationItemKind Kind,
        string CustomerMexId,
        ReconciliationItemClassification Classification,
        string Notes,
        string OperatorId);

    private sealed record ClearOverrideRequest(
        ReconciliationItemKind Kind,
        string CustomerMexId,
        string OperatorId);
}
