# Implementation Plan: Billing Reconciliation Engine

**Branch**: `004-reconciliation-engine` | **Date**: 2026-07-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-reconciliation-engine/spec.md`

## Summary

Implement a deterministic reconciliation engine in `BillDrift.Application.Reconciliation` that consumes normalized inputs from four billing domains (supplier cost, Microsoft subscription truth, intended retail pricing, Stripe billing + catalogue) and produces a `ReconciliationRun` with `EntityMatchGroup` results, typed `Mismatch` issues, and `ProposedChange` actions. Matching prioritizes offer ID + SKU ID and Stripe product metadata, uses Mex ID for customer scoping, falls back to exact supplier name variants via `IProductMappingResolver`, then applies a deterministic fuzzy name scorer only when no higher-confidence path resolves. The engine performs subscription-truth → Stripe validation (quantity, frequency, price), supplier cost attachment and non-CSP flagging, and catalogue gap/price checks. Replaces `ReconciliationEngineStub` with a pure, side-effect-free pipeline; no Stripe writes.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: BCL only — no external matching libraries; reuse existing `BillDrift.Domain` types  
**Storage**: N/A — in-memory `ReconciliationInputs` snapshot; persistence deferred  
**Testing**: xUnit + FluentAssertions in `BillDrift.Application.Tests`; extend domain fixtures from 001  
**Target Platform**: Azure (Aspire solution); engine runs anywhere .NET 10 runs  
**Project Type**: Modular .NET Aspire solution — Application-layer pure reconciliation pipeline  
**Performance Goals**: Reconcile 50 customers × ~20 products (<1,000 match groups) in <5 seconds (SC-005); typical monthly run <1s  
**Constraints**: Deterministic output (FR-015, SC-002); no Stripe API calls; no side effects; mandatory code comments on matching rules (constitution I); human approval boundary preserved (FR-020)  
**Scale/Scope**: Single-tenant reseller; 7 mismatch types + non-CSP manual review flag; configurable run options via `ReconciliationOptions`

### Dependency on 001-billing-domain-model

| Artifact | Usage |
|----------|-------|
| `ReconciliationRun`, `ReconciliationInputs`, `EntityMatchGroup`, `Mismatch`, `ProposedChange` | Output and input types — no schema changes expected |
| `MismatchType`, `ProposedActionType`, `MatchConfidence` | Rule engine enums |
| `ProductMapping`, `CommercialKey`, `CustomerIdentity` | Matching keys |
| `contracts/reconciliation-engine.md` | Baseline contract — extended by this feature's contracts |
| Domain test fixtures | `reconciliation-quantity-mismatch`, `reconciliation-determinism` |

### Dependency on 002-giacom-pdf-ingestion / 003-stripe-csv-ingestion

| Artifact | Usage |
|----------|-------|
| Normalized `SupplierCostLine` | Supplier cost reconciliation input |
| Normalized `MicrosoftSubscriptionLine` | Subscription truth reconciliation input |
| Normalized `StripeBillingItem` + catalogue | Stripe billing state input |
| Normalizer interfaces (`IGiacomBillingNormalizer`, etc.) | Upstream — engine consumes outputs only |

### Dependency on pricing ingestion (adjacent)

| Artifact | Usage |
|----------|-------|
| Normalized `IntendedPrice` from `ResellerPricingVsRRP.csv` + manual overrides | RRP reference for price and catalogue checks |
| Manual override precedence rule | Implemented in `IntendedPriceIndex` |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Code Quality & Maintainability | ✅ PASS | Pipeline split into single-responsibility stages; matching rules documented in contracts and code comments |
| II. Testing Standards | ✅ PASS | One fixture per `MismatchType`; determinism test; pro-rata quantity exclusion test |
| III. Consistent User Experience | ✅ N/A | No UI; operator-facing mismatch descriptions per FR-021 |
| IV. Security by Design | ✅ PASS | No secrets; no external calls; inputs are normalized snapshots |
| V. Billing Accuracy & Human Control | ✅ PASS | Deterministic engine; explainable mismatches; no auto-apply; mapping issues block bill-impacting proposals |

### Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I | ✅ PASS | Stage contracts in `contracts/`; fuzzy fallback algorithm specified deterministically in research R3 |
| II | ✅ PASS | quickstart.md defines fixture scenarios per mismatch type + golden run comparison |
| III | ✅ N/A | — |
| IV | ✅ PASS | Engine is read-only over inputs |
| V | ✅ PASS | Proposed changes carry idempotency keys; low-confidence matches suppress bill-impacting actions |

**Gate result**: PASS — proceed to `/speckit-tasks`.

## Project Structure

### Documentation (this feature)

```text
specs/004-reconciliation-engine/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── reconciliation-pipeline.md
│   ├── matching-phases.md
│   └── mismatch-rules.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
BillDrift.sln
src/
├── BillDrift.Application/
│   ├── Reconciliation/
│   │   ├── ReconciliationEngine.cs           # ★ IReconciliationEngine implementation
│   │   ├── ReconciliationPipeline.cs         # ★ Stage orchestrator
│   │   ├── ReconciliationContext.cs          # ★ Shared indexes + options per run
│   │   ├── Stages/
│   │   │   ├── InputValidationStage.cs
│   │   │   ├── IndexBuildStage.cs
│   │   │   ├── MatchGroupBuildStage.cs
│   │   │   ├── SubscriptionTruthReconcileStage.cs
│   │   │   ├── SupplierCostReconcileStage.cs
│   │   │   ├── CatalogueReconcileStage.cs
│   │   │   └── OutputOrderingStage.cs
│   │   ├── Matching/
│   │   │   ├── CommercialKeyResolver.cs      # ★ Offer/SKU + metadata resolution
│   │   │   ├── CustomerMatcher.cs
│   │   │   ├── StripeItemMatcher.cs
│   │   │   └── DeterministicFuzzyNameMatcher.cs
│   │   ├── Detection/
│   │   │   ├── MismatchDetector.cs           # ★ Rule dispatch
│   │   │   └── ProposedChangeFactory.cs
│   │   └── Indexing/
│   │       ├── IntendedPriceIndex.cs         # ★ Manual override precedence
│   │       ├── StripeCatalogueIndex.cs
│   │       └── ProductMappingIndex.cs
│   ├── Mapping/
│   │   └── IProductMappingResolver.cs        # Existing — reuse
│   └── Reconciliation/
│       └── IReconciliationEngine.cs            # Existing — remove stub to separate file
└── BillDrift.Domain/
    └── Reconciliation/                         # Existing domain types — no changes expected

