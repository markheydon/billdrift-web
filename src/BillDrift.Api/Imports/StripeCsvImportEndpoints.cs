using BillDrift.Application.Import;
using BillDrift.Application.Import.Stripe;
using BillDrift.Application.Ingestion;
using BillDrift.Domain.CatalogueReconciliation;

namespace BillDrift.Api.Imports;

/// <summary>REST endpoints for Stripe CSV ingestion uploads.</summary>
public static class StripeCsvImportEndpoints
{
    /// <summary>Maps Stripe CSV import API endpoints.</summary>
    public static IEndpointRouteBuilder MapStripeCsvImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/imports/stripe-csv").WithTags("StripeCsvImports");

        group.MapPost("/", UploadAsync).DisableAntiforgery();
        group.MapGet("/", ListRunsAsync);
        group.MapGet("/{ingestionId:guid}", GetRunDetailAsync);
        group.MapGet("/{ingestionId:guid}/billing", GetBillingAsync);
        group.MapGet("/{ingestionId:guid}/catalogue", GetCatalogueAsync);

        return endpoints;
    }

    private static async Task<IResult> UploadAsync(
        HttpRequest request,
        IStripeCsvIngestionService service,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest("Multipart form data is required.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var subscriptions = form.Files.GetFile("subscriptions");
        if (subscriptions is null || subscriptions.Length == 0)
        {
            return Results.BadRequest("subscriptions.csv is required (form field 'subscriptions').");
        }

        var options = new StripeCsvIngestionOptions();

        var subscriptionsTooLarge = TooLarge(subscriptions, "subscriptions.csv", options.MaxFileSizeBytes);
        if (subscriptionsTooLarge is not null)
        {
            return subscriptionsTooLarge;
        }

        var products = form.Files.GetFile("products");
        var prices = form.Files.GetFile("prices");

        var productsTooLarge = TooLarge(products, "products.csv", options.MaxFileSizeBytes);
        if (productsTooLarge is not null)
        {
            return productsTooLarge;
        }

        var pricesTooLarge = TooLarge(prices, "prices.csv", options.MaxFileSizeBytes);
        if (pricesTooLarge is not null)
        {
            return pricesTooLarge;
        }

        try
        {
            var files = new StripeCsvUploadFiles(
                subscriptions.OpenReadStream(),
                subscriptions.FileName,
                products?.OpenReadStream(),
                products?.FileName,
                prices?.OpenReadStream(),
                prices?.FileName);

            var run = await service.IngestAndPersistAsync(files, cancellationToken);

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

    private static IResult? TooLarge(IFormFile? file, string displayName, long maxFileSizeBytes)
    {
        if (file is null || file.Length <= maxFileSizeBytes)
        {
            return null;
        }

        return Results.Problem(
            statusCode: StatusCodes.Status413PayloadTooLarge,
            title: "Payload Too Large",
            detail: $"{displayName} exceeds maximum allowed size of {maxFileSizeBytes} bytes.");
    }

    private static async Task<IResult> ListRunsAsync(
        int? take,
        IIngestionRunIndexStore indexStore,
        CancellationToken cancellationToken)
    {
        var effectiveTake = take is null or <= 0 ? 20 : take.Value;
        var runs = await indexStore.ListRecentStripeCsvAsync(effectiveTake, cancellationToken);
        return Results.Ok(runs);
    }

    private static async Task<IResult> GetRunDetailAsync(
        Guid ingestionId,
        IIngestionRunIndexStore indexStore,
        CancellationToken cancellationToken)
    {
        var run = await indexStore.GetStripeCsvByIdAsync(ingestionId, cancellationToken);
        return run is null ? Results.NotFound() : Results.Ok(run);
    }

    private static async Task<IResult> GetBillingAsync(
        Guid ingestionId,
        IIngestionBlobStore blobStore,
        CancellationToken cancellationToken)
    {
        var items = await blobStore.GetStripeBillingItemsAsync(ingestionId, cancellationToken);
        return items is null ? Results.NotFound() : Results.Ok(items);
    }

    private static async Task<IResult> GetCatalogueAsync(
        Guid ingestionId,
        IIngestionBlobStore blobStore,
        CancellationToken cancellationToken)
    {
        var products = await blobStore.GetStripeCatalogueProductsAsync(ingestionId, cancellationToken);
        var prices = await blobStore.GetStripeCataloguePricesAsync(ingestionId, cancellationToken);

        if (products is null && prices is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new StripeCatalogueSnapshot(
            products ?? Array.Empty<StripeCatalogueProduct>(),
            prices ?? Array.Empty<StripeCataloguePrice>()));
    }

    private sealed record StripeCatalogueSnapshot(
        IReadOnlyList<StripeCatalogueProduct> Products,
        IReadOnlyList<StripeCataloguePrice> Prices);
}
