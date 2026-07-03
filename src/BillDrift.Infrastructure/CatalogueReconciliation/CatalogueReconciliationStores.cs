using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using BillDrift.Application.CatalogueReconciliation;
using BillDrift.Domain.CatalogueReconciliation;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.CatalogueReconciliation;

/// <summary>In-memory catalogue reconciliation store for unit tests.</summary>
public sealed class InMemoryCatalogueReconciliationStore : ICatalogueReconciliationStore
{
    private readonly Dictionary<Guid, CatalogueReconciliationRun> _runs = new();

    /// <inheritdoc />
    public Task SaveRunAsync(CatalogueReconciliationRun run, CancellationToken cancellationToken = default)
    {
        _runs[run.RunId.Value] = run;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<CatalogueReconciliationRun?> GetRunAsync(CatalogueRunId runId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_runs.TryGetValue(runId.Value, out var run) ? run : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<CatalogueRunListItem>> ListRunsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var items = _runs.Values
            .OrderByDescending(r => r.ExecutedAt)
            .Take(limit)
            .Select(ToListItem)
            .ToList();

        return Task.FromResult<IReadOnlyList<CatalogueRunListItem>>(items);
    }

    private static CatalogueRunListItem ToListItem(CatalogueReconciliationRun run)
    {
        var byType = run.Summary.ExceptionsByType;
        return new CatalogueRunListItem(
            run.RunId,
            run.ExecutedAt,
            run.Exceptions.Count,
            byType.GetValueOrDefault(CatalogueExceptionType.MissingProduct),
            byType.GetValueOrDefault(CatalogueExceptionType.MissingPrice),
            byType.GetValueOrDefault(CatalogueExceptionType.IncorrectPrice),
            byType.GetValueOrDefault(CatalogueExceptionType.DuplicateProduct) +
            byType.GetValueOrDefault(CatalogueExceptionType.DuplicatePrice),
            run.Summary.ProposedFixesActionable,
            run.Inputs.InputReferences.StripeIngestionRunId,
            run.Inputs.InputReferences.PricingIngestionRunId);
    }
}

/// <summary>Azure Blob + Table implementation using Aspire-injected storage clients.</summary>
public sealed class AzureCatalogueReconciliationStore : ICatalogueReconciliationStore
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(CatalogueReconciliationJsonSerializerContext.Default.Options);

    private readonly BlobContainerClient _containerClient;
    private readonly TableClient _tableClient;
    private bool _containerEnsured;
    private bool _tableEnsured;

    /// <summary>Creates a store using Aspire-injected blob and table clients — no manual connection strings.</summary>
    public AzureCatalogueReconciliationStore(
        BlobServiceClient blobServiceClient,
        TableServiceClient tableServiceClient,
        IOptions<CatalogueReconciliationStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(blobServiceClient);
        ArgumentNullException.ThrowIfNull(tableServiceClient);
        _containerClient = blobServiceClient.GetBlobContainerClient(options.Value.BlobContainerName);
        _tableClient = tableServiceClient.GetTableClient(options.Value.TableName);
    }