tests/
├── BillDrift.Application.Tests/
│   └── Reconciliation/
│       ├── ReconciliationEngineTests.cs      # ★ End-to-end per mismatch type
│       ├── DeterminismTests.cs
│       ├── CommercialKeyResolverTests.cs
│       ├── IntendedPriceIndexTests.cs
│       ├── FuzzyNameMatcherTests.cs
│       ├── MismatchDetectorTests.cs
│       └── GoldenRunComparer.cs
└── fixtures/
    └── reconciliation/                       # ★ NEW — full ReconciliationInputs JSON
        ├── clean-match-all-domains.json
        ├── missing-in-stripe.json
        ├── quantity-mismatch.json
        ├── billing-frequency-mismatch.json
        ├── price-mismatch.json
        ├── catalogue-missing.json
        ├── mapping-missing.json
        ├── mapping-ambiguous.json
        ├── non-csp-supplier-line.json
        ├── duplicate-stripe-items.json
        └── expected/
            └── quantity-mismatch-run.json
```

**Structure Decision**: Reconciliation logic lives entirely in `BillDrift.Application` — no Infrastructure dependency. The engine is a pure function over domain types. `ReconciliationEngineStub` moves to test helpers or is removed once real engine ships. Pipeline stages are internal; public surface remains `IReconciliationEngine` + existing request/options types.

## Complexity Tracking

> No constitution violations requiring justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Phase 0: Research

**Status**: ✅ Complete — see [research.md](./research.md)

Key decisions:
- Pipeline of ordered stages with shared `ReconciliationContext` (R1)
- Product resolution priority: offer/SKU → Stripe metadata → mapping variants → deterministic fuzzy (R2, R3)
- `IntendedPriceIndex` with manual override wins on `CommercialKey` collision (R4)
- Subscription truth drives primary match groups; supplier cost and catalogue attach/enrich (R5)
- Non-CSP supplier lines emit mapping issue with distinct operator description (R6)
- Duplicate Stripe items for same customer+key → `MappingAmbiguous` not silent merge (R7)
- Pro-rata supplier lines excluded from quantity totals (R8)

## Phase 1: Design

**Status**: ✅ Complete

| Artifact | Path |
|----------|------|
| Application data model | [data-model.md](./data-model.md) |
| Pipeline contract | [contracts/reconciliation-pipeline.md](./contracts/reconciliation-pipeline.md) |
| Matching phases | [contracts/matching-phases.md](./contracts/matching-phases.md) |
| Mismatch rules | [contracts/mismatch-rules.md](./contracts/mismatch-rules.md) |
| Validation quickstart | [quickstart.md](./quickstart.md) |

## Phase 2: Implementation Tasks

**Status**: Pending — run `/speckit-tasks` to generate [tasks.md](./tasks.md)

Expected task groups:
1. Extract `ReconciliationEngineStub` to test helpers; scaffold pipeline + context
2. Implement `IntendedPriceIndex`, `StripeCatalogueIndex`, `ProductMappingIndex`
3. Implement `CommercialKeyResolver` and `DeterministicFuzzyNameMatcher`
4. Implement `MatchGroupBuildStage` (customer + product grouping)
5. Implement `SubscriptionTruthReconcileStage` (missing/quantity/frequency/price)
6. Implement `SupplierCostReconcileStage` (non-CSP, orphaned lines)
7. Implement `CatalogueReconcileStage` (missing product/price, catalogue price drift)
8. Implement `MismatchDetector` + `ProposedChangeFactory`
9. Implement `ReconciliationEngine` orchestrating all stages
10. Add reconciliation fixtures and golden-run tests (one per mismatch type + determinism)
11. Register DI: `IReconciliationEngine` → `ReconciliationEngine`

## Out of Scope (this feature)

- Blazor reconciliation review UI
- Stripe write/apply workflow (approval, dry-run, API mutations)
- Product mapping CRUD UI
- Normalizer implementations (upstream features)
- Persistence of reconciliation runs
- Scheduled/unattended multi-tenant reconciliation
- Cross-currency price comparison

## Next Steps

1. `/speckit-tasks` — generate dependency-ordered implementation tasks
2. Ensure normalized input fixtures exist for all four domains (may require normalizer stubs)
3. `/speckit-implement` — build engine and regression tests
