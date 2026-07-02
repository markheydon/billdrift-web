# Contract: Azure Table Schema — Run History

**Feature**: `008-reconciliation-run-history`  
**Storage**: Azure Table Storage via Aspire-injected `TableServiceClient`  
**Date**: 2026-07-02

## Table

**Default name**: `reconciliationrunhistory` (override via `RunHistoryStorageOptions.TableName`)

Create table idempotently on first store access via `AzureTableRunHistoryStore.EnsureTableAsync()`.

**Client registration** (API only):

```csharp
builder.AddAzureTableServiceClient("tables");
```

Store constructor: `AzureTableRunHistoryStore(TableServiceClient client, IOptions<RunHistoryStorageOptions> options)` — **no manual connection strings**.

---

## Entity: Run Index (`PartitionKey = "run"`)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `PartitionKey` | string | Yes | Constant `"run"` |
| `RowKey` | string | Yes | `{completedAtUtcTicks:D19}:{runId:N}` — reverse chronological |
| `RunId` | string | Yes | GUID |
| `Status` | string | Yes | `RunArchiveStatus` |
| `BillingPeriodStart` | string | Yes | ISO date |
| `BillingPeriodEnd` | string | Yes | ISO date |
| `StartedAt` | DateTimeOffset | Yes | UTC |
| `CompletedAt` | DateTimeOffset | No | UTC |
| `InitiatorId` | string | No | Operator |
| `MappingVersionId` | string | Yes | Version label |
| `MappingContentHash` | string | Yes | SHA-256 |
| `MappingEffectiveDate` | string | Yes | ISO date |
| `ManifestBlobPath` | string | Yes | `{runId}/manifest.json` |
| `MatchGroupCount` | int | Yes | Summary |
| `MismatchCount` | int | Yes | Summary |
| `ProposedChangeCount` | int | Yes | Summary |
| `CleanRun` | bool | Yes | Zero mismatches |
| `MismatchCountByCategoryJson` | string | No | JSON dict |
| `FailureReason` | string | No | When failed |
| `IsArchived` | bool | Yes | Retention flag |
| `ArchivedAt` | DateTimeOffset | No | UTC |
| `RetentionExpiresAt` | DateTimeOffset | No | UTC |
| `InputPresenceFlags` | int | Yes | Bitmask: supplier/subscription/pricing/stripe/mappings |

**RowKey design**: Inverted ticks prefix enables `RowKey ge '{fromTicks}' and RowKey le '{toTicks}'` for date-range listing without secondary index.

---

## Entity: Input Metadata (`PartitionKey = "input"`)

One row per `(RunId, Domain)`.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `PartitionKey` | string | Yes | Constant `"input"` |
| `RowKey` | string | Yes | `{runId:N}:{domain}` |
| `RunId` | string | Yes | GUID |
| `Domain` | string | Yes | `InputDomainType` |
| `IsPresent` | bool | Yes | Data available |
| `SourceFileName` | string | No | Original filename |
| `UploadedAt` | DateTimeOffset | No | UTC |
| `ContentFingerprint` | string | No | Source file SHA-256 |
| `BillingPeriodStart` | string | No | ISO date |
| `BillingPeriodEnd` | string | No | ISO date |
| `RecordCount` | int | Yes | Normalized count |
| `BlobPath` | string | No | Relative blob path |
| `ContentHash` | string | No | Normalized blob SHA-256 |

---

## Entity: Drift Index (`PartitionKey = "drift"`)

Denormalized row for trend queries. One row per `(RunId, StableMismatchKey)`.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `PartitionKey` | string | Yes | Constant `"drift"` |
| `RowKey` | string | Yes | `{stableKeyHash}:{runId:N}` |
| `RunId` | string | Yes | GUID |
| `StableMismatchKey` | string | Yes | Full key string |
| `StableKeyHash` | string | Yes | SHA-256 prefix (16 chars) for partition fan-out |
| `CustomerMexId` | string | No | Mex ID |
| `CommercialKeyRootJson` | string | No | Product identity |
| `MismatchType` | string | Yes | Enum name |
| `Severity` | string | Yes | Enum name |
| `MismatchId` | string | Yes | Run-scoped GUID |
| `CompletedAt` | DateTimeOffset | Yes | From parent run |
| `DescriptionSummary` | string | Yes | Truncated description (max 256) |

**Trend query**: Filter `StableMismatchKey eq '{key}'` across runs, or scan by `StableKeyHash` prefix + aggregate in Application.

---

## Entity: Audit (`PartitionKey = "audit"`)

Append-only.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `PartitionKey` | string | Yes | Constant `"audit"` |
| `RowKey` | string | Yes | `{runId:N}:{utcTicks:D19}:{eventId:N}` |
| `EventType` | string | Yes | Enum |
| `RunId` | string | Yes | GUID |
| `OperatorId` | string | No | Actor |
| `Timestamp` | DateTimeOffset | Yes | UTC |
| `Summary` | string | Yes | Readable summary |
| `PayloadJson` | string | No | Extended detail |

---

## Access Patterns

| Operation | Pattern | Frequency |
|-----------|---------|-----------|
| List runs (recent first) | Query `run` partition, order by RowKey desc | Operator landing page |
| Filter by billing period | Query `run` + filter `BillingPeriodStart/End` | Monthly review |
| Get run summary | Point read `run` by RowKey or filter `RunId` | Detail header |
| Get input metadata | Query `input` partition `RunId eq '{id}'` | Detail inputs tab |
| Drift trend by key | Query `drift` `StableMismatchKey eq '{key}'` | Trend analysis |
| Drift trend window | Query `drift` + filter `CompletedAt` range | Trend page |
| Audit trail | Query `audit` `RunId eq '{id}'` | Audit view |

---

## Limits & Mitigations

| Limit | Mitigation |
|-------|------------|
| 1 MB entity size | Large payloads in blobs only |
| Table query pagination | Continuation tokens in API; default page size 50 runs |
| Hot partition on `run` | Single-tenant v1 acceptable; shard partition key if multi-tenant later |

---

## Options

```csharp
public sealed class RunHistoryStorageOptions
{
    public string TableName { get; set; } = "reconciliationrunhistory";
    public string BlobContainerName { get; set; } = "reconciliation-runs";
    public int DefaultRetentionMonths { get; set; } = 24;
}
```
