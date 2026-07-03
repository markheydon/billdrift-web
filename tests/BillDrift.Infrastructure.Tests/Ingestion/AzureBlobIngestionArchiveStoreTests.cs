using System.Text.Json;
using BillDrift.Application.Import;
using BillDrift.Domain.Billing;
using BillDrift.Domain.Common;
using BillDrift.Domain.Import;
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

    [Fact]
    [Trait("Category", AzureStorageTestSupport.IntegrationTrait)]
    public async Task Retail_pricing_blob_round_trip_persists_and_reloads_all_payloads()
    {
        AzureStorageTestSupport.EnsureAvailableOrSkip();

        var store = CreateStore($"retailblob{Guid.NewGuid():N}");
        var ingestionId = Guid.NewGuid();
        var uploadedAt = DateTimeOffset.UtcNow;

        await store.UploadSourceAsync(
            ingestionId,
            "Offer ID,SKU ID,Term,Frequency,Wholesale,RRP\nOFFER-1,SKU-1,Annual,Monthly,5.00,8.00"u8.ToArray(),
            "pricing.csv",
            TestContext.Current.CancellationToken);

        var result = new RetailPricingCsvIngestionResult
        {
            SourceDocumentId = "pricing-hash",
            IngestedAt = DateTimeOffset.UtcNow,
            Status = IngestionOutcomeStatus.Success,
            RawCatalogueRows = [CreateRawCatalogueRow()],
            RawManualEntries = [CreateRawManualEntry()],
            CataloguePrices = [CreateIntendedPrice(PriceSource.Catalogue)],
            ManualPrices = [CreateIntendedPrice(PriceSource.ManualOverride)],
            ResolvedPrices = [CreateIntendedPrice(PriceSource.ManualOverride)],
            ResolutionDetails = [],
            LogEntries = [],
            Summary = new RetailPricingCsvIngestionSummary { ResolvedPriceCount = 1 }
        };

        var manifestPath = await store.PersistRetailPricingResultAsync(
            ingestionId,
            result,
            "pricing.csv",
            uploadedAt,
            TestContext.Current.CancellationToken);

        manifestPath.Should().EndWith("manifest.json");
        var loaded = await store.GetRetailPricingResultAsync(ingestionId, TestContext.Current.CancellationToken);
        loaded.Should().NotBeNull();
        loaded!.SourceDocumentId.Should().Be("pricing-hash");
        loaded.RawCatalogueRows.Should().HaveCount(1);
        loaded.CataloguePrices.Should().HaveCount(1);
        loaded.ManualPrices.Should().HaveCount(1);
        loaded.RawManualEntries.Should().HaveCount(1);
        loaded.ResolvedPrices.Should().HaveCount(1);
    }

    [Fact]
    [Trait("Category", AzureStorageTestSupport.IntegrationTrait)]
    public async Task Retail_pricing_manifest_source_path_matches_uploaded_blob_when_filename_absent()
    {
        AzureStorageTestSupport.EnsureAvailableOrSkip();

        var containerName = $"retailblob{Guid.NewGuid():N}";
        var store = CreateStore(containerName);
        var ingestionId = Guid.NewGuid();

        // Blank filename is the exact case where source-path defaults could diverge.
        var uploadedPath = await store.UploadSourceAsync(
            ingestionId,
            "Offer ID,SKU ID,Term,Frequency,Wholesale,RRP\nOFFER-1,SKU-1,Annual,Monthly,5.00,8.00"u8.ToArray(),
            originalFileName: null,
            TestContext.Current.CancellationToken);

        await store.PersistRetailPricingResultAsync(
            ingestionId,
            EmptyResult("pricing-hash"),
            originalFileName: null,
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        var manifest = await DownloadManifestAsync(containerName, ingestionId, TestContext.Current.CancellationToken);
        manifest.Blobs.Source.Should().Be(uploadedPath);

        // The blob at the manifest-recorded source path must actually exist.
        var blobServiceClient = AzureStorageTestSupport.CreateBlobServiceClient(AzureStorageTestSupport.GetConnectionString());
        var sourceBlob = blobServiceClient
            .GetBlobContainerClient(containerName)
            .GetBlobClient(manifest.Blobs.Source);
        (await sourceBlob.ExistsAsync(TestContext.Current.CancellationToken)).Value.Should().BeTrue();
    }

    private static async Task<RetailPricingManifestDocument> DownloadManifestAsync(
        string containerName,
        Guid ingestionId,
        CancellationToken cancellationToken)
    {
        var blobServiceClient = AzureStorageTestSupport.CreateBlobServiceClient(AzureStorageTestSupport.GetConnectionString());
        var manifestClient = blobServiceClient
            .GetBlobContainerClient(containerName)
            .GetBlobClient($"{ingestionId:D}/result/manifest.json");

        var content = await manifestClient.DownloadContentAsync(cancellationToken);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonSerializer.Deserialize<RetailPricingManifestDocument>(
            content.Value.Content.ToString(),
            options)!;
    }

    private static RetailPricingCsvIngestionResult EmptyResult(string sourceDocumentId) => new()
    {
        SourceDocumentId = sourceDocumentId,
        IngestedAt = DateTimeOffset.UtcNow,
        Status = IngestionOutcomeStatus.Success,
        RawCatalogueRows = [],
        RawManualEntries = [],
        CataloguePrices = [],
        ManualPrices = [],
        ResolvedPrices = [],
        ResolutionDetails = [],
        LogEntries = [],
        Summary = new RetailPricingCsvIngestionSummary()
    };

    private static RawPriceListRow CreateRawCatalogueRow() =>
        new(
            RawImportId.Create(ImportSourceKind.GiacomPriceList, "pricing-hash", "1"),
            "OFFER-1",
            "SKU-1",
            "Annual",
            "Monthly",
            "5.00",
            "8.00",
            null,
            null,
            "Active",
            null,
            null,
            "pricing-hash",
            1);

    private static RawManualPriceEntry CreateRawManualEntry() =>
        new(
            RawImportId.Create(ImportSourceKind.ManualPriceEntry, "pricing-hash/manual-overrides", "override-1"),
            "OFFER-1",
            "SKU-1",
            "Annual",
            "Monthly",
            "5.00",
            "9.00",
            "Bespoke pricing",
            new DateOnly(2026, 1, 1),
            DateTimeOffset.UtcNow);

    private static IntendedPrice CreateIntendedPrice(PriceSource source) =>
        new(
            IntendedPriceId.New(),
            CommercialKey.Create(
                OfferId.Create("OFFER-1"),
                SkuId.Create("SKU-1"),
                Term.Annual,
                BillingFrequency.Monthly),
            Money.Gbp(5.00m),
            Money.Gbp(source == PriceSource.ManualOverride ? 9.00m : 8.00m),
            null,
            null,
            PriceListStatus.Active,
            source,
            SourceReference.FromRawImportId(
                RawImportId.Create(ImportSourceKind.GiacomPriceList, "pricing-hash", "1")));

    private static AzureBlobIngestionArchiveStore CreateStore(string containerName)
    {
        var blobServiceClient = AzureStorageTestSupport.CreateBlobServiceClient(AzureStorageTestSupport.GetConnectionString());
        return new AzureBlobIngestionArchiveStore(
            blobServiceClient,
            Options.Create(new IngestionStorageOptions { BlobContainerName = containerName }));
    }
}
