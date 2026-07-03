# Contract: Azure Blob Ingestion Archive (Retail Pricing)

**Feature**: `010-retail-pricing-ingestion`  
**Storage**: Azure Blob Storage via Aspire-injected `BlobServiceClient`  
**Implementation**: `BillDrift.Infrastructure.Ingestion.AzureBlobIngestionArchiveStore` (extended)

## Container

**Default name**: `ingestion-uploads` (via `IngestionStorageOptions.BlobContainerName`)

```csharp
// BillDrift.Api/Program.cs (existing)
builder.AddAzureBlobServiceClient("blobs");
```

```csharp
AzureBlobIngestionArchiveStore(BlobServiceClient blobServiceClient, IOptions<IngestionStorageOptions> options)
```

**No manual connection strings.**

## Path Layout

```
{ingestionId}/
  source/
    ResellerPricingVsRRP.csv          # Original catalogue upload bytes
    manual-overrides.json             # Optional; persisted override requests
  result/
    manifest.json                     # Written last (commit marker)
    raw-catalogue-rows.json           # RawPriceListRow[] + log entries
    catalogue-prices.json             # IntendedPrice[] (Catalogue source only)
    manual-prices.json                # IntendedPrice[] (ManualOverride source only)
    resolved-prices.json              # IntendedPrice[] after strategy merge ★ primary consumer payload
```

`ingestionId` = `Guid` string (`D` format).

## manifest.json

```json
{
  "ingestionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sourceKind": "GiacomPriceList",
  "originalFileName": "ResellerPricingVsRRP.csv",
  "contentFingerprint": "a1b2c3...",
  "uploadedAt": "2026-07-03T10:00:00Z",
  "completedAt": "2026-07-03T10:00:04Z",
  "status": "Completed",
  "summary": {
    "catalogueRowsRead": 520,
    "catalogueRowsEmitted": 515,
    "catalogueRowsSkipped": 5,
    "manualOverridesSubmitted": 3,
    "manualOverridesAccepted": 3,
    "resolvedPriceCount": 518,
    "overrideWinsCount": 2
  },
  "blobs": {
    "source": "3fa85f64-.../source/ResellerPricingVsRRP.csv",
    "manualOverrides": "3fa85f64-.../source/manual-overrides.json",
    "rawCatalogueRows": "3fa85f64-.../result/raw-catalogue-rows.json",
    "cataloguePrices": "3fa85f64-.../result/catalogue-prices.json",
    "manualPrices": "3fa85f64-.../result/manual-prices.json",
    "resolvedPrices": "3fa85f64-.../result/resolved-prices.json"
  },
  "contentHashes": {
    "resolvedPrices": "sha256:..."
  }
}
```

## Primary Consumer Payload: resolved-prices.json

```json
{
  "records": [ /* IntendedPrice[] */ ],
  "resolutionDetails": [ /* PricingResolutionDetail[] */ ],
  "logEntries": [ /* IngestionLogEntry[] */ ]
}
```

Feeds:
- `008` run archive `inputs/intended-pricing.json`
- `004` `ReconciliationInputs.IntendedPrices`

## Write Order (commit pattern)

1. Upload source CSV (+ optional manual-overrides.json)
2. Write `raw-catalogue-rows.json`
3. Write `catalogue-prices.json`
4. Write `manual-prices.json`
5. Write `resolved-prices.json`
6. Write `manifest.json` **last**

## Interface Extension

```csharp
// BillDrift.Application.Ingestion.IIngestionBlobStore — add:
Task<string> PersistRetailPricingResultAsync(
    Guid ingestionId,
    RetailPricingCsvIngestionResult result,
    byte[]? manualOverridesJson,
    string? originalFileName,
    DateTimeOffset uploadedAt,
    CancellationToken cancellationToken = default);

Task<RetailPricingCsvIngestionResult?> GetRetailPricingResultAsync(
    Guid ingestionId,
    CancellationToken cancellationToken = default);

Task<IReadOnlyList<IntendedPrice>?> GetResolvedPricesAsync(
    Guid ingestionId,
    CancellationToken cancellationToken = default);
```

## Run History Integration

`InputSnapshotMetadata` for `InputDomainType.IntendedPricing`:
- `ContentFingerprint` = catalogue CSV hash
- `BlobPath` = `resolved-prices.json`
- `RecordCount` = `ResolvedPriceCount`

## Constraints

- **No SQL**
- **Aspire DI only** for `BlobServiceClient`
- **No Web storage clients** — UI posts to API
