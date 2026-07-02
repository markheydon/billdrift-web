# Contract: Azure Blob Run Archive

**Feature**: `008-reconciliation-run-history`  
**Storage**: Azure Blob Storage via Aspire-injected `BlobServiceClient`  
**Date**: 2026-07-02

## Container

**Default name**: `reconciliation-runs` (override via `RunHistoryStorageOptions.BlobContainerName`)

Create container idempotently on first write via `AzureBlobRunArchiveStore.EnsureContainerAsync()`.

**Client registration** (API only):

```csharp
builder.AddAzureBlobServiceClient("blobs");
```

Store constructor: `AzureBlobRunArchiveStore(BlobServiceClient client, IOptions<RunHistoryStorageOptions> options)` — **no manual connection strings**.

---

## Path Layout

All paths relative to container root:

```text
{runId}/
├── manifest.json
├── inputs/
│   ├── supplier-cost.json
│   ├── subscription-truth.json
│   ├── intended-pricing.json
│   ├── stripe-billing.json
│   └── product-mappings.json
└── results/
    ├── match-groups.json
    ├── mismatches.json
    └── proposed-changes.json
```

`runId` = `RunId.Value.ToString("D")` (lowercase GUID with hyphens).

---

## Manifest Schema (`manifest.json`)

```json
{
  "schemaVersion": 1,
  "runId": "00000000-0000-0000-0000-000000000000",
  "archivedAt": "2026-07-02T12:00:00Z",
  "billingPeriod": { "start": "2026-06-01", "end": "2026-06-30" },
  "mappingVersion": {
    "versionId": "2026-07-02",
    "contentHash": "sha256:...",
    "effectiveDate": "2026-07-02"
  },
  "inputs": {
    "supplierCost": { "present": true, "blobPath": "inputs/supplier-cost.json", "contentHash": "sha256:...", "recordCount": 120 },
    "subscriptionTruth": { "present": true, "blobPath": "inputs/subscription-truth.json", "contentHash": "sha256:...", "recordCount": 95 },
    "intendedPricing": { "present": true, "blobPath": "inputs/intended-pricing.json", "contentHash": "sha256:...", "recordCount": 450 },
    "stripeBilling": { "present": true, "blobPath": "inputs/stripe-billing.json", "contentHash": "sha256:...", "recordCount": 88 },
    "productMappings": { "present": true, "blobPath": "inputs/product-mappings.json", "contentHash": "sha256:...", "recordCount": 320 }
  },
  "results": {
    "matchGroups": { "blobPath": "results/match-groups.json", "contentHash": "sha256:..." },
    "mismatches": { "blobPath": "results/mismatches.json", "contentHash": "sha256:..." },
    "proposedChanges": { "blobPath": "results/proposed-changes.json", "contentHash": "sha256:..." }
  },
  "summaryMetrics": {
    "matchGroupCount": 85,
    "mismatchCount": 12,
    "proposedChangeCount": 8,
    "cleanRun": false
  }
}
```

When a domain is absent, entry uses `{ "present": false, "recordCount": 0 }` with no blob path.

---

## Input Blob Payloads

Each input blob wraps metadata + normalized records:

```json
{
  "domain": "SupplierCost",
  "sourceMetadata": {
    "fileName": "giacom-june-2026.pdf",
    "uploadedAt": "2026-07-01T09:00:00Z",
    "contentFingerprint": "sha256:...",
    "billingPeriod": { "start": "2026-06-01", "end": "2026-06-30" }
  },
  "records": [ /* SupplierCostLine[] */ ]
}
```

**Serialization**: System.Text.Json with source-generated `RunHistoryJsonSerializerContext` referencing domain types from `BillDrift.Domain`.

**Stripe billing blob** includes both subscription items and embedded product/price catalogue entries extracted from `StripeBillingItem` collection (same shape reconciliation engine consumes).

---

## Results Blob Payloads

| Blob | Content |
|------|---------|
| `match-groups.json` | `{ "records": EntityMatchGroup[] }` |
| `mismatches.json` | `{ "records": Mismatch[] }` |
| `proposed-changes.json` | `{ "records": ProposedChange[] }` |

Each file includes top-level `contentHash` field matching manifest (computed over canonical JSON bytes).

---

## Write Protocol

1. Write all input + results blobs with `Content-Type: application/json`
2. Compute SHA-256 over UTF-8 bytes (canonical serialization — no pretty-print variance)
3. Write `manifest.json` last (commit marker)
4. Set blob metadata: `runid`, `status`, `schemaversion`

**Atomicity**: If manifest missing on read, treat run as `InProgress` or `Failed`.

---

## Read Protocol

1. Read `manifest.json`; verify `contentHash` entries
2. Load requested blobs only (partial read for pricing drift)
3. On hash mismatch → throw `RunArchiveIntegrityException`

---

## Retention / Tiering

When `IsArchived=true` on table row:
- Set blob access tier to `Cool` (configurable)
- Blobs remain at same paths — no path change on archive

---

## Export Blobs (optional)

Comparison/trend exports may be written to:

```text
{runId}/exports/comparison-{earlierRunId}-vs-{laterRunId}.json
{runId}/exports/drift-trends-{windowStart}-{windowEnd}.json
```

Referenced from audit events; not required for core persist flow.

---

## Size Guidance

| Blob | Typical size |
|------|-------------|
| Input domains | 50 KB – 2 MB each |
| Results | 100 KB – 5 MB total |
| Manifest | < 4 KB |

Runs exceeding 50 MB total SHOULD log warning; no hard reject in v1.

---

## Comparison Export Schema (on-demand)

Written when operator exports comparison report:

```json
{
  "schemaVersion": 1,
  "generatedAt": "2026-07-02T12:00:00Z",
  "earlierRunId": "...",
  "laterRunId": "...",
  "exceptionDeltas": { "new": [], "resolved": [], "persisting": [] },
  "inputChangeSummaries": []
}
```
