# Tasks: Billing Reconciliation Engine

**Input**: Design documents from `/specs/004-reconciliation-engine/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md; `001-billing-domain-model` complete (domain reconciliation types, `ProductMapping`, `ReconciliationInputs`); `IProductMappingResolver` in Application; `003-stripe-csv-ingestion` complete (Stripe raw ingest — normalizers may be stubbed for fixture-based tests)

**UI note**: Backend-only — no Blazor reconciliation UI or Stripe write workflow in scope (see plan.md Out of Scope).

**Tests**: Included per constitution Principle II, `quickstart.md` validation scenarios, and `contracts/mismatch-rules.md` test contract.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US5) for story-phase tasks only

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Fixture layout, test helpers, and stub relocation per plan.md project structure.

- [X] T001 Create `tests/fixtures/reconciliation/` and `tests/fixtures/reconciliation/expected/` directory structure
- [X] T002 [P] Add `tests/fixtures/reconciliation/README.md` documenting fixture purpose, sanitized data policy, and mapping to `quickstart.md` scenarios
- [X] T003 Move `ReconciliationEngineStub` from `src/BillDrift.Application/Reconciliation/IReconciliationEngine.cs` to `tests/BillDrift.Application.Tests/Reconciliation/ReconciliationEngineStub.cs`; leave interface and request/options types in Application
- [X] T004 [P] Implement `GoldenRunComparer` in `tests/BillDrift.Application.Tests/Reconciliation/GoldenRunComparer.cs` for deterministic mismatch set comparison per `quickstart.md`
- [X] T005 [P] Implement `ReconciliationInputsFixtureLoader` JSON helper in `tests/BillDrift.Application.Tests/Reconciliation/ReconciliationInputsFixtureLoader.cs` for loading normalized input snapshots

**Checkpoint**: Test project compiles with stub relocated; fixture directories ready.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Pipeline skeleton, shared context, indexes, and validation/index stages. MUST complete before user story implementation.

**⚠️ CRITICAL**: No user story work until this phase is complete.

- [X] T006 Implement `ReconciliationContext` in `src/BillDrift.Application/Reconciliation/ReconciliationContext.cs` per `data-model.md`
- [X] T007 Implement `IReconciliationStage` interface in `src/BillDrift.Application/Reconciliation/IReconciliationStage.cs` per `contracts/reconciliation-pipeline.md`
- [X] T008 [P] Implement `ProductResolutionPath` enum in `src/BillDrift.Application/Reconciliation/Matching/ProductResolutionPath.cs`
- [X] T009 [P] Implement `CommercialKeyResolution` record in `src/BillDrift.Application/Reconciliation/Matching/CommercialKeyResolution.cs`
- [X] T010 [P] Implement `StripePriceSnapshot` in `src/BillDrift.Application/Reconciliation/Indexing/StripePriceSnapshot.cs`
- [X] T011 [P] Implement `StripeProductSnapshot` in `src/BillDrift.Application/Reconciliation/Indexing/StripeProductSnapshot.cs`
- [X] T012 Implement `IntendedPriceIndex` with manual override precedence in `src/BillDrift.Application/Reconciliation/Indexing/IntendedPriceIndex.cs` per research R4
- [X] T013 Implement `StripeCatalogueIndex` in `src/BillDrift.Application/Reconciliation/Indexing/StripeCatalogueIndex.cs` per `data-model.md`
- [X] T014 Implement `ProductMappingIndex` in `src/BillDrift.Application/Reconciliation/Indexing/ProductMappingIndex.cs` delegating to `IProductMappingResolver`
- [X] T015 Implement `InputValidationStage` in `src/BillDrift.Application/Reconciliation/Stages/InputValidationStage.cs` per `contracts/reconciliation-pipeline.md`
- [X] T016 Implement `IndexBuildStage` in `src/BillDrift.Application/Reconciliation/Stages/IndexBuildStage.cs` wiring all three indexes
- [X] T017 Implement `ReconciliationPipeline` orchestrator skeleton in `src/BillDrift.Application/Reconciliation/ReconciliationPipeline.cs` (validation + index stages only)
- [X] T018 [P] Add `IntendedPriceIndexTests` in `tests/BillDrift.Application.Tests/Reconciliation/IntendedPriceIndexTests.cs` covering manual override precedence (FR-017)
- [X] T019 [P] Add pipeline validation tests in `tests/BillDrift.Application.Tests/Reconciliation/ReconciliationPipelineTests.cs` (invalid scope throws `DomainValidationException`)

**Checkpoint**: Context, indexes, and first two pipeline stages compile and unit-test; Application project has no Infrastructure references.

---

## Phase 3: User Story 2 — Build and Maintain Canonical Product Mapping (Priority: P1)

**Goal**: Resolve products across domains using offer/SKU first, Stripe metadata, exact name variants, and deterministic fuzzy fallback; scope customers by Mex ID; record match confidence.

**Independent Test**: Given inputs where the same product appears under different names and in Stripe with offer/SKU metadata, engine resolves to one canonical identity with documented confidence; ambiguous/missing mappings produce explicit issues.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T020 [P] [US2] Add `CommercialKeyResolverTests` skeleton in `tests/BillDrift.Application.Tests/Reconciliation/CommercialKeyResolverTests.cs`
- [X] T021 [P] [US2] Add `FuzzyNameMatcherTests` skeleton in `tests/BillDrift.Application.Tests/Reconciliation/FuzzyNameMatcherTests.cs`
- [X] T022 [P] [US2] Add `CustomerMatcherTests` in `tests/BillDrift.Application.Tests/Reconciliation/CustomerMatcherTests.cs`

### Implementation for User Story 2

- [X] T023 [P] [US2] Implement `CustomerMatcher` in `src/BillDrift.Application/Reconciliation/Matching/CustomerMatcher.cs` per `contracts/matching-phases.md` Phase 1
- [X] T024 [US2] Implement `CommercialKeyResolver` priority chain in `src/BillDrift.Application/Reconciliation/Matching/CommercialKeyResolver.cs` per research R2 and `contracts/matching-phases.md` Phase 2
- [X] T025 [US2] Implement `DeterministicFuzzyNameMatcher` (token-set Jaccard, 0.85 threshold, tie-break) in `src/BillDrift.Application/Reconciliation/Matching/DeterministicFuzzyNameMatcher.cs` per research R3
- [X] T026 [US2] Implement `StripeItemMatcher` in `src/BillDrift.Application/Reconciliation/Matching/StripeItemMatcher.cs` per `contracts/matching-phases.md` Phase 3
- [X] T027 [US2] Implement `MatchGroupBuildStage` (subscription-truth-driven grouping) in `src/BillDrift.Application/Reconciliation/Stages/MatchGroupBuildStage.cs` per research R5
- [X] T028 [US2] Add offer/SKU high-confidence resolution tests in `CommercialKeyResolverTests.cs` per spec US2 acceptance scenario 1
- [X] T029 [US2] Add fuzzy threshold, tie-break, and ambiguous candidate tests in `FuzzyNameMatcherTests.cs` per research R3
- [X] T030 [US2] Create `tests/fixtures/reconciliation/mapping-missing.json` and `tests/fixtures/reconciliation/mapping-ambiguous.json` input snapshots

**Checkpoint**: Product resolution and match group construction testable in isolation; mapping fixtures available.

---

## Phase 4: User Story 5 — Review Explainable Reconciliation Results (Priority: P1)

**Goal**: Emit typed mismatches with expected/actual values, operator-facing descriptions, proposed actions with guards, deterministic ordering, and idempotency keys.

**Independent Test**: Given fixtures covering each mismatch category, operator can read issues and proposed actions without raw files; duplicate runs produce identical output.

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T031 [P] [US5] Add `MismatchDetectorTests` skeleton in `tests/BillDrift.Application.Tests/Reconciliation/MismatchDetectorTests.cs` (one case per `MismatchType`)
- [X] T032 [P] [US5] Add `DeterminismTests` in `tests/BillDrift.Application.Tests/Reconciliation/DeterminismTests.cs` per SC-002
- [X] T033 [P] [US5] Add `ProposedChangeFactoryTests` in `tests/BillDrift.Application.Tests/Reconciliation/ProposedChangeFactoryTests.cs` covering idempotency key format and bill-impacting guards

### Implementation for User Story 5

- [X] T034 [US5] Implement `MismatchDetector` rule dispatch in `src/BillDrift.Application/Reconciliation/Detection/MismatchDetector.cs` per `contracts/mismatch-rules.md`
- [X] T035 [US5] Implement `ProposedChangeFactory` with execution order and guard rules in `src/BillDrift.Application/Reconciliation/Detection/ProposedChangeFactory.cs` per `contracts/mismatch-rules.md`
- [X] T036 [US5] Implement `OutputOrderingStage` in `src/BillDrift.Application/Reconciliation/Stages/OutputOrderingStage.cs` per FR-019
- [X] T037 [US5] Implement `ReconciliationEngine` implementing `IReconciliationEngine` in `src/BillDrift.Application/Reconciliation/ReconciliationEngine.cs`
- [X] T038 [US5] Wire all stages into `ReconciliationPipeline.cs` per `contracts/reconciliation-pipeline.md`
- [X] T039 [US5] Add mapping-issue guard tests (no bill-impacting actions for `MappingMissing`/`MappingAmbiguous`) in `ProposedChangeFactoryTests.cs` per SC-008
- [X] T040 [US5] Add low-confidence fuzzy guard test (no bill-impacting actions) in `ProposedChangeFactoryTests.cs` per research R3
- [X] T041 [US5] Add determinism double-execution test in `DeterminismTests.cs`

**Checkpoint**: Full pipeline executes end-to-end with empty/minimal inputs; mismatch detector and proposed-change factory unit-tested.

---

## Phase 5: User Story 1 — Reconcile Subscription Truth Against Stripe (Priority: P1) 🎯 MVP

**Goal**: Match active subscription truth lines to Stripe items; detect missing-in-Stripe, quantity, billing frequency, and price mismatches; propose corrective actions.

**Independent Test**: Given subscription truth + Stripe items for one customer with known offer/SKU, engine produces match groups with correct issue types without supplier PDF inputs.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T042 [P] [US1] Add `ReconciliationEngineTests` integration skeleton in `tests/BillDrift.Application.Tests/Reconciliation/ReconciliationEngineTests.cs`
- [X] T043 [P] [US1] Create `tests/fixtures/reconciliation/clean-match-all-domains.json` per `quickstart.md` Scenario 1

### Implementation for User Story 1

- [X] T044 [US1] Implement `SubscriptionTruthReconcileStage` in `src/BillDrift.Application/Reconciliation/Stages/SubscriptionTruthReconcileStage.cs` invoking `MismatchDetector` for truth ↔ Stripe rules
- [X] T045 [US1] Integrate `SubscriptionTruthReconcileStage` into `ReconciliationPipeline.cs` after match group build
- [X] T046 [P] [US1] Create `tests/fixtures/reconciliation/missing-in-stripe.json` per `quickstart.md` Scenario 2
- [X] T047 [P] [US1] Create `tests/fixtures/reconciliation/quantity-mismatch.json` per `quickstart.md` Scenario 3
- [X] T048 [P] [US1] Create `tests/fixtures/reconciliation/billing-frequency-mismatch.json` per `quickstart.md` Scenario 4
- [X] T049 [P] [US1] Create `tests/fixtures/reconciliation/price-mismatch.json` per `quickstart.md` Scenario 5
- [X] T050 [US1] Add clean-match integration test (zero mismatches, high confidence) in `ReconciliationEngineTests.cs`
- [X] T051 [US1] Add missing-in-Stripe test asserting `CreateMissingItem` proposal in `ReconciliationEngineTests.cs`
- [X] T052 [US1] Add quantity mismatch test asserting `UpdateQuantity` proposal in `ReconciliationEngineTests.cs`
- [X] T053 [US1] Add billing frequency mismatch test asserting `SwitchPrice` when alternate exists in `ReconciliationEngineTests.cs`
- [X] T054 [US1] Add price mismatch test with `PriceTolerance` in `ReconciliationEngineTests.cs`
- [X] T055 [US1] Generate golden run `tests/fixtures/reconciliation/expected/quantity-mismatch-run.json` and add golden comparison test using `GoldenRunComparer`

**Checkpoint**: MVP subscription-truth ↔ Stripe reconciliation passes integration tests; golden run stable.

---

## Phase 6: User Story 3 — Reconcile Supplier Cost Lines (Priority: P2)

**Goal**: Attach supplier cost lines to match groups; flag unmapped and non-CSP lines; exclude pro-rata from quantity totals.

**Independent Test**: Given supplier cost fixtures, mappable lines appear in groups; non-CSP and orphan lines flagged without silent auto-match.

### Tests for User Story 3

- [X] T056 [P] [US3] Add supplier cost integration tests in `tests/BillDrift.Application.Tests/Reconciliation/SupplierCostReconciliationTests.cs`

### Implementation for User Story 3

- [X] T057 [US3] Implement `SupplierCostReconcileStage` in `src/BillDrift.Application/Reconciliation/Stages/SupplierCostReconcileStage.cs` per research R6 and R8
- [X] T058 [US3] Integrate `SupplierCostReconcileStage` into `ReconciliationPipeline.cs` after subscription truth stage
- [X] T059 [P] [US3] Create `tests/fixtures/reconciliation/non-csp-supplier-line.json` per `quickstart.md` Scenario 9
- [X] T060 [P] [US3] Create orphan supplier-only fixture `tests/fixtures/reconciliation/supplier-orphan-line.json` per spec US3 acceptance scenario 5
- [X] T061 [US3] Add non-CSP manual mapping flag test (no bill-impacting proposals) in `SupplierCostReconciliationTests.cs` per SC-006
- [X] T062 [US3] Add pro-rata exclusion test (recurring qty only in comparison) in `SupplierCostReconciliationTests.cs` per FR-016

**Checkpoint**: Supplier cost lines enrich match groups; non-CSP and pro-rata rules verified.

---

## Phase 7: User Story 4 — Reconcile Stripe Catalogue (Priority: P2)

**Goal**: Detect missing Stripe products/prices for mapped offer/SKU combinations; compare catalogue prices to intended RRP; propose catalogue maintenance actions.

**Independent Test**: Given intended pricing + Stripe catalogue fixtures, engine reports catalogue gaps and price drift without live subscription data.

### Tests for User Story 4

- [X] T063 [P] [US4] Add catalogue reconciliation tests in `tests/BillDrift.Application.Tests/Reconciliation/CatalogueReconciliationTests.cs`

### Implementation for User Story 4

- [X] T064 [US4] Implement `CatalogueReconcileStage` in `src/BillDrift.Application/Reconciliation/Stages/CatalogueReconcileStage.cs` per `contracts/mismatch-rules.md` catalogue rules
- [X] T065 [US4] Integrate `CatalogueReconcileStage` into `ReconciliationPipeline.cs` before output ordering
- [X] T066 [P] [US4] Create `tests/fixtures/reconciliation/catalogue-missing.json` per `quickstart.md` Scenario 6
- [X] T067 [US4] Add catalogue-missing test asserting `CreateOrUpdateCatalogueEntry` when `ProposeCatalogueChanges=true` in `CatalogueReconciliationTests.cs`
- [X] T068 [US4] Add catalogue price drift test in `CatalogueReconciliationTests.cs` per SC-007

**Checkpoint**: Catalogue gaps and price mismatches detected; catalogue proposals respect options flag.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: DI registration, duplicate Stripe edge case, comment audit, and quickstart validation.

- [X] T069 [P] Create `tests/fixtures/reconciliation/duplicate-stripe-items.json` per spec edge case (duplicate billing)
- [X] T070 Add duplicate Stripe items → `MappingAmbiguous` test in `ReconciliationEngineTests.cs` per research R7
- [X] T071 Implement `ReconciliationServiceCollectionExtensions` with `AddReconciliationEngine()` registering `IReconciliationEngine` → `ReconciliationEngine` and `IProductMappingResolver` → `ProductMappingResolver` in `src/BillDrift.Application/Reconciliation/ReconciliationServiceCollectionExtensions.cs`
- [X] T072 Register `AddReconciliationEngine()` from `src/BillDrift.Api/Program.cs` (or shared Application DI entry point if present)
- [X] T073 [P] Audit billing-critical comments on matching and mismatch rules per constitution Principle I (modules, public interfaces, non-obvious algorithms)
- [X] T074 Run full `dotnet test tests/BillDrift.Application.Tests --filter "FullyQualifiedName~Reconciliation"` and fix failures
- [X] T075 Validate all scenarios in `specs/004-reconciliation-engine/quickstart.md` manually or via test coverage map; document any deferred scenarios in fixture README

**Checkpoint**: All reconciliation tests pass; DI wired; quickstart scenarios covered.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **US2 (Phase 3)**: Depends on Foundational — **BLOCKS US1, US5 integration** (match groups + product resolution)
- **US5 (Phase 4)**: Depends on US2 — provides detector/factory/engine shell needed for US1 end-to-end tests
- **US1 (Phase 5)**: Depends on US5 — MVP subscription truth reconciliation
- **US3 (Phase 6)**: Depends on US1 pipeline wiring — can parallel with US4 after US5
- **US4 (Phase 7)**: Depends on US5 — can parallel with US3 after US5
- **Polish (Phase 8)**: Depends on US1–US4 completion

### User Story Dependencies

| Story | Depends on | Delivers independently |
|-------|------------|------------------------|
| US2 (P1) | Foundational | Product resolution + match groups via unit tests |
| US5 (P1) | US2 | Mismatch/proposal/detector unit tests + empty pipeline run |
| US1 (P1) | US5 | **MVP** — truth ↔ Stripe integration tests |
| US3 (P2) | US1 | Supplier cost attachment tests |
| US4 (P2) | US5 | Catalogue gap tests |

### Within Each User Story

- Tests marked in story phases MUST fail before implementation (constitution II)
- Matchers/indexes before stages that consume them
- Stages before engine integration tests
- Story checkpoint before next priority

### Parallel Opportunities

- **Phase 1**: T002, T004, T005 in parallel
- **Phase 2**: T008–T011, T018, T019 in parallel after T006–T007
- **Phase 3**: T020–T022, T023 in parallel; T028–T030 after implementation
- **Phase 4**: T031–T033 in parallel
- **Phase 5**: T043, T046–T049 fixture creation in parallel
- **Phase 6–7**: US3 and US4 can proceed in parallel once Phase 5 completes
- **Phase 8**: T069, T073 in parallel

---

## Parallel Example: User Story 2

```bash
# Launch all US2 test skeletons together:
Task: "CommercialKeyResolverTests in tests/BillDrift.Application.Tests/Reconciliation/CommercialKeyResolverTests.cs"
Task: "FuzzyNameMatcherTests in tests/BillDrift.Application.Tests/Reconciliation/FuzzyNameMatcherTests.cs"
Task: "CustomerMatcherTests in tests/BillDrift.Application.Tests/Reconciliation/CustomerMatcherTests.cs"

