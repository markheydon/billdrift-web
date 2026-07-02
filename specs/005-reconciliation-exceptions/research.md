# Research: Reconciliation Exception Surfacing

**Feature**: `005-reconciliation-exceptions`  
**Date**: 2026-07-02

## R1: Layer placement for view models

**Decision**: Implement exception surfacing in `BillDrift.Application.Reconciliation` under an `ExceptionSurfacing/` subfolder. View model types (`ReconciliationExceptionViewModel`, `SurfacedException`, etc.) are Application-layer `sealed record` types, not Domain entities.

**Rationale**: The surfacing layer transforms existing `ReconciliationRun` output into a presentation-oriented structure. It does not mutate billing truth or introduce new persistence. Keeping it adjacent to the reconciliation engine avoids a new project boundary and matches the 004 Application-layer pattern for pipeline internals.

**Alternatives considered**:
- **Domain entities for exceptions** — Rejected: exceptions are derived views of mismatches, not authoritative billing state; would pollute the domain model with UI-oriented fields (`RequiresActionNow`, evidence bundles).
- **Separate `BillDrift.Application.Presentation` project** — Rejected: no second consumer or deployment unit yet; violates Principle VI.
- **Web layer only** — Rejected: spec requires UI-agnostic view model consumable by API, exports, and future UI.

---

## R2: Public entry point shape

**Decision**: Single concrete service `ExceptionSurfacingService` with one public method `Surface(ReconciliationRun run, ReconciliationOptions? options = null) → ReconciliationExceptionViewModel`. No interface unless a second implementation is needed.

**Rationale**: Principle VI — one implementation, testable through concrete types. The service is invoked after `IReconciliationEngine.Execute` completes. Options are passed through for scope flags (`IncludeInactiveSubscriptions`) used by gap-filling rules (orphaned Stripe detection).

**Alternatives considered**:
- **`IExceptionSurfacingService` interface** — Deferred until API or Web needs DI swap; concrete registration in DI is sufficient for v1.
- **Extension method on `ReconciliationRun`** — Rejected: couples domain aggregate to Application surfacing logic.

---

## R3: Mismatch-to-exception mapping strategy

**Decision**: Primary mapping table from `MismatchType` → `ExceptionCategory` + `ReconciliationDomain`. Gap-filling detectors run after mismatch mapping for categories the engine does not yet emit: `OrphanedBillingItem`, `MexIdMismatch`, `ProductMismatch`. Catalogue `CatalogueMissing` is subdivided at surfacing time using evidence from match group attachments (product exists vs price missing vs price amount delta).

**Rationale**: Spec FR-006 requires documented deterministic mapping without changing the 001 domain `MismatchType` enum in this feature. Subdividing catalogue issues at surfacing preserves backward compatibility with 004 engine output while delivering the 11 operator categories.

**Mapping summary**:

| MismatchType (004) | ExceptionCategory | Domain |
|--------------------|-------------------|--------|
| `MissingInStripe` | Missing Billing Item | Truth vs Stripe |
| `QuantityMismatch` | Quantity or Licence Mismatch | Truth vs Stripe |
| `BillingFrequencyMismatch` | Billing Frequency Mismatch | Truth vs Stripe |
| `PriceMismatch` | Stripe Price Does Not Match Intended RRP | Truth vs Stripe |
| `CatalogueMissing` | Stripe Product Missing / Stripe Price Missing (subdivided) | Pricing vs Catalogue |
| `MappingMissing` | Offer or SKU Ambiguous Mapping (supplier) OR Non-CSP Requires Manual Review | Supplier Cost vs Mapped Products |
| `MappingAmbiguous` | Offer or SKU Ambiguous Mapping | Supplier Cost vs Mapped Products |
| *(derived)* | Orphaned Billing Item | Truth vs Stripe |
| *(derived)* | Mex ID Mismatch | Supplier Cost vs Mapped Products |
| *(derived)* | Product Mismatch | Truth vs Stripe |

**Alternatives considered**:
- **Extend `MismatchType` enum in Domain** — Rejected for this feature: would require 001 amendment and 004 engine changes; surfacing can derive gaps without blocking this feature.
- **1:1 MismatchType = ExceptionCategory** — Rejected: operator categories are coarser and catalogue subdivision needs context.

---

