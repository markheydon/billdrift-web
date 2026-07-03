using System.Text.Json;
using BillDrift.Application.Import;
using BillDrift.Application.Import.RetailPricing;
using BillDrift.Application.Ingestion;

namespace BillDrift.Api.Imports;

/// <summary>REST endpoints for retail pricing CSV ingestion uploads.</summary>
public static class RetailPricingImportEndpoints
{
    private static readonly JsonSerializerOptions ManualOverrideJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Maps retail pricing import API endpoints.</summary>
    public static IEndpointRouteBuilder MapRetailPricingImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/imports/retail-pricing").WithTags("RetailPricingImports");

        group.MapPost("/", UploadAsync);
        group.MapGet("/", ListRunsAsync);
        group.MapGet("/{ingestionId:guid}", GetRunDetailAsync);
        group.MapGet("/{ingestionId:guid}/resolved-prices", GetResolvedPricesAsync);

        return endpoints;
    }

    private static async Task<IResult> UploadAsync(
        HttpRequest request,
        IRetailPricingIngestionService service,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest("Multipart form data is required.");
        }

        var form = await request.ReadFormAsync(cancellationToken);

        var catalogueFile = form.Files.GetFile("catalogue");
        if (catalogueFile is null || catalogueFile.Length == 0)
        {
            return Results.BadRequest("Catalogue CSV file is required (form field 'catalogue').");
        }

        if (catalogueFile.Length > RetailPricingCsvIngestionOptions.DefaultMaxFileSizeBytes)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Payload Too Large",
                detail: $"CSV file exceeds maximum allowed size of {RetailPricingCsvIngestionOptions.DefaultMaxFileSizeBytes} bytes.");
        }

        IReadOnlyList<ManualPriceOverrideRequest>? manualOverrides = null;
        var overridesFile = form.Files.GetFile("manualOverrides");
        if (overridesFile is not null && overridesFile.Length > 0)
        {
            await using var overridesStream = overridesFile.OpenReadStream();
            manualOverrides = await JsonSerializer.DeserializeAsync<List<ManualPriceOverrideRequest>>(
                overridesStream,
                ManualOverrideJsonOptions,
                cancellationToken);

            if (manualOverrides is null)
            {
                return Results.BadRequest("manualOverrides must be a JSON array.");
            }
        }

        try
        {
            await using var stream = catalogueFile.OpenReadStream();
            var run = await service.IngestAndPersistAsync(stream, catalogueFile.FileName, manualOverrides, cancellationToken);
            return Results.Ok(run);
        }
        catch (RetailPricingUploadTooLargeException ex)
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
        var runs = await indexStore.ListRecentRetailPricingAsync(effectiveTake, cancellationToken);
        return Results.Ok(runs);
    }

    private static async Task<IResult> GetRunDetailAsync(
        Guid ingestionId,
        IIngestionRunIndexStore indexStore,
        CancellationToken cancellationToken)
    {
        var run = await indexStore.GetRetailPricingByIdAsync(ingestionId, cancellationToken);
        return run is null ? Results.NotFound() : Results.Ok(run);
    }

    private static async Task<IResult> GetResolvedPricesAsync(
        Guid ingestionId,
        IIngestionBlobStore blobStore,
        CancellationToken cancellationToken)
    {
        var prices = await blobStore.GetResolvedPricesAsync(ingestionId, cancellationToken);
        return prices is null ? Results.NotFound() : Results.Ok(prices);
    }
}