    /// <inheritdoc />
    public async Task SaveRunAsync(CatalogueReconciliationRun run, CancellationToken cancellationToken = default)
    {
        await EnsureResourcesAsync(cancellationToken);
        var runId = run.RunId.Value.ToString("D");
        var runPath = $"{runId}/run.json";
        var exceptionsPath = $"{runId}/results/exceptions.json";
        var fixesPath = $"{runId}/results/proposed-fixes.json";
        var summaryPath = $"{runId}/results/summary.json";
        var manifestPath = $"{runId}/manifest.json";

        await UploadJsonAsync(runPath, run, cancellationToken);
        await UploadJsonAsync(exceptionsPath, run.Exceptions, cancellationToken);
        await UploadJsonAsync(fixesPath, run.ProposedFixes, cancellationToken);
        await UploadJsonAsync(summaryPath, run.Summary, cancellationToken);

        var manifest = new CatalogueRunManifestDocument(
            run.RunId.Value,
            DateTimeOffset.UtcNow,
            exceptionsPath,
            fixesPath,
            summaryPath);

        await UploadJsonAsync(manifestPath, manifest, cancellationToken);

        var entity = new CatalogueRunIndexEntity
        {
            PartitionKey = "catalogue",
            RowKey = runId,
            ExecutedAt = run.ExecutedAt,
            StripeIngestionRunId = run.Inputs.InputReferences.StripeIngestionRunId?.ToString("D"),
            PricingIngestionRunId = run.Inputs.InputReferences.PricingIngestionRunId?.ToString("D"),
            TotalExceptions = run.Exceptions.Count,
            MissingProductCount = run.Summary.ExceptionsByType.GetValueOrDefault(CatalogueExceptionType.MissingProduct),
            MissingPriceCount = run.Summary.ExceptionsByType.GetValueOrDefault(CatalogueExceptionType.MissingPrice),
            IncorrectPriceCount = run.Summary.ExceptionsByType.GetValueOrDefault(CatalogueExceptionType.IncorrectPrice),
            DuplicateCount =
                run.Summary.ExceptionsByType.GetValueOrDefault(CatalogueExceptionType.DuplicateProduct) +
                run.Summary.ExceptionsByType.GetValueOrDefault(CatalogueExceptionType.DuplicatePrice),
            ActionableFixCount = run.Summary.ProposedFixesActionable,
            BlobManifestPath = manifestPath,
            Status = "Completed"
        };

        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CatalogueReconciliationRun?> GetRunAsync(CatalogueRunId runId, CancellationToken cancellationToken = default)
    {
        await EnsureResourcesAsync(cancellationToken);
        var runPath = $"{runId.Value:D}/run.json";
        return await DownloadAsync<CatalogueReconciliationRun>(runPath, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogueRunListItem>> ListRunsAsync(int limit, CancellationToken cancellationToken = default)
    {
        await EnsureResourcesAsync(cancellationToken);
        var results = new List<CatalogueRunListItem>();
        await foreach (var entity in _tableClient.QueryAsync<CatalogueRunIndexEntity>(
                           e => e.PartitionKey == "catalogue",
                           cancellationToken: cancellationToken))
        {
            results.Add(new CatalogueRunListItem(
                CatalogueRunId.FromGuid(Guid.Parse(entity.RowKey)),
                entity.ExecutedAt ?? DateTimeOffset.MinValue,
                entity.TotalExceptions,
                entity.MissingProductCount,
                entity.MissingPriceCount,
                entity.IncorrectPriceCount,
                entity.DuplicateCount,
                entity.ActionableFixCount,
                ParseGuid(entity.StripeIngestionRunId),
                ParseGuid(entity.PricingIngestionRunId)));

            if (results.Count >= limit)
            {
                break;
            }
        }

        return results.OrderByDescending(r => r.ExecutedAt).Take(limit).ToList();
    }

    private async Task<T?> DownloadAsync<T>(string path, CancellationToken cancellationToken)
    {
        var client = _containerClient.GetBlobClient(path);
        if (!await client.ExistsAsync(cancellationToken))
        {
            return default;
        }

        var content = await client.DownloadContentAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(content.Value.Content.ToString(), JsonOptions);
    }

    private async Task UploadJsonAsync<T>(string path, T payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var client = _containerClient.GetBlobClient(path);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        await client.UploadAsync(stream, overwrite: true, cancellationToken);
    }

    private async Task EnsureResourcesAsync(CancellationToken cancellationToken)
    {
        if (!_containerEnsured)
        {
            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _containerEnsured = true;
        }

        if (!_tableEnsured)
        {
            await _tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _tableEnsured = true;
        }
    }

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var guid) ? guid : null;
}

/// <summary>Azure Table entity for catalogue run index rows.</summary>
public sealed class CatalogueRunIndexEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "catalogue";
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public DateTimeOffset? ExecutedAt { get; set; }
    public string? StripeIngestionRunId { get; set; }
    public string? PricingIngestionRunId { get; set; }
    public int TotalExceptions { get; set; }
    public int MissingProductCount { get; set; }
    public int MissingPriceCount { get; set; }
    public int IncorrectPriceCount { get; set; }
    public int DuplicateCount { get; set; }
    public int ActionableFixCount { get; set; }
    public string BlobManifestPath { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed";
}