## R4: Stable exception identity

**Decision**: `SurfacedExceptionId` is a deterministic string: `{RunId}:{SourceKind}:{SourceKey}` where:
- For mismatch-backed exceptions: `SourceKind=Mismatch`, `SourceKey={MismatchId}`
- For derived exceptions: `SourceKind=Derived`, `SourceKey={ruleCode}:{entityRef}` (e.g., `OrphanedStripe:si_abc123`)

**Rationale**: SC-002 requires identical runs to produce identical exception identities. Tying IDs to run + source prevents GUID randomness across executions.

**Alternatives considered**:
- **Sequential integers** — Rejected: order-dependent, fragile when suppression rules change ordering only.
- **MismatchId only** — Insufficient for derived exceptions without a backing mismatch.

---

## R5: Suppression and consolidation pipeline

**Decision**: Four-phase surfacing pipeline:
1. **Collect** — Map mismatches + run derived detectors → raw `SurfacedException` candidates
2. **Suppress** — Apply root-cause rules per match group (mapping → suppress billing mismatches; MexId → suppress truth-vs-Stripe; low confidence → strip proposed action refs)
3. **Consolidate** — Merge catalogue exceptions sharing same `CommercialKey`
4. **Finalize** — Compute `RequiresActionNow`, build evidence bundles, group by customer, apply ordering

**Rationale**: Matches spec FR-012–FR-014; keeps each phase independently testable. Suppression runs before consolidation so consolidated catalogue items do not resurrect suppressed children.

**Alternatives considered**:
- **Single-pass fold** — Rejected: harder to test and audit suppression decisions.
- **Suppress at engine level** — Rejected: detection and surfacing concerns are separate; engine must emit full mismatch set for audit.

---

## R6: Evidence construction

**Decision**: `EvidenceBuilder` extracts labelled snapshots from `EntityMatchGroup` attachments and `Mismatch.ExpectedValue`/`ActualValue`. Standard evidence sources: `Subscription Truth`, `Stripe Subscription Item`, `Supplier Cost Line`, `Intended Retail Price`, `Stripe Catalogue`. Entity references use domain ID string forms for drill-down.

**Rationale**: FR-019 requires operator-verifiable bundles without re-opening raw files. Reusing match group attachments avoids re-indexing inputs.

**Alternatives considered**:
- **Store raw JSON blobs** — Rejected: secrets risk, oversized view model, duplicates ingestion data.
- **Evidence only from Mismatch fields** — Insufficient for multi-source exceptions (e.g., quantity mismatch needs both truth and Stripe fields labelled separately).

---

## R7: Requires-action-now computation

**Decision**: `RequiresActionNow = true` when:
- `Severity == Error`, AND
- No blocking mapping/identity issue on the same match group, AND
- Either (`ProposedChangeId` linked and eligible) OR category ∈ `{Missing Billing Item, Orphaned Billing Item, Quantity/Licence, Billing Frequency, Product Mismatch}`

For blocking setup categories (mapping, MexId, catalogue-only, non-CSP): `RequiresActionNow = true` only when `Severity == Error` and category is one of mapping/MexId/catalogue-missing types — reflects blocking setup work per FR-011, not bill-impacting apply.

**Rationale**: Operators need a single flag for triage queues; separating "blocking setup" from "ready to apply" is handled by whether `ProposedChangeId` is populated and eligible.

---

## R8: Orphaned Stripe detection

**Decision**: After match group build, any in-scope `StripeBillingItem` not referenced as `EntityMatchGroup.StripeItem` for a group with a matching `SubscriptionLine` (same MexId + CommercialKeyRoot) emits an `OrphanedBillingItem` derived exception. Items on canceled subscriptions excluded when `IncludeInactiveSubscriptions == false`.

**Rationale**: 004 engine emits `MissingInStripe` but not the inverse; spec FR-015 requires bidirectional drift detection at surfacing layer.

---

## R9: Terminology alignment

**Decision**: Use **exception** in view model type names and operator-facing strings; retain **mismatch** only in internal mapping from engine output. Align with constitution III terms: "corrective action" (not "proposed change") in explanations shown to operators; `ProposedChangeId` remains the technical reference field.

**Rationale**: Spec FR-024 and constitution III require consistent operator vocabulary across future UI surfaces.
