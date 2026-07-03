# Contract: Azure Table Ingestion Index

**Feature**: `009-giacom-subscription-csv`  
**Storage**: Azure Table Storage via Aspire-injected `TableServiceClient`  
**Implementation**: `BillDrift.Infrastructure.Ingestion.AzureTableIngestionRunIndexStore`

## Table

**Default name**: `ingestionruns` (override via `IngestionStorageOptions.TableName`)

```csharp
// BillDrift.Api/Program.cs (existing)
builder.AddAzureTableServiceClient("tables");
```

Store constructor:

```csharp
AzureTableIngestionRunIndexStore(TableServiceClient tableServiceClient, IOptions<IngestionStorageOptions> options)
```

**No manual connection strings.**

## Entity Design

### Partition / Row Key Strategy

| Field | Value |
|-------|-------|
| `PartitionKey` | `GiacomSubscriptionManagement` (source kind) |
| `RowKey` | `{ingestionId:D}` inverted timestamp optional for sort — use `{DateTime.MaxValue.Ticks - uploadedAt.Ticks:D19}_{ingestionId:D}` for newest-first listing |

### Entity Properties

| Property | Type | Required | Notes |
|----------|------|----------|-------|
| `IngestionId` | string (Guid) | Yes | |
| `SourceKind` | string | Yes | `GiacomSubscriptionManagement` |
| `OriginalFileName` | string | No | |
| `ContentFingerprint` | string | Yes | SHA-256 hex |
| `UploadedAt` | DateTimeOffset | Yes | UTC |
| `CompletedAt` | DateTimeOffset | No | |
| `Status` | string | Yes | `InProgress`, `Completed`, `PartialSuccess`, `Failed` |
| `RowsEmitted` | int | No | |
| `RowsExcludedByScope` | int | No | |
| `RowsSkipped` | int | No | |
| `SourceBlobPath` | string | Yes | |
| `ManifestBlobPath` | string | No | Set on completion |
| `FailureReason` | string | No | |

## Operations

| Method | Purpose |
|--------|---------|
| `CreateInProgressAsync` | Insert row at upload start |
| `CompleteAsync` | Update status, counts, manifest path |
| `FailAsync` | Set `Failed` + reason |
| `GetByIdAsync` | Single run lookup |
| `ListRecentAsync(take)` | Operator history (newest first) |

## Application Interface

```csharp
namespace BillDrift.Application.Ingestion;

public interface IIngestionRunIndexStore
{
    Task CreateInProgressAsync(SubscriptionManagementIngestionRun run, CancellationToken ct = default);
    Task CompleteAsync(SubscriptionManagementIngestionRun run, CancellationToken ct = default);
    Task<SubscriptionManagementIngestionRun?> GetByIdAsync(Guid ingestionId, CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionManagementIngestionRun>> ListRecentAsync(int take = 20, CancellationToken ct = default);
}
```

## API Endpoints (BillDrift.Api)

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/imports/subscription-management` | Multipart CSV upload → `IngestAndPersistAsync` |
| `GET` | `/api/imports/subscription-management` | List recent ingestion runs |
| `GET` | `/api/imports/subscription-management/{ingestionId}` | Run detail + summary |
| `GET` | `/api/imports/subscription-management/{ingestionId}/subscription-truth` | Deserialized `MicrosoftSubscriptionLine[]` from blob |

## Testing

| Test type | Store |
|-----------|-------|
| Unit / parser | In-memory; no Azure |
| Store integration | Azurite via existing AppHost emulator |
| API integration | WebApplicationFactory + Azurite |

Use `InMemoryIngestionRunIndexStore` + `InMemoryIngestionBlobStore` for fast unit tests (008 pattern).

## Constraints

- **No SQL** — table holds index metadata only; payloads in blobs
- **No Web storage clients** — `BillDrift.Web` calls API only
- **Aspire DI only** for `BlobServiceClient` / `TableServiceClient` in Infrastructure

## Options Type

```csharp
public sealed class IngestionStorageOptions
{
    public string BlobContainerName { get; set; } = "ingestion-uploads";
    public string TableName { get; set; } = "ingestionruns";
}
```

Registered in `BillDrift.Api` via `IOptions<IngestionStorageOptions>`.
