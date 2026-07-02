using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Storage.Blobs;
using BillDrift.Application.Approval;
using BillDrift.Domain.Approval;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.Approval;

/// <summary>Exports approved changesets to Azure Blob Storage.</summary>
public sealed class AzureBlobChangesetExporter : IApprovedChangesetExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly BlobContainerClient _containerClient;
    private readonly IApprovalStore _store;
    private bool _containerEnsured;

    /// <summary>Creates an exporter using an Aspire-injected blob service client.</summary>
    public AzureBlobChangesetExporter(
        BlobServiceClient blobServiceClient,
        IOptions<ApprovalStorageOptions> options,
        IApprovalStore store)
    {
        ArgumentNullException.ThrowIfNull(blobServiceClient);
        _containerClient = blobServiceClient.GetBlobContainerClient(options.Value.ChangesetContainerName);
        _store = store;
    }

    /// <inheritdoc />
    public async Task<ApprovedChangeset> ExportAsync(ApprovedChangeset changeset, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);

        var payload = new ChangesetDocument(
            1,
            changeset.ExportId,
            changeset.RunId.Value,
            changeset.ExportedAt,
            changeset.ExportedBy,
            ComputeHash(changeset),
            changeset.Entries.Select(entry => new ChangesetEntryDocument(
                entry.ProposalId.Value,
                entry.IdempotencyKey.Value,
                entry.ActionType.ToString(),
                entry.CustomerMexId.Value,
                entry.ProductLabel,
                entry.PriorValues,
                entry.ProposedValues,
                entry.ApprovedAt,
                entry.ApprovedBy,
                entry.ExecutionOrder)).ToList());

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var blobPath = $"{changeset.RunId.Value}/{changeset.ExportId}.json";
        var blobClient = _containerClient.GetBlobClient(blobPath);

        await blobClient.UploadAsync(
            BinaryData.FromString(json),
            overwrite: true,
            cancellationToken);

        await _store.SaveExportMetadataAsync(
            changeset.ExportId,
            changeset.RunId,
            changeset.ExportedBy,
            blobPath,
            changeset.Entries.Count,
            payload.ContentHash,
            cancellationToken);

        return changeset with { BlobUri = blobClient.Uri.ToString() };
    }

    /// <inheritdoc />
    public async Task<string> DownloadAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var response = await _containerClient.GetBlobClient(blobPath).DownloadContentAsync(cancellationToken);
        return response.Value.Content.ToString();
    }

    private static string ComputeHash(ApprovedChangeset changeset)
    {
        var builder = new StringBuilder();
        foreach (var entry in changeset.Entries.OrderBy(e => e.ExecutionOrder).ThenBy(e => e.ProposalId.Value))
        {
            builder.Append(entry.ProposalId.Value);
            builder.Append(entry.IdempotencyKey.Value);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerEnsured)
        {
            return;
        }

        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        _containerEnsured = true;
    }

    private sealed record ChangesetDocument(
        int SchemaVersion,
        Guid ExportId,
        Guid RunId,
        DateTimeOffset ExportedAt,
        string ExportedBy,
        string ContentHash,
        IReadOnlyList<ChangesetEntryDocument> Entries);

    private sealed record ChangesetEntryDocument(
        Guid ProposalId,
        string IdempotencyKey,
        string ActionType,
        string CustomerMexId,
        string ProductLabel,
        IReadOnlyDictionary<string, string> PriorValues,
        IReadOnlyDictionary<string, string> ProposedValues,
        DateTimeOffset ApprovedAt,
        string ApprovedBy,
        int ExecutionOrder);
}
