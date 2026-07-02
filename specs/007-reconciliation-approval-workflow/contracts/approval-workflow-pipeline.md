# Contract: Approval Workflow Pipeline

**Feature**: `007-reconciliation-approval-workflow`  
**Projects**: `BillDrift.Application.Approval`, `BillDrift.Api`, `BillDrift.Web`  
**Date**: 2026-07-02

## Purpose

Defines the ingest → review → decide → export pipeline for human-in-the-loop approval of reconciliation corrective actions. **No Stripe mutations** occur in this pipeline.

---

## Entry Points

```text
ApprovalService.IngestAsync(ApprovalIngestionRequest request) → ApprovalIngestionResult
ApprovalService.GetQueueAsync(RunId runId, ApprovalQueueOptions? options) → ApprovalQueueViewModel
ApprovalService.ApproveAsync(ApproveProposalCommand command) → ApprovalDecision
ApprovalService.RejectAsync(RejectProposalCommand command) → ApprovalDecision
ApprovalService.BulkApproveAsync(BulkApproveCommand command) → BulkApproveResult
ApprovalService.ExportApprovedChangesetAsync(ExportChangesetCommand command) → ApprovedChangeset
ApprovalService.GetAuditHistoryAsync(RunId runId, ApprovalProposalId? proposalId) → IReadOnlyList<ApprovalAuditEvent>
```

**Preconditions (ingest)**:
- `request.Run` is immutable and fully constructed
- Idempotent: re-ingest same `RunId` replaces proposal snapshots but preserves non-superseded decisions per R7

**Postconditions (ingest)**:
- Every eligible `ProposedChange` on run has corresponding `ApprovalProposal` in `Pending` or `Historical`/`Stale` as applicable
- Investigation exceptions without proposals ingested as `InvestigationOnly`
- Audit event `Ingest` appended

**Postconditions (approve/reject)**:
- State transition valid per data-model state diagram
- Immutable `decision` row appended
- Audit event `Decision` or `BulkDecision` appended
- **No** Stripe API calls

**Postconditions (export)**:
- Only `Approved` proposals included
- Pending, Rejected, Stale, InvestigationOnly, CatalogueConflict excluded
- Deterministic entry order: `ExecutionOrder` asc, then `Catalogue` before `Subscription` at same order
- Blob written + `export` metadata row + audit event `Export`

---

## Phase 1: Ingest

**Component**: `ApprovalIngestionService`

1. Load existing proposals for prior runs sharing same customer scope (optional optimization: default: load by idempotency keys from current run
2. For each `ProposedChange` in `run.ProposedChanges`:
   - Build `ApprovalProposal` snapshot (prior/proposed values from mismatch + match group context)
   - Run `ApprovalEligibilityEvaluator`
   - Apply supersession rules (research R7)
   - Upsert `proposal` row
3. For surfaced exceptions without `ProposedChangeId` where category ∈ `{MappingIssue, MappingAmbiguous, NonCspManualReview}`:
   - Create investigation proposal (`InvestigationOnly`)
4. Append ingest audit event

---

## Phase 2: Eligibility Evaluation

**Component**: `ApprovalEligibilityEvaluator`

| Condition | Eligibility |
|-----------|-------------|
| Exception `RequiresActionNow == false` for linked mismatch | `InvestigationOnly` |
| Classification `Internal` or `CustomService` with missing-billing only | `InvestigationOnly` |
| Classification `NonCspSupplier` without manual mapping | `InvestigationOnly` |
| Match confidence Low/None | `InvestigationOnly` |
| Duplicate/conflict catalogue detector flag | `CatalogueConflict` |
| Prerequisite catalogue proposal not approved | `DependencyBlocked` |
| Otherwise | `Eligible` |

Revenue reduction heuristic: if proposed quantity or price lowers vs truth → set `RiskIndicator = RevenueReduction` (still eligible but flagged).

---

## Phase 3: Operator Decision

**Component**: `ApprovalService`

### Approve

**Valid from**: `Pending`, `Stale` (with `AcknowledgedStale = true`)  
**Invalid from**: `Approved`, `Rejected`, `Historical`  
**Requires**: `Eligibility == Eligible` OR explicit stale acknowledgment path  
**Writes**: Update proposal state, append decision, audit

### Reject

**Valid from**: `Pending`, `Stale`  
**Requires**: Non-empty `RejectionReason`  
**Writes**: Update proposal state, append decision, audit

### Bulk Approve

**Requires**: `ConfirmationToken` matching server-issued preview hash  
**Valid targets**: All specified IDs must be `Pending` + `Eligible`  
**On partial failure**: Transactional all-or-nothing (no partial bulk approve)

---

## Phase 4: Export

**Component**: `ApprovedChangesetBuilder` + `AzureBlobChangesetExporter`

1. Query proposals: `RunId` + `State == Approved`
2. Filter: was eligible at approval (stored `ApprovedWhileEligible` flag on proposal)
3. Sort entries (deterministic)
4. Serialize JSON schema v1 (see [azure-blob-changeset-export.md](./azure-blob-changeset-export.md))
5. Upload blob via injected `BlobServiceClient`
6. Save export metadata; return `ApprovedChangeset` with download path

---

## Error Handling

| Error | HTTP | Operator message |
|-------|------|------------------|
| Proposal not found | 404 | Proposal no longer exists |
| Invalid state transition | 409 | Cannot approve/reject in current state |
| Ineligible approve | 422 | Investigation or blocked item cannot be approved |
| Empty export | 422 | No approved items to export |
| Missing rejection reason | 400 | Rejection reason required |
| Stale without ack | 409 | Re-run superseded this proposal; acknowledge to proceed |

---

## Integration Points

| Upstream | Usage |
|----------|-------|
| `IReconciliationEngine` | Produces `ReconciliationRun` |
| `ExceptionSurfacingService` | Produces eligibility inputs |
| `ClassificationService` | Classification context for gating |

| Downstream | Usage |
|------------|-------|
| Manual Stripe apply runbook | Consumes blob JSON (future: automated apply service) |

---

## Determinism

Given identical run input, ingest produces identical proposal snapshots (except `IngestedAt`). Export order deterministic. Audit events ordered by timestamp + row key.
