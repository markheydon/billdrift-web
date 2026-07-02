# Implementation Plan: Reconciliation Exception Surfacing

**Branch**: `005-reconciliation-exceptions` | **Date**: 2026-07-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-reconciliation-exceptions/spec.md`

## Summary

Implement a deterministic exception surfacing layer in `BillDrift.Application.Reconciliation.ExceptionSurfacing` that consumes a completed `ReconciliationRun` from the reconciliation engine (feature 004) and produces a `ReconciliationExceptionViewModel` — a UI-ready, presentation-agnostic graph with run summary, customer groups, and individual exceptions carrying type, severity, category, explanation, and evidence. The layer maps engine `Mismatch` records to 11 operator-facing categories, derives gap-filling exceptions (orphaned Stripe items, Mex ID mismatch, product mismatch), applies suppression and catalogue consolidation to reduce false positives, computes `RequiresActionNow` triage flags, and orders output for operator workflows. No UI, no Stripe writes, no domain schema changes.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: BCL only — consumes existing `BillDrift.Domain` reconciliation types; no external libraries  
**Storage**: N/A — in-memory `ReconciliationRun` → view model transformation  
**Testing**: xUnit + FluentAssertions in `BillDrift.Application.Tests/ExceptionSurfacing/`; fixtures chain engine + surfacing  
**Target Platform**: Azure (Aspire solution); surfacing runs anywhere .NET 10 runs  
**Project Type**: Modular .NET Aspire solution — Application-layer presentation transform over reconciliation output  
**Performance Goals**: Surface exceptions for 50-customer run (<500 match groups) in <500ms; negligible overhead vs engine execution  
**Constraints**: Deterministic output (FR-022, SC-002); no Stripe API calls; no side effects; mandatory code comments on suppression and mapping rules (constitution I); operator terminology consistency (FR-024, constitution III)  
**Scale/Scope**: 11 exception categories; 3 reconciliation domains; 5 suppression rules; 3 derived detectors; single concrete `ExceptionSurfacingService`

### Dependency on 001-billing-domain-model

| Artifact | Usage |
|----------|-------|
| `ReconciliationRun`, `EntityMatchGroup`, `Mismatch`, `ProposedChange` | Input types — no schema changes |
| `MismatchType`, `MismatchSeverity`, `MatchConfidence` | Mapping and guard inputs |
| `CustomerIdentity`, `CommercialKey` | Grouping and product context |
| `contracts/reconciliation-engine.md` | Baseline contract |

### Dependency on 004-reconciliation-engine

| Artifact | Usage |
|----------|-------|
| `IReconciliationEngine`, `ReconciliationRequest`, `ReconciliationOptions` | Upstream producer of `ReconciliationRun` |
| `contracts/mismatch-rules.md` | Proposed change guards inherited at surfacing |
| Reconciliation test fixtures | Chained into exception surfacing fixtures |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Code Quality & Maintainability | ✅ PASS | Four-phase pipeline with single-responsibility classes; mapping and suppression rules documented in contracts |
| II. Testing Standards | ✅ PASS | One test per category, suppression rule, and ordering contract; determinism test; chained engine fixtures |
| III. Consistent User Experience | ✅ PASS (design) | Operator-facing explanations and evidence bundles; terminology aligned ("exception", "corrective action") |
| IV. Security by Design | ✅ PASS | No secrets in evidence; read-only transform of in-memory snapshots |
| V. Billing Accuracy & Human Control | ✅ PASS | Deterministic surfacing; does not auto-apply; strips proposed actions when confidence low |
| VI. Pragmatic Simplicity | ✅ PASS | Single concrete service, no new project; no interface until second implementation needed |

### Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I | ✅ PASS | Contracts in `contracts/`; derived detector rules specified in research R8 and mapping contract |
| II | ✅ PASS | quickstart.md defines 8 validation scenarios + determinism check |
| III | ✅ PASS | Evidence builder produces labelled multi-source bundles per FR-019 |
| IV | ✅ PASS | Evidence values formatted from domain fields only; no credential fields |
| V | ✅ PASS | `ProposedChangeId` reference only; no approval/apply logic in surfacing |
| VI | ✅ PASS | No new interfaces; Application subfolder only; complexity tracked — no violations |

**Gate result**: PASS — proceed to `/speckit-tasks`.

## Project Structure

### Documentation (this feature)

```text
specs/005-reconciliation-exceptions/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── exception-surfacing-pipeline.md
│   ├── mismatch-to-exception-mapping.md
│   └── suppression-and-ordering-rules.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
BillDrift.sln
src/
├── BillDrift.Application/
│   └── Reconciliation/
│       └── ExceptionSurfacing/
│           ├── ExceptionSurfacingService.cs       # ★ Entry point
│           ├── ReconciliationExceptionViewModel.cs
│           ├── SurfacingContext.cs
│           ├── Phases/
│           │   ├── CollectPhase.cs
│           │   ├── SuppressPhase.cs
│           │   ├── ConsolidatePhase.cs
│           │   └── FinalizePhase.cs
│           ├── Mapping/
│           │   └── MismatchToExceptionMapper.cs
│           ├── Detection/
│           │   ├── OrphanedStripeDetector.cs
│           │   ├── MexIdMismatchDetector.cs
│           │   └── ProductMismatchDetector.cs
│           ├── Evidence/
│           │   └── EvidenceBuilder.cs
│           └── Ordering/
│               └── ExceptionOrdering.cs
tests/
├── BillDrift.Application.Tests/
│   └── ExceptionSurfacing/
│       ├── ExceptionSurfacingServiceTests.cs
│       ├── SuppressionRulesTests.cs
│       ├── MismatchMappingTests.cs
│       ├── DerivedDetectorTests.cs
│       ├── ConsolidationTests.cs
│       ├── OrderingTests.cs
│       └── DeterminismTests.cs
└── fixtures/
    └── exception-surfacing/
        ├── mixed-three-customers.json
        ├── suppression-mapping-root-cause.json
        ├── catalogue-consolidation.json
        ├── orphaned-stripe-item.json
        ├── mex-id-mismatch.json
        ├── low-confidence-no-action.json
        └── clean-run-empty.json
