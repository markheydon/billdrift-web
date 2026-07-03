using BillDrift.Application.Import;
using BillDrift.Infrastructure.Ingestion;
using BillDrift.Infrastructure.Tests.Storage;
using Microsoft.Extensions.Options;

namespace BillDrift.Infrastructure.Tests.Ingestion;

public sealed class AzureBlobIngestionArchiveStoreTests
{
    [Fact]
    [Trait("Category", AzureStorageTestSupport.IntegrationTrait)]
    public async Task Blob_round_trip_persists_manifest_last()
    {
        AzureStorageTestSupport.EnsureAvailableOrSkip();

        var store = CreateStore($"ingestionblob{Guid.NewGuid():N}");
        var ingestionId = Guid.NewGuid();
        var uploadedAt = DateTimeOffset.UtcNow;
        var content = "Mex ID,Offer ID,SKU ID,Licences,Status\nMEX001,OFFER-1,SKU-1,1,Active"u8.ToArray();

        await store.UploadSourceAsync(ingestionId, content, "sample.csv", TestContext.Current.CancellationToken);

        var result = new SubscriptionManagementCsvIngestionResult
        {
            SourceDocumentId = "abc123",
            IngestedAt = DateTimeOffset.UtcNow,
            Status = IngestionOutcomeStatus.Success,
            RawRows = [],
            SubscriptionLines = [],
            LogEntries = [],
            Summary = new SubscriptionManagementCsvIngestionSummary { RowsRead = 1, RowsEmitted = 1 },
            SourceFile = new SubscriptionManagementSourceFileInfo("abc123", "sample.csv", 1)
        };

        var manifestPath = await store.PersistResultAsync(
            ingestionId,
            result,
            "sample.csv",
            uploadedAt,
            TestContext.Current.CancellationToken);

        manifestPath.Should().EndWith("manifest.json");
        var loaded = await store.GetIngestionResultAsync(ingestionId, TestContext.Current.CancellationToken);
        loaded.Should().NotBeNull();
        loaded!.SourceDocumentId.Should().Be("abc123");
    }

    private static AzureBlobIngestionArchiveStore CreateStore(string containerName)
    {
        var blobServiceClient = AzureStorageTestSupport.CreateBlobServiceClient(AzureStorageTestSupport.GetConnectionString());
        return new AzureBlobIngestionArchiveStore(
            blobServiceClient,
            Options.Create(new IngestionStorageOptions { BlobContainerName = containerName }));
    }
}
