# Research: Reconciliation Change Approval Workflow

**Feature**: `007-reconciliation-approval-workflow`  
**Date**: 2026-07-02

## R1: Approval state vs ephemeral reconciliation output

**Decision**: Persist **approval proposals** as durable entities keyed by `IdempotencyKey` (from domain `ProposedChange`) plus `RunId` lineage. Reconciliation runs produce ephemeral `ProposedChange` lists; the approval workflow **snapshots** each eligible proposal into storage on ingest and tracks `Pending` / `Approved` / `Rejected` / `Stale` / `Historical` independently of the in-memory run object.

**Rationale**: FR-002–FR-015 require durable decisions, audit immutability, and supersession on re-run. Ephemeral engine output alone cannot satisfy export or audit requirements after the run object is discarded.

**Alternatives considered**:
- **Re-derive state from latest run only** — Rejected: loses approval decisions when inputs change; violates audit immutability.
- **SQL relational model** — Rejected: user constraint — Azure Tables only for v1.

---

## R2: Investigation and ineligible proposals

**Decision**: Introduce `ApprovalEligibility` on each persisted proposal:

| Value | Meaning |
|-------|---------|
| `Eligible` | Operator may approve/reject for export |
| `InvestigationOnly` | Display-only; mapping ambiguous, low confidence, or policy block |
| `CatalogueConflict` | Duplicate/conflict flag; no destructive apply path |
| `Superseded` | Replaced by newer run proposal |

Investigation items **without** a `ProposedChange` (mapping-only exceptions) are ingested as synthetic proposals with `ActionType = None` and `Eligibility = InvestigationOnly`. Items with `ProposedChange` but blocked by classification/mapping use `InvestigationOnly` with the underlying change retained for context but export blocked.

**Rationale**: Spec FR-007, FR-008, FR-016 require non-approvable investigation and conflict flags while still presenting them in the queue. Reuses exception surfacing eligibility logic (005) rather than adding a new `ProposedActionType`.

**Alternatives considered**:
- **New `ProposedActionType.FlagForInvestigation`** — Rejected: pollutes Stripe action enum with non-action; engine does not produce Stripe mutations for these cases.
- **Separate investigation queue table** — Rejected: splits operator UX; spec requires unified queue with distinguishable types.

---

## R3: Layer placement

**Decision**: Three-layer split mirroring classification (006):

1. **Domain** — `ApprovalProposal`, `ApprovalDecision`, `ApprovalDecisionState`, `ApprovalEligibility`, `ApprovedChangeset`, `ApprovedChangesetEntry`, `ApprovalAuditEvent`
2. **Application** — `ApprovalService`, `ApprovalIngestionService`, `ApprovalEligibilityEvaluator`, `ApprovedChangesetBuilder`, `IApprovalStore`
3. **Infrastructure** — `AzureTableApprovalStore`, `AzureBlobChangesetExporter` using DI-injected `TableServiceClient` and `BlobServiceClient`
4. **API** — Minimal REST endpoints under `BillDrift.Api/Approval`
5. **Web** — Fluent UI Blazor pages in `BillDrift.Web` consuming API (first UI feature)

**Rationale**: Billing-critical decision logic testable without Azure; storage isolated; UI separated from domain per existing solution structure.

**Alternatives considered**:
- **Approval logic inside exception surfacing** — Rejected: surfacing is read-only view model (005 FR-020); approval has write state and export side effects.
- **UI-only state in Blazor** — Rejected: violates audit persistence and API-first design for future automation.

---

## R4: Azure Table schema design

**Decision**: Single table `reconciliationapprovals` (configurable via `ApprovalStorageOptions.TableName`) with partition strategies:

| Partition | RowKey | Purpose |
|-----------|--------|---------|
| `proposal` | `{runId}:{idempotencyKey}` | Current proposal state |
| `decision` | `{runId}:{idempotencyKey}:{utcTicks:D19}` | Immutable decision audit |
| `export` | `{runId}:{exportId}` | Export metadata (blob pointer) |

Proposal entity stores JSON snapshots of prior/proposed values, customer/product context, eligibility, and decision state. Decision rows append-only. No updates to decision partition rows.

**Rationale**: Matches established 006 table pattern; supports FR-014/FR-015 immutability; point reads by run + key for queue views.

**Alternatives considered**:
- **Separate tables per entity** — Rejected: unnecessary for v1 scale; increases Aspire wiring complexity without benefit.
- **Blob-only persistence** — Rejected: poor query pattern for pending queue by customer; tables optimized for keyed lookups.

---

## R5: Approved changeset export (Blob)

**Decision**: Export writes deterministic JSON to blob container `approved-changesets` (default) at path `{runId}/{exportId}.json`. Export metadata row in table `export` partition. Content includes only `Approved` + `Eligible` entries ordered by `ExecutionOrder` then catalogue-before-subscription tie-break.

**Rationale**: FR-011/FR-012 require structured handoff artifact; blobs suit large snapshots and operator download; table stores index/metadata only.

**Alternatives considered**:
- **Table storage for full changeset** — Rejected: 64KB entity limit; changesets may grow with evidence.
- **Inline API response only** — Rejected: no durable export record for audit (FR-014 export events).

---

## R6: Aspire storage client usage