# Launch independent matchers together (after T024 CommercialKeyResolver interface defined):
Task: "CustomerMatcher in src/BillDrift.Application/Reconciliation/Matching/CustomerMatcher.cs"
```

---

## Parallel Example: User Story 1 Fixtures

```bash
# Create all US1 fixtures in parallel after ReconciliationInputsFixtureLoader exists:
Task: "missing-in-stripe.json"
Task: "quantity-mismatch.json"
Task: "billing-frequency-mismatch.json"
Task: "price-mismatch.json"
```

---

## Implementation Strategy

### MVP First (User Story 1 path)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (**CRITICAL**)
3. Complete Phase 3: US2 (product mapping)
4. Complete Phase 4: US5 (detector, factory, engine shell)
5. Complete Phase 5: US1 (subscription truth ↔ Stripe)
6. **STOP and VALIDATE**: Run `ReconciliationEngineTests` + `DeterminismTests`; demo mismatch output for one customer
7. Add US3 + US4 incrementally
8. Polish

### Incremental Delivery

| Increment | Stories | Value |
|-----------|---------|-------|
| MVP | Setup + Foundational + US2 + US5 + US1 | Core revenue-protection: truth vs Stripe drift detection |
| +Supplier | US3 | Margin visibility: supplier cost alignment |
| +Catalogue | US4 | Proactive catalogue hygiene before billing drift spreads |
| +Polish | Phase 8 | Production-ready DI and full quickstart coverage |

### Parallel Team Strategy

With multiple developers after Phase 2:

- **Developer A**: US2 matching components
- **Developer B**: US5 detector/factory (after US2 T027 match groups)
- Once US5 engine shell lands → **Developer A**: US1 truth stage + fixtures; **Developer B**: US3 supplier stage; **Developer C**: US4 catalogue stage

---

## Notes

- Domain types in `BillDrift.Domain.Reconciliation` are pre-built (001); avoid domain changes unless `ReconciliationInputs` lacks catalogue fields — document in PR if extended
- Normalized fixture data may use hand-built `ReconciliationInputs` JSON until normalizers ship; loader helper (T005) decouples tests from ingest pipeline
- `[P]` tasks touch different files — safe to parallelize within phase constraints above
- Commit after each task or logical group
- Stop at any **Checkpoint** to validate story independence before continuing
