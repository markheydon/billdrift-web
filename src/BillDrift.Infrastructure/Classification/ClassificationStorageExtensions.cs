using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using BillDrift.Application.Classification;
using BillDrift.Domain.Classification;
using BillDrift.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.Classification;

/// <summary>
/// Azure Table entity mappings for classification persistence.
/// </summary>
internal static class ClassificationTableEntities
{
    public const string ItemPartition = "item";
    public const string HistoryPartition = "hist";
    public const string ConfigPartition = "config";
    public const string InternalMexIdsRow = "internal-mex-ids";
    public const string ProductCategoryRulesRow = "product-category-rules";

    public static string EncodeRowKey(string stableKey) => Uri.EscapeDataString(stableKey);

    public static string DecodeRowKey(string rowKey) => Uri.UnescapeDataString(rowKey);

    public static TableEntity ToItemEntity(ItemClassification classification)
    {
        var entity = new TableEntity(ItemPartition, EncodeRowKey(classification.ItemRef.StableKey))
        {
            ["Kind"] = classification.ItemRef.Kind.ToString(),
            ["CustomerMexId"] = classification.ItemRef.CustomerMexId.Value,
            ["Classification"] = classification.Classification.ToString(),
            ["Source"] = classification.Source.ToString(),
            ["RuleBasis"] = classification.RuleBasis,
            ["Confidence"] = classification.Confidence.ToString(),
            ["UpdatedAt"] = classification.ClassifiedAt
        };

        if (classification.OverrideNotes is not null)
        {
            entity["OverrideNotes"] = classification.OverrideNotes;
        }

        if (classification.OperatorId is not null)
        {
            entity["OperatorId"] = classification.OperatorId;
        }

        if (classification.ItemRef.EntityId is not null)
        {
            entity["EntityId"] = classification.ItemRef.EntityId.Value.ToString();
        }

        return entity;
    }

    public static ItemClassification ToItemClassification(TableEntity entity)
    {
        var stableKey = DecodeRowKey(entity.RowKey);
        var kind = Enum.Parse<ReconciliationItemKind>((string)entity["Kind"]);
        var mexId = MexId.Create((string)entity["CustomerMexId"]);
        Guid? entityId = entity.TryGetValue("EntityId", out var entityIdValue) && entityIdValue is string entityIdText
            ? Guid.Parse(entityIdText)
            : null;
        var itemRef = ReconciliationItemRef.Create(kind, stableKey, mexId, entityId);

        return new ItemClassification(
            itemRef,
            Enum.Parse<ReconciliationItemClassification>((string)entity["Classification"]),
            Enum.Parse<ClassificationSource>((string)entity["Source"]),
            entity.TryGetValue("RuleBasis", out var ruleBasis) ? (string)ruleBasis : string.Empty,
            Enum.Parse<ClassificationConfidence>((string)entity["Confidence"]),
            entity.TryGetValue("OverrideNotes", out var notes) ? notes as string : null,
            entity.TryGetValue("UpdatedAt", out var updatedAt) ? (DateTimeOffset)updatedAt : DateTimeOffset.UtcNow,
            entity.TryGetValue("OperatorId", out var operatorId) ? operatorId as string : null);
    }

    public static ClassificationOverride? ToOverride(TableEntity entity)
    {
        if (!entity.TryGetValue("Classification", out var classificationValue))
        {
            return null;
        }

        var stableKey = DecodeRowKey(entity.RowKey);
        var kind = Enum.Parse<ReconciliationItemKind>((string)entity["Kind"]);
        var mexId = MexId.Create((string)entity["CustomerMexId"]);
        var itemRef = ReconciliationItemRef.Create(kind, stableKey, mexId);

        return new ClassificationOverride(
            itemRef,
            Enum.Parse<ReconciliationItemClassification>((string)classificationValue),
            entity.TryGetValue("OverrideNotes", out var notes) ? (string)notes : string.Empty,
            entity.TryGetValue("OperatorId", out var operatorId) ? (string)operatorId : "system",
            entity.TryGetValue("UpdatedAt", out var updatedAt) ? (DateTimeOffset)updatedAt : DateTimeOffset.UtcNow);
    }

