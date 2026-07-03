using BillDrift.Application.Import.Giacom;
using BillDrift.Application.Ingestion;

namespace BillDrift.Api.Imports;

/// <summary>REST endpoints for Giacom billing PDF ingestion uploads.</summary>
public static class GiacomPdfImportEndpoints
{
    /// <summary>Maps Giacom PDF import API endpoints.</summary>
    public static IEndpointRouteBuilder MapGiacomPdfImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/imports/giacom-pdf").WithTags("GiacomPdfImports");

        group.MapPost("/", UploadAsync).DisableAntiforgery();
        group.MapGet("/", ListRunsAsync);
        group.MapGet("/{ingestionId:guid}", GetRunDetailAsync);
        group.MapGet("/{ingestionId:guid}/supplier-cost", GetSupplierCostAsync);

        return endpoints;
    }

    private static async Task<IResult> UploadAsync(
        HttpRequest request,
        IGiacomPdfIngestionService service,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest("PDF file is required.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest("PDF file is required.");
        }

        if (file.Length > GiacomPdfIngestionOptions.MaxFileSizeBytes)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Payload Too Large",
                detail: $"PDF file exceeds maximum allowed size of {GiacomPdfIngestionOptions.MaxFileSizeBytes} bytes.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var run = await service.IngestAndPersistAsync(stream, file.FileName, cancellationToken);

            if (run.Status == IngestionRunStatus.Failed)
            {
                return Results.UnprocessableEntity(run);
            }

            return Results.Ok(run);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<IResult> ListRunsAsync(
        int? take,
        IIngestionRunIndexStore indexStore,
        CancellationToken cancellationToken)
    {
        var effectiveTake = take is null or <= 0 ? 20 : take.Value;
        var runs = await indexStore.ListRecentGiacomPdfAsync(effectiveTake, cancellationToken);
        return Results.Ok(runs);
    }

    private static async Task<IResult> GetRunDetailAsync(
        Guid ingestionId,
        IIngestionRunIndexStore indexStore,
        CancellationToken cancellationToken)
    {
        var run = await indexStore.GetGiacomPdfByIdAsync(ingestionId, cancellationToken);
        return run is null ? Results.NotFound() : Results.Ok(run);
    }

    private static async Task<IResult> GetSupplierCostAsync(
        Guid ingestionId,
        IIngestionBlobStore blobStore,
        CancellationToken cancellationToken)
    {
        var lines = await blobStore.GetSupplierCostLinesAsync(ingestionId, cancellationToken);
        return lines is null ? Results.NotFound() : Results.Ok(lines);
    }
}