**Decision**: Register `AddAzureTableServiceClient("tables")` and `AddAzureBlobServiceClient("blobs")` in **BillDrift.Api** only (already wired). Infrastructure store classes accept injected clients via constructor — **no** `Environment.GetEnvironmentVariable` or manual connection string parsing. `BillDrift.Web` has **no** direct storage access; calls API over HTTP service discovery.

**Rationale**: User constraint; 006 established pattern; WebFetch point for storage credentials via Aspire.

**Alternatives considered**:
- **Web Blazor direct table access** — Rejected: duplicates auth boundary; bypasses API audit hooks.

---

## R7: Supersession on reconciliation re-run

**Decision**: On ingest for run `R2`, for each proposal key:

1. If matching `proposal` row exists for same `IdempotencyKey` from prior run `R1` with state `Pending` → mark `Stale`, set `SupersededByRunId = R2`
2. If prior state was `Approved` → mark prior row `Historical`; create new `Pending` row for `R2` requiring re-review (FR-013)
3. Append `decision` audit event type `Supersession` (no mutation of prior decision rows)

**Rationale**: Spec edge cases and FR-013; preserves audit immutability while forcing re-review after input change.

**Alternatives considered**:
- **Auto-carry forward approvals** — Rejected: violates safety when Stripe/truth snapshot changed.

---

## R8: Fluent UI Blazor first-time setup

**Decision**: Refactor `BillDrift.Web` skeleton (Bootstrap layout) to **Microsoft.FluentUI.AspNetCore.Components v5** per `.cursor/skills/fluentui-blazor-usage/SKILL.md`:

- `AddFluentUIComponents()` in `Program.cs`
- `<FluentProviders />` in root component tree
- Replace `MainLayout` / `NavMenu` with `FluentLayout` + `FluentNav` (v5 names)
- Add `default-fuib.css` link; remove Bootstrap dependency from layout (keep wwwroot cleanup as follow-up task)
- Approval UI: `FluentDataGrid` for queue, `FluentBadge` for state/eligibility, `FluentDialog` for reject reason and bulk confirm, `FluentMessageBar` for errors

**Rationale**: User instruction — first UI work; no prior Fluent setup exists. v5 API differs from v4; skill prevents deprecated components (`FluentNavMenu`, `FluentDesignTheme`).

**Alternatives considered**:
- **Keep Bootstrap for v1** — Rejected: explicit user request to adopt Fluent UI now.
- **Separate SPA frontend** — Rejected: existing Aspire `BillDrift.Web` Blazor host; Principle VI favors extending current skeleton.

---

## R9: API ↔ Web integration

**Decision**: `BillDrift.Web` registers typed `HttpClient` `IApprovalApiClient` with base address from Aspire service reference `https+http://api`. Approval pages are Interactive Server components calling API endpoints. API enforces operator identity via placeholder `IOperatorContext` (header `X-Operator-Id` in dev; real auth deferred).

**Rationale**: Web project currently has no API client wiring; Aspire already connects web → api in AppHost. Keeps storage and audit writes server-side.

**Alternatives considered**:
- **Blazor WASM calling API** — Rejected: current template uses Interactive Server; no WASM setup.

---

## R10: Eligibility integration with 005/006

**Decision**: `ApprovalEligibilityEvaluator` consumes:

- `ReconciliationExceptionViewModel` (surfaced exceptions with `ProposedChangeId`, `RequiresActionNow`)
- `ClassificationContext` (internal suppression, non-CSP blocks)
- `ProductMatchConfidence` from match groups

Rules mirror 005 FR-010–FR-013: low/none confidence → `InvestigationOnly`; non-CSP without mapping → `InvestigationOnly`; duplicate catalogue conflicts → `CatalogueConflict`.

**Rationale**: Avoid duplicating suppression logic; single eligibility gate before approve endpoint accepts request.

**Alternatives considered**:
- **Re-implement rules in approval only** — Rejected: drift risk vs exception surfacing.

---

## R11: Testing strategy

**Decision**:

- **Unit**: `ApprovalEligibilityEvaluator`, `ApprovedChangesetBuilder`, state transitions, supersession — `InMemoryApprovalStore`
- **Integration**: ingest → approve → export → audit with fixtures under `tests/fixtures/approval/`
- **Infrastructure**: Azurite table/blob round-trip tests
- **Web**: bUnit smoke tests for Fluent layout + grid render (optional, minimal)

Red-green-refactor for billing-critical paths: no auto-approve, export excludes pending/rejected/ineligible.

**Rationale**: Constitution II; mirrors 006 testing split.

**Alternatives considered**:
- **E2E Playwright only** — Rejected: heavy for v1; unit + API integration sufficient for approval safety proofs.

---

## R12: Bulk approve UX contract

**Decision**: API `POST .../bulk-approve` accepts `{ customerMexId, proposalIds[], confirmationToken }`. Server validates all IDs are `Pending` + `Eligible`, returns summary; client shows Fluent dialog with count/action types; on confirm, applies batch and writes one audit event per proposal plus one batch summary event.

**Rationale**: FR-017 explicit confirmation; avoids silent mass approval.

**Alternatives considered**:
- **Single-request bulk without token** — Rejected: fails FR-017 safety intent.