    public static TableEntity ToHistoryEntity(ClassificationHistoryEntry entry)
    {
        var rowKey = $"{EncodeRowKey(entry.ItemRef.StableKey)}:{entry.Timestamp.UtcTicks:D19}";
        return new TableEntity(HistoryPartition, rowKey)
        {
            ["ItemStableKey"] = entry.ItemRef.StableKey,
            ["PriorClassification"] = entry.PriorClassification?.ToString(),
            ["NewClassification"] = entry.NewClassification.ToString(),
            ["Source"] = entry.Source.ToString(),
            ["Notes"] = entry.Notes,
            ["OperatorId"] = entry.OperatorId,
            ["Timestamp"] = entry.Timestamp
        };
    }

    public static ClassificationHistoryEntry ToHistoryEntry(TableEntity entity)
    {
        var stableKey = (string)entity["ItemStableKey"];
        var kind = entity.TryGetValue("Kind", out var kindValue)
            ? Enum.Parse<ReconciliationItemKind>((string)kindValue)
            : ReconciliationItemKind.SupplierCost;
        var mexId = entity.TryGetValue("CustomerMexId", out var mexValue)
            ? MexId.Create((string)mexValue)
            : MexId.Create("unknown");
        var itemRef = ReconciliationItemRef.Create(kind, stableKey, mexId);

        ReconciliationItemClassification? prior = entity.TryGetValue("PriorClassification", out var priorValue) &&
                                                priorValue is string priorText &&
                                                !string.IsNullOrWhiteSpace(priorText)
            ? Enum.Parse<ReconciliationItemClassification>(priorText)
            : null;

        return new ClassificationHistoryEntry(
            itemRef,
            prior,
            Enum.Parse<ReconciliationItemClassification>((string)entity["NewClassification"]),
            Enum.Parse<ClassificationSource>((string)entity["Source"]),
            entity.TryGetValue("Notes", out var notes) ? notes as string : null,
            entity.TryGetValue("OperatorId", out var operatorId) ? operatorId as string : null,
            entity.TryGetValue("Timestamp", out var timestamp) ? (DateTimeOffset)timestamp : DateTimeOffset.UtcNow);
    }

    public static TableEntity ToConfigEntity(string rowKey, string json, string operatorId)
    {
        return new TableEntity(ConfigPartition, rowKey)
        {
            ["Json"] = json,
            ["UpdatedAt"] = DateTimeOffset.UtcNow,
            ["OperatorId"] = operatorId
        };
    }
}

