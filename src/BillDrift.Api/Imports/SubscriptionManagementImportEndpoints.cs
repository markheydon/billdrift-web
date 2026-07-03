using BillDrift.Application.Import;
using BillDrift.Application.Import.SubscriptionManagement;
using BillDrift.Application.Ingestion;

namespace BillDrift.Api.Imports;

/// <summary>REST endpoints for Subscription Management CSV ingestion uploads.</summary>
public static class SubscriptionManagementImportEndpoints
{
    /// <summary>Maps Subscription Management import API endpoints.</summary>
    public static IEndpointRouteBuilder MapSubscriptionManagementImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/imports/subscription-management").WithTags("SubscriptionManagementImports");

        group.MapPost("/", UploadAsync).DisableAntiforgery();
        group.MapGet("/", ListRunsAsync);
        group.MapGet("/{ingestionId:guid}", GetRunDetailAsync);
        group.MapGet("/{ingestionId:guid}/subscription-truth", GetSubscriptionTruthAsync);

        return endpoints;
    }

    private static async Task<IResult> UploadAsync(
        IFormFile file,
        ISubscriptionManagementIngestionService service,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Results.BadRequest("CSV file is required.");
        }

        if (file.Length > SubscriptionManagementCsvIngestionOptions.DefaultMaxFileSizeBytes)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Payload Too Large",
                detail: $"CSV file exceeds maximum allowed size of {SubscriptionManagementCsvIngestionOptions.DefaultMaxFileSizeBytes} bytes.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var run = await service.IngestAndPersistAsync(stream, file.FileName, cancellationToken);
            return Results.Ok(run);
        }
        catch (SubscriptionManagementUploadTooLargeException ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Payload Too Large",
                detail: ex.Message);
        }
    }

    private static async Task<IResult> ListRunsAsync(
        int? take,
        IIngestionRunIndexStore indexStore,
        CancellationToken cancellationToken)
    {
        var effectiveTake = take is null or <= 0 ? 20 : take.Value;
        var runs = await indexStore.ListRecentAsync(effectiveTake, cancellationToken);
        return Results.Ok(runs);
    }

    private static async Task<IResult> GetRunDetailAsync(
        Guid ingestionId,
        IIngestionRunIndexStore indexStore,
        CancellationToken cancellationToken)
    {
        var run = await indexStore.GetByIdAsync(ingestionId, cancellationToken);
        return run is null ? Results.NotFound() : Results.Ok(run);
    }

    private static async Task<IResult> GetSubscriptionTruthAsync(
        Guid ingestionId,
        IIngestionBlobStore blobStore,
        CancellationToken cancellationToken)
    {
        var lines = await blobStore.GetSubscriptionTruthAsync(ingestionId, cancellationToken);
        return lines is null ? Results.NotFound() : Results.Ok(lines);
    }
}