```

**Structure Decision**: Application-layer subfolder under existing `Reconciliation` namespace. View model types stay in Application (not Domain) because they are derived presentation structures. Web/API integration deferred to a future feature that consumes `ReconciliationExceptionViewModel`.

## Complexity Tracking

> No constitution violations requiring justification. Design explicitly rejects Domain schema changes and separate Presentation project per Principle VI.

## Phase 0 Output

See [research.md](./research.md) — all technical context items resolved:
- R1: Application layer placement
- R2: Concrete `ExceptionSurfacingService` (no interface)
- R3: Mismatch-to-exception mapping with catalogue subdivision
- R4: Deterministic `SurfacedExceptionId` format
- R5: Four-phase pipeline (collect → suppress → consolidate → finalize)
- R6: `EvidenceBuilder` from match group attachments
- R7: `RequiresActionNow` rules
- R8: Orphaned Stripe derived detection
- R9: Terminology alignment

## Phase 1 Output

| Artifact | Path |
|----------|------|
| Data model | [data-model.md](./data-model.md) |
| Pipeline contract | [contracts/exception-surfacing-pipeline.md](./contracts/exception-surfacing-pipeline.md) |
| Mapping contract | [contracts/mismatch-to-exception-mapping.md](./contracts/mismatch-to-exception-mapping.md) |
| Suppression/ordering contract | [contracts/suppression-and-ordering-rules.md](./contracts/suppression-and-ordering-rules.md) |
| Validation guide | [quickstart.md](./quickstart.md) |

## Implementation Notes

1. **Chaining**: Typical call sequence: `var run = engine.Execute(request); var viewModel = surfacing.Surface(run, request.Options);`
2. **Audit**: `Summary.SuppressedCount` and `SuppressedSiblingCount` expose deduplication transparency for operators and tests.
3. **Future UI**: `CustomerExceptionGroup` and `FlatExceptions()` provide grouped and flat consumption patterns without prescribing rendering.
4. **Engine gaps**: Orphaned Stripe, MexId, and Product mismatch are derived at surfacing time; may migrate to engine `MismatchType` values in a future 001/004 amendment if audit requirements change.
5. **DI**: Register `ExceptionSurfacingService` as singleton in Application service registration alongside `IReconciliationEngine`.
