# Contract: Approval Workflow Integration

**Feature**: `012-stripe-catalogue-reconciliation` → `007-reconciliation-approval-workflow`  
**Date**: 2026-07-03

## Purpose

Defines how catalogue proposed fixes enter the existing human approval queue without duplicating decision state. Approval store persistence uses the same Azure Blob + Table pattern (007) via Aspire DI — no SQL.

---

## Adapter: `CatalogueApprovalAdapter`

### Input

- `CatalogueReconciliationRun` with `ProposedFixes` where `IsActionable == true`
- Source `CatalogueRunId` for audit linkage

### Output

- `IReadOnlyList<ApprovalProposal>` suitable for `IApprovalStore.UpsertProposalsAsync`

---

## Field Mapping

| Catalogue field | Approval field |
|-----------------|----------------|
| `CatalogueProposedFix.Id` | Generate new `ApprovalProposalId`; store catalogue fix ID in metadata |
| `CatalogueRunId` | Stored as `SourceCatalogueRunId` on proposal metadata |
| `ActionType` → `CreateOrUpdateCatalogueEntry` | `ApprovalProposalCategory.Catalogue` |
| `FlagManualCleanup` | `ApprovalProposalCategory.Investigation`, `ApprovalEligibility.CatalogueConflict` |
| `IdempotencyKey` | Direct copy |
| `PriorState` / `ProposedState` | Direct copy to proposal dictionaries |
| `CommercialKeyRoot` | `CommercialKeyRoot` on proposal |
| `Rationale` | `EligibilityReason` or description field |

---

## Idempotency Key Format

```text
catalogue:{CatalogueRunId}:{CommercialKeyRoot}:{ActionType}:{Frequency?}
```

Ensures supersession when a newer catalogue run produces updated proposals.

---

## Eligibility Rules (reuse 007)

| Condition | Eligibility |
|-----------|-------------|
| `CreateProduct`, `CreatePrice`, `CreateReplacementPrice` | `Eligible` |
| `FlagManualCleanup` | `CatalogueConflict` — no export as bill-impacting action |
| Duplicate conflict on same root | `InvestigationOnly` until manual cleanup acknowledged |

---

## Export Ordering

Catalogue proposals retain execution order `10` per 007 contract — catalogue before subscription corrections when both are ingested for the same operator session.

---

## Audit Events

Record `ApprovalAuditEventType.Ingest` with payload:
- `sourceKind`: `CatalogueReconciliation`
- `catalogueRunId`
- `ingestedCount`

---

## Test Contract

1. Ingest 3 actionable + 1 manual-only fix → 3 approval proposals created
2. Re-ingest same run → 0 new proposals (idempotent)
3. Export approved changeset includes only `Eligible` catalogue proposals
4. `CatalogueConflict` proposals excluded from export
