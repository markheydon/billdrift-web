# Contract: Approval Ingest Convenience Endpoint

**Feature**: `013-v1-mvp-ui`  
**Project**: `BillDrift.Api`  
**Date**: 2026-07-03

## Purpose

Allow the UI to ingest reconciliation proposals into the approval queue without assembling the full `ApprovalIngestionRequest` payload client-side. Server loads persisted run data and invokes existing `ApprovalService.IngestAsync`.

---

## Route

```text
POST /api/reconciliation/{runId:guid}/approvals/ingest-from-run
```

Added to existing `ApprovalEndpoints` group.

---

## Request

**Body** (optional):

```json
{
  "includeInvestigationItems": true
}
```

---

## Behaviour

1. Load reconciliation run from run history blob archive (`IRunHistoryStore` + blob reads) **or** accept run from just-completed orchestration cache (implementation choice: prefer archive as source of truth)
2. Re-run `ExceptionSurfacingService` on loaded run if exception view model not stored
3. Load classification context if available
4. Assemble `ApprovalIngestionRequest(run, exceptions, classifications, includeInvestigationItems)`
5. Call `ApprovalService.IngestAsync`
6. Return `ApprovalIngestionResult`

**Response** `200 OK`: `ApprovalIngestionResult`

**Errors**:

| Status | Condition |
|--------|-----------|
| 404 | Run not found in history |
| 409 | Proposals already ingested (if idempotent guard applies) |
| 422 | Run has no proposals to ingest |

---

## Existing Endpoint (Unchanged)

```text
POST /api/reconciliation/{runId}/approvals/ingest
```

Full-payload ingest remains for programmatic/test use. UI uses `ingest-from-run` exclusively.

---

## UI Integration

Reconciliation results page and run detail "Proposals" tab show **"Send to approval queue"** button calling this endpoint. On success, navigate to `/approvals/{runId}`.

---

## Notes

- No Application-layer ingest contract change.
- Idempotency: second ingest may supersede stale proposals per 007 rules — surface counts in response.
