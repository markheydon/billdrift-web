# Quickstart: Reconciliation Change Approval Workflow

**Feature**: `007-reconciliation-approval-workflow`  
**Date**: 2026-07-02

## Prerequisites

- .NET 10 SDK
- Repository built: `dotnet build BillDrift.sln`
- Aspire AppHost with Azure Storage emulator (Azurite) — tables + blobs wired in `AppHost.cs`
- Features 001–006 implemented (domain, ingestion, reconciliation engine, exception surfacing, classification)
- Fluent UI package added to `BillDrift.Web` per [fluent-ui-integration.md](./contracts/fluent-ui-integration.md)

## Run Tests (primary validation)

```powershell
# Approval domain/application unit tests
dotnet test tests/BillDrift.Application.Tests --filter "FullyQualifiedName~Approval"

# Infrastructure table/blob store tests (Azurite)
dotnet test tests/BillDrift.Infrastructure.Tests --filter "FullyQualifiedName~ApprovalStorage"

# API integration tests
dotnet test tests/BillDrift.Api.Tests --filter "FullyQualifiedName~Approval"
```

## Validation Scenarios

### V1: Ingest creates pending proposals

**Fixture**: `tests/fixtures/approval/mixed-subscription-proposals.json`  
**Steps**: Run reconciliation → surface exceptions → `POST /api/reconciliation/{runId}/approvals/ingest`  
**Expected**: All eligible `ProposedChange` items in `Pending`; investigation items `InvestigationOnly`

### V2: Approve subscription quantity update

**Fixture**: `tests/fixtures/approval/quantity-mismatch-proposal.json`  
**Steps**: Ingest → `POST .../approve` on quantity proposal  
**Expected**: State `Approved`; decision audit row; `ApprovedWhileEligible = true`

### V3: Reject requires reason

**Steps**: `POST .../reject` with empty body  
**Expected**: HTTP 400  
**Steps**: Reject with reason  
**Expected**: State `Rejected`; reason in audit

### V4: Investigation item cannot approve

**Fixture**: `tests/fixtures/approval/mapping-ambiguous-investigation.json`  
**Steps**: Attempt approve  
**Expected**: HTTP 422; state remains `Pending`

### V5: Export contains only approved items

**Setup**: Approve 2 of 4 pending; reject 1  
**Steps**: `POST .../export`  
**Expected**: Blob JSON with exactly 2 entries; pending/rejected/investigation absent

### V6: Export deterministic ordering

**Fixture**: Catalogue + subscription approved  
**Expected**: Catalogue entry appears before subscription when `ExecutionOrder` equal

### V7: Supersession on re-run

**Steps**:
1. Ingest run R1; approve proposal P
2. Ingest run R2 with same idempotency key, changed values

**Expected**: R1 proposal `Historical`; R2 proposal `Pending`; R1 decision audit unchanged

### V8: Stale pending blocked without acknowledgment

**Steps**: Ingest R2 without approving R1 pending (superseded to Stale)  
**Expected**: Approve without `acknowledgeStale: true` → 409

### V9: Bulk approve with confirmation

**Steps**: Preview → bulk approve with token  
**Expected**: All eligible pending → Approved; invalid token → 409

### V10: No auto-approve on ingest

**Steps**: Ingest any fixture  
**Expected**: Zero proposals in `Approved` without operator action (SC-002)

## Local Aspire Run

```powershell
cd src/BillDrift.AppHost
dotnet run
```

1. Open Web frontend URL from Aspire dashboard  
2. Navigate to `/approvals/{runId}` (use run ID from test ingest or API)  
3. Verify Fluent layout renders (no Bootstrap sidebar on approval page)  
4. Approve/reject via UI; export changeset

## API Smoke (curl)

```powershell
$runId = "<run-guid>"
$headers = @{ "X-Operator-Id" = "dev-operator" }

Invoke-RestMethod -Method Post -Uri "https://localhost:7xxx/api/reconciliation/$runId/approvals/ingest" -Headers $headers

Invoke-RestMethod -Method Get -Uri "https://localhost:7xxx/api/reconciliation/$runId/approvals" -Headers $headers
```

Replace port with API endpoint from Aspire dashboard.

## Success Criteria Mapping

| SC | Validated by |
|----|--------------|
| SC-002 | V10 |
| SC-003 | V5 |
| SC-004 | V3 |
| SC-005 | V4, V5 |
| SC-008 | V7 |
| SC-010 | V8 |

## Related Artifacts

- [Data model](./data-model.md)
- [Approval pipeline](./contracts/approval-workflow-pipeline.md)
- [Table schema](./contracts/azure-table-schema.md)
- [Blob export](./contracts/azure-blob-changeset-export.md)
- [API endpoints](./contracts/approval-api-endpoints.md)
- [Fluent UI integration](./contracts/fluent-ui-integration.md)

## Implementation Validation Checklist (2026-07-02)

| Scenario | Status | Notes |
|----------|--------|-------|
| V1 Ingest pending | PASS | `ApprovalIngestionServiceTests` |
| V2 Approve quantity | PASS | `ApprovalServiceDecisionTests` |
| V3 Reject requires reason | PASS | `ApprovalServiceDecisionTests` |
| V4 Investigation ineligible | PASS | `ApprovalEligibilityEvaluatorTests` |
| V5 Export approved only | PASS | `ApprovedChangesetBuilderTests` |
| V6 Deterministic ordering | PASS | `ApprovedChangesetBuilderTests` |
| V7 Supersession | PASS | `ApprovalServiceDecisionTests` |
| V8 Stale acknowledgment | PASS | `ApprovalService.ApproveAsync` validation |
| V9 Bulk approve token | PASS | `ApprovalService` preview/hash |
| V10 No auto-approve | PASS | `ApprovalIngestionServiceTests` |

`dotnet build` — 0 errors, 0 warnings. `dotnet test` — 167/168 passed; AppHost integration test requires Docker/Azurite (environment).

## Next Step

Feature implementation complete. Run Aspire AppHost locally with Docker for end-to-end UI validation.
