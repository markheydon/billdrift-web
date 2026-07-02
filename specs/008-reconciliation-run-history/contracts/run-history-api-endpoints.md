# Contract: Run History API Endpoints

**Feature**: `008-reconciliation-run-history`  
**Project**: `BillDrift.Api/History/RunHistoryEndpoints.cs`  
**Date**: 2026-07-02

## Base Path

```text
/api/run-history
```

All endpoints require operator context (`IOperatorContext.OperatorId`). Development: `X-Operator-Id` header.

---

## GET `/`

List reconciliation run records.

**Query**:
- `billingPeriodStart` (optional, ISO date)
- `billingPeriodEnd` (optional, ISO date)
- `fromDate` / `toDate` (optional, ISO datetime)
- `status` (optional: `Completed`, `Failed`, `InProgress`)
- `cleanRunsOnly` (optional bool)
- `includeArchived` (default `false`)
- `pageSize` (default 50, max 100)
- `continuationToken` (optional)

**Response** `200`:

```json
{
  "items": [
    {
      "runId": "guid",
      "status": "Completed",
      "billingPeriod": { "start": "2026-06-01", "end": "2026-06-30" },
      "completedAt": "2026-07-02T10:00:00Z",
      "initiatorId": "operator@example.com",
      "mismatchCount": 12,
      "proposedChangeCount": 8,
      "cleanRun": false,
      "inputPresence": {
        "supplierCost": true,
        "subscriptionTruth": true,
        "intendedPricing": true,
        "stripeBilling": true,
        "productMappings": true
      },
      "isArchived": false
    }
  ],
  "continuationToken": "..."
}
```

---

## GET `/{runId:guid}`

Get run detail with input metadata, summary metrics, and proposal status links.

**Query**:
- `includeResults` (default `false`) — when true, include mismatches and proposals from blob
- `includeMatchGroups` (default `false`)

**Response** `200`: `RunDetailViewModel`  
**Response** `404`: Not found

```json
{
  "runId": "guid",
  "status": "Completed",
  "billingPeriod": { "start": "2026-06-01", "end": "2026-06-30" },
  "mappingVersion": { "versionId": "2026-07-02", "contentHash": "sha256:...", "effectiveDate": "2026-07-02" },
  "inputSnapshots": [ /* InputSnapshotMetadata[] */ ],
  "summaryMetrics": { "matchGroupCount": 85, "mismatchCount": 12, "proposedChangeCount": 8 },
  "proposalStatusLinks": [ /* joined from 007 */ ],
  "executionOutcomes": []
}
```

---

## POST `/`

Persist a completed reconciliation run (typically called from reconciliation orchestration).

**Request body**:

```json
{
  "run": { /* ReconciliationRun serialized */ },
  "context": {
    "initiatorId": "operator@example.com",
    "startedAt": "2026-07-02T09:55:00Z",
    "mappingVersion": { "versionId": "2026-07-02", "contentHash": "...", "effectiveDate": "2026-07-02" },
    "inputMetadata": {
      "supplierCost": { "isPresent": true, "sourceFileName": "...", "contentFingerprint": "..." }
    }
  }
}
```

**Response** `201`: `ReconciliationRunRecord`  
**Response** `409`: Run already archived (`Completed`)

**Note**: Prefer internal service call from reconciliation endpoint; exposed for testing and manual archive.

---

## GET `/{runId:guid}/inputs/{domain}`

Get normalized input snapshot for one domain.

**Path `domain`**: `supplier-cost` | `subscription-truth` | `intended-pricing` | `stripe-billing` | `product-mappings`

**Response** `200`: Input blob payload  
**Response** `404`: Run or domain not found / not present

---

## POST `/compare`

Compare two stored runs.

**Request body**:

```json
{
  "earlierRunId": "guid",
  "laterRunId": "guid",
  "includeInputDeltas": true,
  "includeProposalStatus": true
}
```

**Response** `200`: `RunComparisonReport`  
**Response** `404`: Either run not found  
**Response** `422`: Runs not comparable (e.g. both failed with no results)

---

## GET `/trends/drift`

Recurring mismatch trends over a time window.

**Query**:
- `fromDate` (required)
- `toDate` (required)
- `minOccurrences` (default 2)
- `mismatchType` (optional filter)
- `customerMexId` (optional filter)

**Response** `200`:

```json
{
  "entries": [ /* DriftTrendEntry[] */ ],
  "windowStart": "2026-01-01T00:00:00Z",
  "windowEnd": "2026-07-02T00:00:00Z"
}
```

---

## GET `/trends/pricing`

Pricing drift timeline for a commercial key.

**Query**:
- `offerId` (required)
- `skuId` (required)
- `term` (required)
- `frequency` (required)
- `fromDate` / `toDate` (required)

**Response** `200`:

```json
{
  "commercialKey": { "offerId": "...", "skuId": "...", "term": "P1Y", "frequency": "Annual" },
  "entries": [ /* PricingDriftTimelineEntry[] */ ]
}
```

---

## GET `/{runId:guid}/audit`

Audit events for a run.

**Response** `200`: `{ "events": [ /* audit events */ ] }`

---

## POST `/compare/export`

Export comparison report to blob (optional operator artifact).

**Request body**: Same as `/compare`

**Response** `200`:

```json
{
  "exportBlobPath": "{runId}/exports/comparison-....json",
  "contentHash": "sha256:..."
}
```

---

## Error Shape

Consistent with 007 approval endpoints:

```json
{
  "title": "Run already archived",
  "status": 409,
  "detail": "Run {runId} has status Completed and cannot be re-persisted."
}
```

---

## Service Registration

```csharp
builder.Services.AddRunHistoryStorage(builder.Configuration);
builder.Services.AddRunHistoryApplication();
// ...
app.MapRunHistoryEndpoints();
```

Storage extension follows `ApprovalStorageExtensions` pattern — registers Azure stores when not in test override mode.
