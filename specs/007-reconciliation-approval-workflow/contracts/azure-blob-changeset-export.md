# Contract: Azure Blob — Approved Changeset Export

**Feature**: `007-reconciliation-approval-workflow`  
**Storage**: Azure Blob Storage via Aspire-injected `BlobServiceClient`  
**Date**: 2026-07-02

## Container

**Default name**: `approved-changesets` (override via `ApprovalStorageOptions.ChangesetContainerName`)

Create container idempotently on first export.

**Client registration** (API only):

```csharp
builder.AddAzureBlobServiceClient("blobs");
```

Exporter: `AzureBlobChangesetExporter(BlobServiceClient client, IOptions<ApprovalStorageOptions> options)` — **no manual connection string construction**.

---

## Blob Path

```text
{runId}/{exportId}.json
```

Example: `a1b2c3d4-.../f9e8d7c6-....json`

---

## JSON Schema (v1)

```json
{
  "schemaVersion": 1,
  "exportId": "guid",
  "runId": "guid",
  "exportedAt": "2026-07-02T12:00:00Z",
  "exportedBy": "operator-id",
  "contentHash": "sha256-hex",
  "entries": [
    {
      "proposalId": "guid",
      "idempotencyKey": "string",
      "actionType": "UpdateQuantity",
      "category": "Subscription",
      "customerMexId": "MEX123",
      "productLabel": "Microsoft 365 Business Premium",
      "priorValues": { "quantity": "5" },
      "proposedValues": { "quantity": "10" },
      "approvedAt": "2026-07-02T11:30:00Z",
      "approvedBy": "operator-id",
      "executionOrder": 100
    }
  ]
}
```

---

## Field Rules

| Field | Rule |
|-------|------|
| `schemaVersion` | Always `1` for initial release |
| `entries` | Non-empty array; only approved actions |
| `idempotencyKey` | Must match domain `IdempotencyKey` string form |
| `actionType` | `ProposedActionType` enum name |
| `priorValues` / `proposedValues` | String dictionary; operator-readable keys |
| Order | Array sorted by `executionOrder` asc, catalogue entries before subscription when order equal |

---

## Export Idempotency

Re-export with **unchanged** approved set:
- Same `contentHash` → return existing blob reference (no duplicate write) OR write new export row with same content (configurable; default: new export row, same hash noted)

Re-export after **new** approvals:
- New `exportId`, new blob, new hash

---

## Manual Apply Consumption

Operators use export as checklist:

1. Download JSON from API `/export/{exportId}/download` or Aspire blob link (dev)
2. Apply catalogue entries first (create product/price)
3. Apply subscription changes (create item, quantity, price switch)
4. Record manual completion outside system (future apply feature consumes same artifact)

---

## Security

- Blob access via API proxy in production (no public container)
- No Stripe secrets in blob content
- Operator identity in metadata only

---

## Size Limits

Typical changeset < 100 entries (< 500 KB). If run exceeds 1000 entries, split export by customer via `ExportChangesetCommand.CustomerMexId` filter (API optional parameter).
