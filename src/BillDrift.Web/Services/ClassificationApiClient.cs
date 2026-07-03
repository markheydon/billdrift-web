using BillDrift.Domain.Classification;

namespace BillDrift.Web.Services;

/// <inheritdoc />
public sealed class ClassificationApiClient(HttpClient httpClient) : IClassificationApiClient
{
    /// <inheritdoc />
    public async Task<ItemClassification?> GetClassificationAsync(string stableKey, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"api/classifications/{Uri.EscapeDataString(stableKey)}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ItemClassification>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ItemClassification> ApplyOverrideAsync(
        string stableKey,
        ApplyClassificationOverrideRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"api/classifications/{Uri.EscapeDataString(stableKey)}/override",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ItemClassification>(cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task ClearOverrideAsync(
        string stableKey,
        ClearClassificationOverrideRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            $"api/classifications/{Uri.EscapeDataString(stableKey)}/override")
        {
            Content = JsonContent.Create(request)
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetInternalMexIdsAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<List<string>>("api/classification-config/internal-mex-ids", cancellationToken) ?? [];

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> UpdateInternalMexIdsAsync(
        IReadOnlyList<string> mexIds,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync("api/classification-config/internal-mex-ids", mexIds, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<string>>(cancellationToken) ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductCategoryRule>> GetProductCategoryRulesAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<List<ProductCategoryRule>>("api/classification-config/product-category-rules", cancellationToken) ?? [];

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductCategoryRule>> UpdateProductCategoryRulesAsync(
        IReadOnlyList<ProductCategoryRule> rules,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync("api/classification-config/product-category-rules", rules, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ProductCategoryRule>>(cancellationToken) ?? [];
    }
}
