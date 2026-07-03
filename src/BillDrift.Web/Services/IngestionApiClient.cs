using System.Net;
using System.Text.Json;
using BillDrift.Application.Ingestion;

namespace BillDrift.Web.Services;

/// <summary>Typed HTTP client for all import API endpoints.</summary>
public interface IIngestionApiClient
{
    Task<SubscriptionManagementIngestionRun> UploadSubscriptionManagementAsync(Stream content, string fileName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionManagementIngestionRun>> ListSubscriptionManagementRunsAsync(int take = 20, CancellationToken cancellationToken = default);

    Task<RetailPricingIngestionRun> UploadRetailPricingAsync(Stream catalogue, string fileName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RetailPricingIngestionRun>> ListRetailPricingRunsAsync(int take = 20, CancellationToken cancellationToken = default);

    Task<GiacomPdfIngestionRun> UploadGiacomPdfAsync(Stream content, string fileName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GiacomPdfIngestionRun>> ListGiacomPdfRunsAsync(int take = 20, CancellationToken cancellationToken = default);

    Task<StripeCsvIngestionRun> UploadStripeCsvAsync(
        Stream subscriptions,
        string subscriptionsFileName,
        Stream? products = null,
        string? productsFileName = null,
        Stream? prices = null,
        string? pricesFileName = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StripeCsvIngestionRun>> ListStripeCsvRunsAsync(int take = 20, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class IngestionApiClient(HttpClient httpClient) : IIngestionApiClient
{
    /// <inheritdoc />
    public Task<SubscriptionManagementIngestionRun> UploadSubscriptionManagementAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default) =>
        UploadAsync<SubscriptionManagementIngestionRun>(
            "api/imports/subscription-management",
            async form => form.Add(await CreateFileContentAsync(content, fileName, cancellationToken), "file", fileName),
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SubscriptionManagementIngestionRun>> ListSubscriptionManagementRunsAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"api/imports/subscription-management?take={take}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<SubscriptionManagementIngestionRun>>(cancellationToken))!;
    }

    /// <inheritdoc />
    public Task<RetailPricingIngestionRun> UploadRetailPricingAsync(
        Stream catalogue,
        string fileName,
        CancellationToken cancellationToken = default) =>
        UploadAsync<RetailPricingIngestionRun>(
            "api/imports/retail-pricing",
            async form => form.Add(await CreateFileContentAsync(catalogue, fileName, cancellationToken), "catalogue", fileName),
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RetailPricingIngestionRun>> ListRetailPricingRunsAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"api/imports/retail-pricing?take={take}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<RetailPricingIngestionRun>>(cancellationToken))!;
    }

    /// <inheritdoc />
    public Task<GiacomPdfIngestionRun> UploadGiacomPdfAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default) =>
        UploadAsync<GiacomPdfIngestionRun>(
            "api/imports/giacom-pdf",
            async form => form.Add(await CreateFileContentAsync(content, fileName, cancellationToken), "file", fileName),
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<GiacomPdfIngestionRun>> ListGiacomPdfRunsAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"api/imports/giacom-pdf?take={take}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<GiacomPdfIngestionRun>>(cancellationToken))!;
    }

    /// <inheritdoc />
    public Task<StripeCsvIngestionRun> UploadStripeCsvAsync(
        Stream subscriptions,
        string subscriptionsFileName,
        Stream? products = null,
        string? productsFileName = null,
        Stream? prices = null,
        string? pricesFileName = null,
        CancellationToken cancellationToken = default) =>
        UploadAsync<StripeCsvIngestionRun>(
            "api/imports/stripe-csv",
            async form =>
            {
                form.Add(await CreateFileContentAsync(subscriptions, subscriptionsFileName, cancellationToken), "subscriptions", subscriptionsFileName);

                if (products is not null)
                {
                    form.Add(await CreateFileContentAsync(products, productsFileName ?? "products.csv", cancellationToken), "products", productsFileName ?? "products.csv");
                }

                if (prices is not null)
                {
                    form.Add(await CreateFileContentAsync(prices, pricesFileName ?? "prices.csv", cancellationToken), "prices", pricesFileName ?? "prices.csv");
                }
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<StripeCsvIngestionRun>> ListStripeCsvRunsAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"api/imports/stripe-csv?take={take}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<StripeCsvIngestionRun>>(cancellationToken))!;
    }

    private async Task<TRun> UploadAsync<TRun>(
        string requestUri,
        Func<MultipartFormDataContent, Task> configureForm,
        CancellationToken cancellationToken)
        where TRun : class
    {
        using var form = new MultipartFormDataContent();
        await configureForm(form);
        var response = await httpClient.PostAsync(requestUri, form, cancellationToken);
        return await ReadUploadResponseAsync<TRun>(response, cancellationToken);
    }

    private static async Task<TRun> ReadUploadResponseAsync<TRun>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where TRun : class
    {
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<TRun>(cancellationToken))!;
        }

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (TryDeserializeRun<TRun>(body, out var run))
            {
                return run!;
            }

            throw new HttpRequestException(
                ReadErrorDetailFromBody(body)
                ?? "Upload could not be processed.");
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            ReadErrorDetailFromBody(errorBody)
            ?? $"Upload failed with status {(int)response.StatusCode} ({response.StatusCode}).");
    }

    private static bool TryDeserializeRun<TRun>(string body, out TRun? run)
        where TRun : class
    {
        run = null;
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("ingestionId", out _))
            {
                return false;
            }

            run = JsonSerializer.Deserialize<TRun>(body);
            return run is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadErrorDetailFromBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("detail", out var detailElement))
            {
                var detail = detailElement.GetString();
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    return detail;
                }
            }

            if (document.RootElement.TryGetProperty("title", out var titleElement))
            {
                return titleElement.GetString();
            }
        }
        catch (JsonException)
        {
            return body.Trim();
        }

        return body.Trim();
    }

    private static async Task<ByteArrayContent> CreateFileContentAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var fileContent = new ByteArrayContent(buffer.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "text/csv");
        return fileContent;
    }
}
