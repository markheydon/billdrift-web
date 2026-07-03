using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using BillDrift.Application.History;
using BillDrift.Domain.History;
using BillDrift.Domain.Reconciliation;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.History;

/// <summary>Azure Blob Storage implementation for run archive snapshots.</summary>
public sealed class AzureBlobRunArchiveStore : IRunBlobArchiveStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(RunHistoryJsonSerializerContext.Default.Options);

    private readonly BlobContainerClient _containerClient;
    private bool _containerEnsured;

    /// <summary>Creates a store using an Aspire-injected blob service client.</summary>
    public AzureBlobRunArchiveStore(BlobServiceClient blobServiceClient, IOptions<RunHistoryStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(blobServiceClient);
        _containerClient = blobServiceClient.GetBlobContainerClient(options.Value.BlobContainerName);
    }

    /// <inheritdoc />
    public async Task<RunArchiveWriteResult> WriteRunArchiveAsync(
        ReconciliationRun run,
        RunArchiveContext context,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var runPrefix = run.Id.Value.ToString("D");
        var inputSnapshots = new List<InputSnapshotMetadata>();

        await WriteInputBlobAsync(runPrefix, InputDomainType.SupplierCost, run.Inputs.SupplierCostLines, context, inputSnapshots, cancellationToken);
        await WriteInputBlobAsync(runPrefix, InputDomainType.SubscriptionTruth, run.Inputs.SubscriptionLines, context, inputSnapshots, cancellationToken);
        await WriteInputBlobAsync(runPrefix, InputDomainType.IntendedPricing, run.Inputs.IntendedPrices, context, inputSnapshots, cancellationToken);
        await WriteInputBlobAsync(runPrefix, InputDomainType.StripeBilling, run.Inputs.StripeItems, context, inputSnapshots, cancellationToken);
        await WriteInputBlobAsync(runPrefix, InputDomainType.ProductMappings, run.Inputs.ProductMappings, context, inputSnapshots, cancellationToken);

        var matchGroupsPath = $"{runPrefix}/results/match-groups.json";
        var mismatchesPath = $"{runPrefix}/results/mismatches.json";
        var proposedChangesPath = $"{runPrefix}/results/proposed-changes.json";

        var matchGroupsHash = await WriteResultsBlobAsync(matchGroupsPath, run.MatchGroups, cancellationToken);
        var mismatchesHash = await WriteResultsBlobAsync(mismatchesPath, run.Mismatches, cancellationToken);
        var proposedChangesHash = await WriteResultsBlobAsync(proposedChangesPath, run.ProposedChanges, cancellationToken);

        var resultsHash = ComputeCombinedHash(matchGroupsHash, mismatchesHash, proposedChangesHash);
        var summary = BuildSummaryMetrics(run);

        var manifest = new RunManifestDocument(
            1,
            run.Id.Value,
            DateTimeOffset.UtcNow,
            new BillingPeriodDocument(run.Scope.Start.ToString("O"), run.Scope.End.ToString("O")),
            new MappingVersionDocument(
                context.MappingVersion.VersionId,
                context.MappingVersion.ContentHash,
                context.MappingVersion.EffectiveDate.ToString("O"),
                context.MappingVersion.Label),
            BuildManifestInputs(inputSnapshots),
            new ManifestResultsSection(
                new ManifestBlobRef("results/match-groups.json", matchGroupsHash),
                new ManifestBlobRef("results/mismatches.json", mismatchesHash),
                new ManifestBlobRef("results/proposed-changes.json", proposedChangesHash)),
            new ManifestSummaryMetrics(
                summary.MatchGroupCount,
                summary.MismatchCount,
                summary.ProposedChangeCount,
                summary.CleanRun));

        var manifestPath = $"{runPrefix}/manifest.json";
        var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
        await UploadJsonAsync(manifestPath, manifestJson, cancellationToken);

        var manifestClient = _containerClient.GetBlobClient(manifestPath);
        await manifestClient.SetMetadataAsync(new Dictionary<string, string>
        {
            ["runid"] = run.Id.Value.ToString("D"),
            ["status"] = RunArchiveStatus.Completed.ToString(),
            ["schemaversion"] = "1"
        }, cancellationToken: cancellationToken);

        return new RunArchiveWriteResult(manifestPath, inputSnapshots, summary, resultsHash);
    }

    /// <inheritdoc />
    public async Task<RunResultsSnapshot> LoadResultsSnapshotAsync(RunId runId, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var manifest = await LoadManifestAsync(runId, cancellationToken);

        var matchGroups = await LoadResultsRecordsAsync<EntityMatchGroup>(
            runId, manifest.Results.MatchGroups, cancellationToken);
        var mismatches = await LoadResultsRecordsAsync<Mismatch>(
            runId, manifest.Results.Mismatches, cancellationToken);
        var proposedChanges = await LoadResultsRecordsAsync<ProposedChange>(
            runId, manifest.Results.ProposedChanges, cancellationToken);

        var combinedHash = ComputeCombinedHash(
            manifest.Results.MatchGroups.ContentHash,
            manifest.Results.Mismatches.ContentHash,
            manifest.Results.ProposedChanges.ContentHash);

        return new RunResultsSnapshot(runId, matchGroups, mismatches, proposedChanges, combinedHash);
    }

    /// <inheritdoc />
    public async Task<string> LoadInputBlobAsync(RunId runId, InputDomainType domain, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var path = GetInputBlobPath(runId, domain);
        var client = _containerClient.GetBlobClient(path);

        try
        {
            var response = await client.DownloadContentAsync(cancellationToken);
            return response.Value.Content.ToString();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // A missing input blob is only an error when the run itself does not exist.
            // For an existing run with an absent domain, return an empty snapshot so
            // callers (input retrieval, pricing-drift analysis) see a normal empty state.
            var manifestClient = _containerClient.GetBlobClient($"{runId.Value:D}/manifest.json");
            if (!await manifestClient.ExistsAsync(cancellationToken))
            {
                throw new RunNotFoundException(runId);
            }

            return EmptyInputBlobContent(domain);
        }
    }

    private static string EmptyInputBlobContent(InputDomainType domain) =>
        $"{{\"domain\":\"{domain}\",\"records\":[]}}";

    /// <inheritdoc />
    public async Task VerifyManifestIntegrityAsync(RunId runId, CancellationToken cancellationToken = default)
    {
        await LoadManifestAsync(runId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(string BlobPath, string ContentHash)> ExportComparisonReportAsync(
        RunId runId,
        RunComparisonReport report,
        CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var path = $"{runId.Value:D}/exports/comparison-{report.EarlierRunId.Value:N}-vs-{report.LaterRunId.Value:N}.json";
        var json = JsonSerializer.Serialize(report, JsonOptions);
        var hash = await UploadJsonAsync(path, json, cancellationToken);
        return (path, hash);
    }

    private async Task<RunManifestDocument> LoadManifestAsync(RunId runId, CancellationToken cancellationToken)
    {
        var path = $"{runId.Value:D}/manifest.json";
        var client = _containerClient.GetBlobClient(path);

        try
        {
            var content = await client.DownloadContentAsync(cancellationToken);
            var manifest = JsonSerializer.Deserialize<RunManifestDocument>(content.Value.Content.ToString(), JsonOptions)
                ?? throw new RunArchiveIntegrityException($"Manifest for run {runId.Value} is invalid.");

            await VerifyBlobHashAsync($"{runId.Value:D}/{manifest.Results.MatchGroups.BlobPath}", manifest.Results.MatchGroups.ContentHash, cancellationToken);
            await VerifyBlobHashAsync($"{runId.Value:D}/{manifest.Results.Mismatches.BlobPath}", manifest.Results.Mismatches.ContentHash, cancellationToken);
            await VerifyBlobHashAsync($"{runId.Value:D}/{manifest.Results.ProposedChanges.BlobPath}", manifest.Results.ProposedChanges.ContentHash, cancellationToken);

            return manifest;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new RunNotFoundException(runId);
        }
    }

    private async Task VerifyBlobHashAsync(string path, string expectedHash, CancellationToken cancellationToken)
    {
        var client = _containerClient.GetBlobClient(path);
        var content = await client.DownloadContentAsync(cancellationToken);
        var actual = HashContent(content.Value.Content.ToString());
        if (!string.Equals(actual, expectedHash, StringComparison.Ordinal))
        {
            throw new RunArchiveIntegrityException($"Blob integrity mismatch at {path}.");
        }
    }

    private async Task<IReadOnlyList<T>> LoadResultsRecordsAsync<T>(
        RunId runId,
        ManifestBlobRef reference,
        CancellationToken cancellationToken)
    {
        var path = $"{runId.Value:D}/{reference.BlobPath}";
        var client = _containerClient.GetBlobClient(path);
        var content = await client.DownloadContentAsync(cancellationToken);
        var hash = HashContent(content.Value.Content.ToString());
        if (!string.Equals(hash, reference.ContentHash, StringComparison.Ordinal))
        {
            throw new RunArchiveIntegrityException($"Results blob hash mismatch at {path}.");
        }

        var doc = JsonSerializer.Deserialize<ResultsBlobDocument<T>>(content.Value.Content.ToString(), JsonOptions);
        return doc?.Records ?? [];
    }

    private async Task WriteInputBlobAsync<T>(
        string runPrefix,
        InputDomainType domain,
        IReadOnlyList<T> records,
        RunArchiveContext context,
        List<InputSnapshotMetadata> snapshots,
        CancellationToken cancellationToken)
    {
        var meta = context.InputMetadata.GetValueOrDefault(domain);
        var isPresent = meta?.IsPresent ?? records.Count > 0;

        if (!isPresent)
        {
            snapshots.Add(new InputSnapshotMetadata(domain, false, RecordCount: 0));
            return;
        }

        var relativePath = GetRelativeInputPath(domain);
        var blobPath = $"{runPrefix}/{relativePath}";
        var doc = new InputBlobDocument<T>(
            domain.ToString(),
            meta?.SourceFileName is not null
                ? new SourceMetadataDocument(meta.SourceFileName, meta.UploadedAt, meta.ContentFingerprint, null)
                : null,
            records);

        var json = JsonSerializer.Serialize(doc, JsonOptions);
        var hash = await UploadJsonAsync(blobPath, json, cancellationToken);

        snapshots.Add(new InputSnapshotMetadata(
            domain,
            true,
            meta?.SourceFileName,
            meta?.UploadedAt,
            meta?.ContentFingerprint,
            meta?.BillingPeriodScope,
            records.Count,
            relativePath,
            hash));
    }

    private async Task<string> WriteResultsBlobAsync<T>(string path, IReadOnlyList<T> records, CancellationToken cancellationToken)
    {
        var doc = new ResultsBlobDocument<T>(records);
        var json = JsonSerializer.Serialize(doc, JsonOptions);
        return await UploadJsonAsync(path, json, cancellationToken);
    }

    private async Task<string> UploadJsonAsync(string path, string json, CancellationToken cancellationToken)
    {
        var hash = HashContent(json);
        var client = _containerClient.GetBlobClient(path);
        await client.UploadAsync(BinaryData.FromString(json), overwrite: true, cancellationToken);
        return hash;
    }

    private static string HashContent(string content) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant()}";

    private static string ComputeCombinedHash(params string[] hashes) =>
        HashContent(string.Join('|', hashes));

    private static RunSummaryMetrics BuildSummaryMetrics(ReconciliationRun run)
    {
        var byCategory = run.Mismatches
            .GroupBy(m => m.Type.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        return new RunSummaryMetrics(
            run.MatchGroups.Count,
            run.Mismatches.Count,
            byCategory,
            run.ProposedChanges.Count,
            run.Mismatches.Count == 0);
    }

    private static Dictionary<string, ManifestInputEntry> BuildManifestInputs(IReadOnlyList<InputSnapshotMetadata> snapshots)
    {
        var map = new Dictionary<string, ManifestInputEntry>();
        foreach (var snapshot in snapshots)
        {
            var key = ToManifestInputKey(snapshot.Domain);
            map[key] = snapshot.IsPresent
                ? new ManifestInputEntry(true, snapshot.BlobPath, snapshot.ContentHash, snapshot.RecordCount)
                : new ManifestInputEntry(false, RecordCount: 0);
        }

        return map;
    }

    private static string ToManifestInputKey(InputDomainType domain) => domain switch
    {
        InputDomainType.SupplierCost => "supplierCost",
        InputDomainType.SubscriptionTruth => "subscriptionTruth",
        InputDomainType.IntendedPricing => "intendedPricing",
        InputDomainType.StripeBilling => "stripeBilling",
        InputDomainType.ProductMappings => "productMappings",
        _ => domain.ToString()
    };

    private static string GetRelativeInputPath(InputDomainType domain) => domain switch
    {
        InputDomainType.SupplierCost => "inputs/supplier-cost.json",
        InputDomainType.SubscriptionTruth => "inputs/subscription-truth.json",
        InputDomainType.IntendedPricing => "inputs/intended-pricing.json",
        InputDomainType.StripeBilling => "inputs/stripe-billing.json",
        InputDomainType.ProductMappings => "inputs/product-mappings.json",
        _ => throw new ArgumentOutOfRangeException(nameof(domain))
    };

    private static string GetInputBlobPath(RunId runId, InputDomainType domain) =>
        $"{runId.Value:D}/{GetRelativeInputPath(domain)}";

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerEnsured)
        {
            return;
        }

        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        _containerEnsured = true;
    }
}
