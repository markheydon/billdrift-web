# Contract: Azure Table Ingestion Index (Retail Pricing)

**Feature**: `010-retail-pricing-ingestion`  
**Storage**: Azure Table Storage via Aspire-injected `TableServiceClient`  
**Implementation**: `BillDrift.Infrastructure.Ingestion.AzureTableIngestionRunIndexStore` (extended)

## Table

**Default name**: `ingestionruns` (via `IngestionStorageOptions.TableName`)

```csharp
// BillDrift.Api/Program.cs (existing)
builder.AddAzureTableServiceClient("tables");
```

```csharp
AzureTableIngestionRunIndexStore(TableServiceClient tableServiceClient, IOptions<IngestionStorageOptions> options)
```

**No manual connection strings.**

## Entity Design

### Partition / Row Key Strategy

| Field | Value |
|-------|-------|
| `PartitionKey` | `GiacomPriceList` |
| `RowKey` | `{DateTime.MaxValue.Ticks - uploadedAt.Ticks:D19}_{ingestionId:D}` |

Same table as Subscription Management runs; partitioned by source kind.

### Entity Properties

| Property | Type | Required | Notes |
|----------|------|----------|-------|
| `IngestionId` | string (Guid) | Yes | |
| `SourceKind` | string | Yes | `GiacomPriceList` |
| `OriginalFileName` | string | No | |
| `ContentFingerprint` | string | Yes | SHA-256 hex of catalogue CSV |
| `UploadedAt` | DateTimeOffset | Yes | UTC |
| `CompletedAt` | DateTimeOffset | No | |
| `Status` | string | Yes | `InProgress`, `Completed`, `PartialSuccess`, `Failed` |
| `ResolvedPriceCount` | int | No | |
| `CatalogueRowsSkipped` | int | No | |
| `OverrideWinsCount` | int | No | |
| `SourceBlobPath` | string | Yes | |
| `ManifestBlobPath` | string | No | |
| `FailureReason` | string | No | |

## Interface Extension

```csharp
// BillDrift.Application.Ingestion.IIngestionRunIndexStore — add:
Task CreateRetailPricingInProgressAsync(RetailPricingIngestionRun run, CancellationToken ct = default);
Task CompleteRetailPricingAsync(RetailPricingIngestionRun run, CancellationToken ct = default);
Task FailRetailPricingAsync(RetailPricingIngestionRun run, CancellationToken ct = default);
Task<RetailPricingIngestionRun?> GetRetailPricingByIdAsync(Guid ingestionId, CancellationToken ct = default);
Task<IReadOnlyList<RetailPricingIngestionRun>> ListRecentRetailPricingAsync(int take = 20, CancellationToken ct = default);
```

## API Endpoints (BillDrift.Api)

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/imports/retail-pricing` | Multipart: CSV + optional `manual-overrides.json` |
| `GET` | `/api/imports/retail-pricing` | List recent price list ingestion runs |
| `GET` | `/api/imports/retail-pricing/{ingestionId}` | Run detail + summary |
| `GET` | `/api/imports/retail-pricing/{ingestionId}/resolved-prices` | Deserialized `IntendedPrice[]` from blob |

### POST multipart parts

| Part name | Required | Content |
|-----------|----------|---------|
| `catalogue` | Yes | `ResellerPricingVsRRP.csv` bytes |
| `manualOverrides` | No | JSON array of `ManualPriceOverrideRequest` |

## Testing

| Test type | Store |
|-----------|-------|
| Parser unit | In-memory; no Azure |
| Store integration | Azurite via AppHost emulator |
| API integration | WebApplicationFactory + Azurite |

Reuse `InMemoryIngestionBlobStore` / `InMemoryIngestionRunIndexStore` extensions for fast tests.

## Constraints

- **No SQL** — index metadata only; payloads in blobs
- **Aspire DI only** for `TableServiceClient`
- **No Web storage clients**
