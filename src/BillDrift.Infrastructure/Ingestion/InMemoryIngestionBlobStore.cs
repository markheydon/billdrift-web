using System.Collections.Concurrent;
using BillDrift.Application.Import;
using BillDrift.Application.Ingestion;
using BillDrift.Domain.Billing;
using BillDrift.Domain.CatalogueReconciliation;

namespace BillDrift.Infrastructure.Ingestion;

/// <summary>In-memory blob store for ingestion unit tests.</summary>
public sealed class InMemoryIngestionBlobStore : IIngestionBlobStore
{
    private readonly ConcurrentDictionary<Guid, byte[]> _sources = new();
    private readonly ConcurrentDictionary<Guid, SubscriptionManagementCsvIngestionResult> _results = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<MicrosoftSubscriptionLine>> _subscriptionTruth = new();
    private readonly ConcurrentDictionary<Guid, RetailPricingCsvIngestionResult> _retailResults = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<IntendedPrice>> _resolvedPrices = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<StripeCatalogueProduct>> _stripeCatalogueProducts = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<StripeCataloguePrice>> _stripeCataloguePrices = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<SupplierCostLine>> _supplierCostLines = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<StripeBillingItem>> _stripeBillingItems = new();

    /// <inheritdoc />
    public Task<string> UploadSourceAsync(
        Guid ingestionId,
        byte[] content,
        string? originalFileName,
        CancellationToken cancellationToken = default)
    {
        _ = originalFileName;
        _sources[ingestionId] = content;
        return Task.FromResult($"{ingestionId:D}/source/SubscriptionManagementReport.csv");
    }

    /// <inheritdoc />
    public Task<string> PersistResultAsync(
        Guid ingestionId,
        SubscriptionManagementCsvIngestionResult result,
        string? originalFileName,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken = default)
    {
        _ = originalFileName;
        _ = uploadedAt;
        _results[ingestionId] = result;
        _subscriptionTruth[ingestionId] = result.SubscriptionLines;
        return Task.FromResult($"{ingestionId:D}/result/manifest.json");
    }

    /// <inheritdoc />
    public Task<SubscriptionManagementCsvIngestionResult?> GetIngestionResultAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_results.TryGetValue(ingestionId, out var result) ? result : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<MicrosoftSubscriptionLine>?> GetSubscriptionTruthAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_subscriptionTruth.TryGetValue(ingestionId, out var lines) ? lines : null);

    /// <inheritdoc />
    public Task<string?> UploadManualOverridesAsync(
        Guid ingestionId,
        byte[]? manualOverridesJson,
        CancellationToken cancellationToken = default)
    {
        _ = manualOverridesJson;
        return Task.FromResult<string?>($"{ingestionId:D}/source/manual-overrides.json");
    }

    /// <inheritdoc />
    public Task<string> PersistRetailPricingResultAsync(
        Guid ingestionId,
        RetailPricingCsvIngestionResult result,
        string? originalFileName,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken = default)
    {
        _ = originalFileName;
        _ = uploadedAt;
        _retailResults[ingestionId] = result;
        _resolvedPrices[ingestionId] = result.ResolvedPrices;
        return Task.FromResult($"{ingestionId:D}/result/manifest.json");
    }

    /// <inheritdoc />
    public Task<RetailPricingCsvIngestionResult?> GetRetailPricingResultAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_retailResults.TryGetValue(ingestionId, out var result) ? result : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<IntendedPrice>?> GetResolvedPricesAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_resolvedPrices.TryGetValue(ingestionId, out var prices) ? prices : null);

    /// <inheritdoc />
    public Task PersistStripeCatalogueAsync(
        Guid ingestionId,
        IReadOnlyList<StripeCatalogueProduct> products,
        IReadOnlyList<StripeCataloguePrice> prices,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(prices);
        _stripeCatalogueProducts[ingestionId] = products;
        _stripeCataloguePrices[ingestionId] = prices;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StripeCatalogueProduct>?> GetStripeCatalogueProductsAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_stripeCatalogueProducts.TryGetValue(ingestionId, out var products) ? products : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<StripeCataloguePrice>?> GetStripeCataloguePricesAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_stripeCataloguePrices.TryGetValue(ingestionId, out var prices) ? prices : null);

    /// <inheritdoc />
    public Task<string> PersistSupplierCostLinesAsync(
        Guid ingestionId,
        GiacomPdfIngestionResult result,
        IReadOnlyList<SupplierCostLine> supplierCostLines,
        string? originalFileName,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken = default)
    {
        _ = result;
        _ = originalFileName;
        _ = uploadedAt;
        _supplierCostLines[ingestionId] = supplierCostLines;
        return Task.FromResult($"{ingestionId:D}/result/manifest.json");
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SupplierCostLine>?> GetSupplierCostLinesAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_supplierCostLines.TryGetValue(ingestionId, out var lines) ? lines : null);

    /// <inheritdoc />
    public Task<string> PersistStripeBillingItemsAsync(
        Guid ingestionId,
        StripeCsvIngestionResult result,
        IReadOnlyList<StripeBillingItem> billingItems,
        string? originalFileName,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken = default)
    {
        _ = result;
        _ = originalFileName;
        _ = uploadedAt;
        _stripeBillingItems[ingestionId] = billingItems;
        return Task.FromResult($"{ingestionId:D}/result/manifest.json");
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StripeBillingItem>?> GetStripeBillingItemsAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_stripeBillingItems.TryGetValue(ingestionId, out var items) ? items : null);

    /// <summary>Seeds resolved intended prices without a full retail pricing ingestion result.</summary>
    public void SeedResolvedPrices(Guid ingestionId, IReadOnlyList<IntendedPrice> prices) =>
        _resolvedPrices[ingestionId] = prices;
}
