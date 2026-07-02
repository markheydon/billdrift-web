# Contract: Azure Table Schema — Approval Workflow

**Feature**: `007-reconciliation-approval-workflow`  
**Storage**: Azure Table Storage via Aspire-injected `TableServiceClient`  
**Date**: 2026-07-02

## Table

**Default name**: `reconciliationapprovals` (override via `ApprovalStorageOptions.TableName`)

Create table idempotently on first store access via `AzureTableApprovalStore.EnsureTableAsync()`.

**Client registration** (API only):

```csharp
builder.AddAzureTableServiceClient("tables");
```

Store constructor: `AzureTableApprovalStore(TableServiceClient client, IOptions<ApprovalStorageOptions> options)` — **no manual connection strings**.

---

## Entity: Proposal (`PartitionKey = "proposal"`)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `PartitionKey` | string | Yes | Constant `"proposal"` |
| `RowKey` | string | Yes | `{runId}:{idempotencyKey}` (URL-safe) |
| `ProposalId` | string | Yes | GUID string |
| `RunId` | string | Yes | Run GUID |
| `ProposedChangeId` | string | No | GUID or empty |
| `IdempotencyKey` | string | Yes | Domain key string |
| `MismatchId` | string | No | GUID or empty |
| `Category` | string | Yes | Enum name |
| `ActionType` | string | No | Enum name or empty |
| `State` | string | Yes | `ApprovalDecisionState` |
| `Eligibility` | string | Yes | `ApprovalEligibility` |
| `EligibilityReason` | string | No | Block explanation |
| `CustomerMexId` | string | Yes | Mex ID |
| `ProductLabel` | string | Yes | Display name |
| `CommercialKeyRootJson` | string | No | Serialized root |
| `PriorValuesJson` | string | Yes | JSON object |
| `ProposedValuesJson` | string | Yes | JSON object |
| `ExecutionOrder` | int | Yes | Sort key |
| `DependsOnJson` | string | No | JSON array of proposal GUIDs |
| `RiskIndicator` | string | No | Enum name |
| `IngestedAt` | DateTimeOffset | Yes | UTC |
| `SupersededByRunId` | string | No | Run GUID |
| `ApprovedWhileEligible` | bool | No | Set true on approve when eligibility was Eligible |
| `LastOperatorId` | string | No | Last actor |
| `LastUpdatedAt` | DateTimeOffset | Yes | UTC |

**RowKey format**: `{runId:N}:{Uri.EscapeDataString(idempotencyKey)}` — max 1024 chars.

---

## Entity: Decision (`PartitionKey = "decision"`)

Append-only. **Never update or delete.**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `PartitionKey` | string | Yes | Constant `"decision"` |
| `RowKey` | string | Yes | `{runId}:{proposalId}:{utcTicks:D19}` |
| `ProposalId` | string | Yes | GUID |
| `RunId` | string | Yes | GUID |
| `PriorState` | string | Yes | Enum |
| `NewState` | string | Yes | Enum |
| `OperatorId` | string | Yes | Actor |
| `DecidedAt` | DateTimeOffset | Yes | UTC |
| `RejectionReason` | string | No | Required when rejected |
| `AcknowledgedStale` | bool | Yes | Stale approval flag |
| `PriorValuesJson` | string | No | Snapshot at decision |
| `ProposedValuesJson` | string | No | Snapshot at decision |

**Query**: `PartitionKey eq 'decision' and RunId eq '{runId}'` — order by RowKey desc.

---

## Entity: Audit (`PartitionKey = "audit"`)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `PartitionKey` | string | Yes | Constant `"audit"` |
| `RowKey` | string | Yes | `{runId}:{utcTicks:D19}:{eventId:N}` |
| `EventType` | string | Yes | Enum |
| `RunId` | string | Yes | GUID |
| `ProposalId` | string | No | GUID |
| `OperatorId` | string | No | Actor |
| `Timestamp` | DateTimeOffset | Yes | UTC |
| `Summary` | string | Yes | Readable summary |
| `PayloadJson` | string | No | Extended detail |

---

## Entity: Export Metadata (`PartitionKey = "export"`)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `PartitionKey` | string | Yes | Constant `"export"` |
| `RowKey` | string | Yes | `{runId}:{exportId:N}` |
| `RunId` | string | Yes | GUID |
| `ExportId` | string | Yes | GUID |
| `ExportedAt` | DateTimeOffset | Yes | UTC |
| `ExportedBy` | string | Yes | Operator |
| `BlobPath` | string | Yes | `{runId}/{exportId}.json` |
| `EntryCount` | int | Yes | Approved entry count |
| `ContentHash` | string | Yes | SHA-256 of blob for idempotent re-export check |

---

## Access Patterns

| Operation | Pattern | Frequency |
|-----------|---------|-----------|
| List queue by run | `PartitionKey eq 'proposal' and RunId eq '{id}'` | Operator page load |
| Get proposal | Point read by RowKey | Approve/reject |
| Append decision | Insert `decision` row | Each decision |
| Audit history | Query `audit` or `decision` by RunId | Review |
| Export list | Query `export` partition by RunId | Export history |

**Note**: Store `RunId` as a queryable property on proposal entities (duplicate of RowKey prefix) for OData filter support.

---

## Concurrency

- Proposal upsert uses ETag optimistic concurrency on ingest
- Approve/reject: read proposal ETag → validate state → upsert with If-Match → append decision
- Conflict → 409 to client for retry

---

## No SQL

All operational approval data lives in this table. No EF Core, no SQL Server, no SQLite for production paths.
