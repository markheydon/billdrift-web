# Contract: Azure Blob Ingestion Archive

**Feature**: `009-giacom-subscription-csv`  
**Storage**: Azure Blob Storage via Aspire-injected `BlobServiceClient`  
**Implementation**: `BillDrift.Infrastructure.Ingestion.AzureBlobIngestionArchiveStore`

## Container

**Default name**: `ingestion-uploads` (override via `IngestionStorageOptions.BlobContainerName`)

```csharp
// BillDrift.Api/Program.cs (existing)
builder.AddAzureBlobServiceClient("blobs");
```

Store constructor:

```csharp
AzureBlobIngestionArchiveStore(BlobServiceClient blobServiceClient, IOptions<IngestionStorageOptions> options)
```

**No manual connection strings.**

## Path Layout

```
{ingestionId}/
  source/
    SubscriptionManagementReport.csv     # Original upload bytes
  result/
    manifest.json                        # Written last (commit marker)
    raw-rows.json                        # RawSubscriptionManagementRow[]
    subscription-truth.json              # MicrosoftSubscriptionLine[]
```

`ingestionId` = `Guid` string (`D` format).

## manifest.json

```json
{
  "ingestionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sourceKind": "GiacomSubscriptionManagement",
  "originalFileName": "SubscriptionManagementReport.csv",
  "contentFingerprint": "a1b2c3...",
  "uploadedAt": "2026-07-03T09:00:00Z",
  "completedAt": "2026-07-03T09:00:05Z",
  "status": "Completed",
  "summary": {
    "rowsRead": 120,
    "rowsEmitted": 115,
    "rowsSkipped": 2,
    "rowsExcludedByScope": 3
  },
  "blobs": {
    "source": "3fa85f64-.../source/SubscriptionManagementReport.csv",
    "rawRows": "3fa85f64-.../result/raw-rows.json",
    "subscriptionTruth": "3fa85f64-.../result/subscription-truth.json"
  },
  "contentHashes": {
    "rawRows": "sha256:...",
    "subscriptionTruth": "sha256:..."
  }
}
```

## Payload Blobs

### raw-rows.json

```json
{
  "records": [ /* RawSubscriptionManagementRow[] */ ],
  "logEntries": [ /* IngestionLogEntry[] */ ]
}
```

### subscription-truth.json

```json
{
  "records": [ /* MicrosoftSubscriptionLine[] */ ],
  "normalizationSkipped": 2
}
```

**Serialization**: `System.Text.Json` with source-generated `IngestionJsonSerializerContext` referencing `BillDrift.Domain` types.

## Write Protocol

1. Upload `source/SubscriptionManagementReport.csv`
2. Run ingester
3. Upload `raw-rows.json` and `subscription-truth.json`; compute SHA-256 per blob
4. Upload `manifest.json` last
5. Set blob metadata: `ingestionid`, `sourcekind`, `status`

**On failure**: Write partial blobs if available; manifest `status: Failed` with `failureReason`.

## Read Protocol

`GetIngestionResultAsync(ingestionId)`:

1. Read `manifest.json`; if missing → `InProgress` or not found
2. Verify `contentHashes` on load (optional integrity check)
3. Return deserialized records

## Integration with Run History (008)

When reconciliation run persists inputs, `InputSnapshotMetadata` for `SubscriptionTruth` domain references:

| Field | Source |
|-------|--------|
| `SourceFileName` | `manifest.originalFileName` |
| `ContentFingerprint` | `manifest.contentFingerprint` |
| `UploadedAt` | `manifest.uploadedAt` |
| `BlobPath` | `result/subscription-truth.json` path |

## Size Expectations

| Blob | Typical size |
|------|--------------|
| Source CSV | 100 KB – 2 MB |
| raw-rows.json | 200 KB – 5 MB |
| subscription-truth.json | 150 KB – 4 MB |

## No SQL

All ingestion payloads live in Blob Storage. Table Storage holds index metadata only.
