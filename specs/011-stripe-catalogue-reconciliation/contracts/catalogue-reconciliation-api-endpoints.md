# Contract: Catalogue Reconciliation API Endpoints

**Feature**: `011-stripe-catalogue-reconciliation`  
**Project**: `BillDrift.Api`  
**Date**: 2026-07-03

## Base Path

`/api/catalogue-reconciliation`

Authentication: same as existing BillDrift API routes (operator context via `IOperatorContext`).

---

## `POST /runs`

Trigger a catalogue reconciliation run.

### Request body

```json
{
  "stripeIngestionRunId": "00000000-0000-0000-0000-000000000001",
  "pricingIngestionRunId": "00000000-0000-0000-0000-000000000002",
  "productMappings": [ /* ProductMapping[] — inline snapshot */ ],
  "options": {
    "includeArchivedPrices": false,
    "includeNonCspProducts": true,
    "exactAmountMatch": true,
    "defaultCurrency": "GBP"
  },
  "ingestToApprovalQueue": false
}
```

Either inline `productMappings` OR `mappingBlobPath` (future) required.

### Response `201 Created`

```json
{
  "catalogueRunId": "00000000-0000-0000-0000-000000000003",
  "executedAt": "2026-07-03T12:00:00Z",
  "summary": {
    "mappedProductsChecked": 95,
    "exceptionsByType": {
      "missingProduct": 2,
      "missingPrice": 3,
      "incorrectPrice": 1,
      "duplicateProduct": 1,
      "duplicatePrice": 0
    },
    "proposedFixesActionable": 6,
    "proposedFixesManualOnly": 1
  },
  "approvalIngestionRunId": null
}
```

### Errors

| Status | When |
|--------|------|
| `400` | Missing ingestion run IDs; empty catalogue snapshot |
| `404` | Referenced ingestion run not found in blob archive |
| `422` | Validation failure (unreadable mapping JSON) |

---

## `GET /runs`

List catalogue reconciliation runs (paginated).

### Query parameters

| Param | Type | Default |
|-------|------|---------|
| `limit` | int | 20 |
| `continuation` | string | — |

### Response `200 OK`

```json
{
  "runs": [
    {
      "catalogueRunId": "...",
      "executedAt": "...",
      "totalExceptions": 7,
      "missingProductCount": 2,
      "actionableFixCount": 5,
      "stripeIngestionRunId": "...",
      "pricingIngestionRunId": "..."
    }
  ],
  "continuation": null
}
```

---

## `GET /runs/{catalogueRunId}`

Run detail with exception and proposed fix lists (from blob or denormalized cache).

### Response `200 OK`

```json
{
  "catalogueRunId": "...",
  "executedAt": "...",
  "inputReferences": { },
  "summary": { },
  "exceptions": [ ],
  "proposedFixes": [ ]
}
```

---

## `POST /runs/{catalogueRunId}/ingest-approvals`

Ingest proposed fixes from a catalogue run into approval workflow (007).

### Response `200 OK`

```json
{
  "ingestedCount": 6,
  "skippedManualOnly": 1,
  "approvalRunReference": "catalogue-run:{catalogueRunId}"
}
```

Idempotent: re-ingest skips already-ingested idempotency keys.

---

## Web Integration (deferred)

`BillDrift.Web` will consume these endpoints via `ICatalogueReconciliationApiClient` in a follow-on task. v1 validation uses API + quickstart scenarios.