/// <summary>
/// Azure Table Storage implementation of <see cref="IItemClassificationStore"/>.
/// </summary>
public sealed class AzureTableItemClassificationStore : IItemClassificationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly TableClient _tableClient;
    private bool _tableEnsured;

    /// <summary>
    /// Creates a store using an Aspire-injected table service client.
    /// </summary>
    public AzureTableItemClassificationStore(TableServiceClient tableServiceClient, IOptions<ClassificationStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(tableServiceClient);
        _tableClient = tableServiceClient.GetTableClient(options.Value.TableName);
    }

    /// <inheritdoc />
    public async Task<ItemClassification?> GetClassificationAsync(
        string stableKey,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);

        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(
                ClassificationTableEntities.ItemPartition,
                ClassificationTableEntities.EncodeRowKey(stableKey),
                cancellationToken: cancellationToken);

            return ClassificationTableEntities.ToItemClassification(response.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ClassificationOverride?> GetOverrideAsync(
        ReconciliationItemRef itemRef,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);

        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(
                ClassificationTableEntities.ItemPartition,
                ClassificationTableEntities.EncodeRowKey(itemRef.StableKey),
                cancellationToken: cancellationToken);

            var entity = response.Value;
            if (entity.TryGetValue("Source", out var source) &&
                string.Equals((string)source, ClassificationSource.ManualOverride.ToString(), StringComparison.Ordinal))
            {
                return ClassificationTableEntities.ToOverride(entity);
            }

            return null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveOverrideAsync(ClassificationOverride classificationOverride, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);

        var prior = await GetOverrideAsync(classificationOverride.ItemRef, cancellationToken);
        var classification = new ItemClassification(
            classificationOverride.ItemRef,
            classificationOverride.Classification,
            ClassificationSource.ManualOverride,
            "ManualOverride",
            ClassificationConfidence.High,
            classificationOverride.Notes,
            classificationOverride.CreatedAt,
            classificationOverride.OperatorId);

        await _tableClient.UpsertEntityAsync(
            ClassificationTableEntities.ToItemEntity(classification),
            TableUpdateMode.Replace,
            cancellationToken);

        var history = new ClassificationHistoryEntry(
            classificationOverride.ItemRef,
            prior?.Classification,
            classificationOverride.Classification,
            ClassificationSource.ManualOverride,
            classificationOverride.Notes,
            classificationOverride.OperatorId,
            classificationOverride.CreatedAt);

        await _tableClient.AddEntityAsync(
            ClassificationTableEntities.ToHistoryEntity(history),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task ClearOverrideAsync(
        ReconciliationItemRef itemRef,
        string operatorId,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);

        var prior = await GetOverrideAsync(itemRef, cancellationToken);
        if (prior is null)
        {
            return;
        }

        try
        {
            await _tableClient.DeleteEntityAsync(
                ClassificationTableEntities.ItemPartition,
                ClassificationTableEntities.EncodeRowKey(itemRef.StableKey),
                cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Already cleared.
        }

        var history = new ClassificationHistoryEntry(
            itemRef,
            prior.Classification,
            prior.Classification,
            ClassificationSource.Automatic,
            "Override cleared",
            operatorId,
            DateTimeOffset.UtcNow);

        await _tableClient.AddEntityAsync(
            ClassificationTableEntities.ToHistoryEntity(history),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ClassificationRuleConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);

        var internalMexIds = await ReadMexIdsAsync(cancellationToken);
        var categoryRules = await ReadCategoryRulesAsync(cancellationToken);

        return new ClassificationRuleConfiguration(internalMexIds, categoryRules);
    }

    /// <inheritdoc />
    public async Task SaveConfigurationAsync(
        ClassificationRuleConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);

        var mexJson = JsonSerializer.Serialize(
            configuration.InternalMexIds.Select(id => id.Value).ToList(),
            JsonOptions);

        await _tableClient.UpsertEntityAsync(
            ClassificationTableEntities.ToConfigEntity(
                ClassificationTableEntities.InternalMexIdsRow,
                mexJson,
                "system"),
            TableUpdateMode.Replace,
            cancellationToken);

        var rulesJson = JsonSerializer.Serialize(configuration.ProductCategoryRules, JsonOptions);
        await _tableClient.UpsertEntityAsync(
            ClassificationTableEntities.ToConfigEntity(
                ClassificationTableEntities.ProductCategoryRulesRow,
                rulesJson,
                "system"),
            TableUpdateMode.Replace,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClassificationHistoryEntry>> GetHistoryAsync(
        ReconciliationItemRef itemRef,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);

        var filter = $"PartitionKey eq '{ClassificationTableEntities.HistoryPartition}' and ItemStableKey eq '{itemRef.StableKey}'";
        var results = new List<ClassificationHistoryEntry>();

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
                           filter,
                           maxPerPage: limit,
                           cancellationToken: cancellationToken))
        {
            results.Add(ClassificationTableEntities.ToHistoryEntry(entity));
            if (results.Count >= limit)
            {
                break;
            }
        }

        return results
            .OrderByDescending(entry => entry.Timestamp)
            .Take(limit)
            .ToList();
    }

    private async Task<IReadOnlyList<MexId>> ReadMexIdsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(
                ClassificationTableEntities.ConfigPartition,
                ClassificationTableEntities.InternalMexIdsRow,
                cancellationToken: cancellationToken);

            var json = (string)response.Value["Json"];
            var values = JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
            return values.Select(MexId.Create).ToList();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<ProductCategoryRule>> ReadCategoryRulesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(
                ClassificationTableEntities.ConfigPartition,
                ClassificationTableEntities.ProductCategoryRulesRow,
                cancellationToken: cancellationToken);

            var json = (string)response.Value["Json"];
            return JsonSerializer.Deserialize<List<ProductCategoryRule>>(json, JsonOptions) ?? [];
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return [];
        }
    }

    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        if (_tableEnsured)
        {
            return;
        }

        await _tableClient.CreateIfNotExistsAsync(cancellationToken);
        _tableEnsured = true;
    }
}

/// <summary>
/// Registers Azure Table classification persistence.
/// </summary>
public static class ClassificationStorageExtensions
{
    /// <summary>
    /// Registers classification storage services with Azure Table store.
    /// </summary>
    public static IServiceCollection AddClassificationStorage(this IServiceCollection services)
    {
        services.Configure<ClassificationStorageOptions>(_ => { });
        services.AddScoped<IItemClassificationStore, AzureTableItemClassificationStore>();
        return services;
    }
}
