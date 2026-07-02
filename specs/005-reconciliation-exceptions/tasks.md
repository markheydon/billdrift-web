# Tasks: Reconciliation Exception Surfacing

**Input**: Design documents from `/specs/005-reconciliation-exceptions/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md; `004-reconciliation-engine` complete (`IReconciliationEngine`, `ReconciliationRun`, mismatch detection fixtures)

**UI note**: Backend-only — no Blazor exception list UI in scope (see plan.md). Delivers `ReconciliationExceptionViewModel` for future consumers.

**Tests**: Included per constitution Principle II, `quickstart.md` validation scenarios, and contract test tables in `contracts/`.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US5) for story-phase tasks only

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Fixture layout, test helpers, and folder structure per plan.md.

- [X] T001 Create `tests/fixtures/exception-surfacing/` directory structure per plan.md
- [X] T002 [P] Add `tests/fixtures/exception-surfacing/README.md` documenting fixture purpose, mapping to `quickstart.md` scenarios, and chaining via `IReconciliationEngine`
- [X] T003 [P] Create `tests/BillDrift.Application.Tests/ExceptionSurfacing/` test folder structure
- [X] T004 [P] Implement `ExceptionSurfacingTestBuilder` in `tests/BillDrift.Application.Tests/ExceptionSurfacing/ExceptionSurfacingTestBuilder.cs` chaining engine execute + surfacing
- [X] T005 [P] Implement `ExceptionViewModelComparer` in `tests/BillDrift.Application.Tests/ExceptionSurfacing/ExceptionViewModelComparer.cs` for determinism comparison excluding `GeneratedAt`

**Checkpoint**: Test project folder ready; helpers compile against existing reconciliation engine.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: View model types, surfacing context, pipeline skeleton, and DI. MUST complete before user story implementation.

**⚠️ CRITICAL**: No user story work until this phase is complete.

- [X] T006 Create `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/` folder structure per plan.md
- [X] T007 Implement enums (`ExceptionCategory`, `ReconciliationDomain`, `ExceptionSeverity`, `EvidenceSource`, `SuppressionRule`) in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/ReconciliationExceptionViewModel.cs` per `data-model.md`
- [X] T008 Implement view model records (`ReconciliationExceptionViewModel`, `ExceptionRunSummary`, `CustomerExceptionGroup`, `SurfacedException`, `ExceptionEvidence`, `ProductContext`, `SurfacedExceptionId`) in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/ReconciliationExceptionViewModel.cs`
- [X] T009 Implement `SurfacingContext` and `SuppressionRecord` in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/SurfacingContext.cs` per `data-model.md`
- [X] T010 Implement `ExceptionSurfacingService` skeleton orchestrating four phase stubs in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/ExceptionSurfacingService.cs` per `contracts/exception-surfacing-pipeline.md`
- [X] T011 [P] Implement `CollectPhase` stub in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Phases/CollectPhase.cs`
- [X] T012 [P] Implement `SuppressPhase` stub in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Phases/SuppressPhase.cs`
- [X] T013 [P] Implement `ConsolidatePhase` stub in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Phases/ConsolidatePhase.cs`
- [X] T014 [P] Implement `FinalizePhase` stub in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Phases/FinalizePhase.cs`
- [X] T015 [P] Add view model type construction tests in `tests/BillDrift.Application.Tests/ExceptionSurfacing/ViewModelTypesTests.cs`
- [X] T016 Register `ExceptionSurfacingService` in `AddReconciliationEngine()` in `src/BillDrift.Application/Reconciliation/ReconciliationEngine.cs`

**Checkpoint**: Application compiles; `Surface(run)` returns empty view model; DI registers surfacing service.

---

## Phase 3: User Story 3 — Work Through Exception Categories Across Reconciliation Domains (Priority: P1)

**Goal**: Map engine mismatches to 11 operator categories across three reconciliation domains; derive orphaned Stripe, Mex ID, and product mismatch exceptions.

**Independent Test**: Given reconciliation output covering all category families, each surfaced exception has exactly one primary category and correct domain; orphaned and Mex ID fixtures produce expected categories.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T017 [P] [US3] Add `MismatchMappingTests` skeleton in `tests/BillDrift.Application.Tests/ExceptionSurfacing/MismatchMappingTests.cs` (one case per `ExceptionCategory`)
- [X] T018 [P] [US3] Add `DerivedDetectorTests` skeleton in `tests/BillDrift.Application.Tests/ExceptionSurfacing/DerivedDetectorTests.cs`

### Implementation for User Story 3

- [X] T019 [US3] Implement `MismatchToExceptionMapper` in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Mapping/MismatchToExceptionMapper.cs` per `contracts/mismatch-to-exception-mapping.md`
- [X] T020 [P] [US3] Implement `OrphanedStripeDetector` in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Detection/OrphanedStripeDetector.cs` per research R8
- [X] T021 [P] [US3] Implement `MexIdMismatchDetector` in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Detection/MexIdMismatchDetector.cs`
- [X] T022 [P] [US3] Implement `ProductMismatchDetector` in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Detection/ProductMismatchDetector.cs`
- [X] T023 [US3] Implement `CollectPhase` invoking mapper and derived detectors in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Phases/CollectPhase.cs`
- [X] T024 [P] [US3] Create `tests/fixtures/exception-surfacing/orphaned-stripe-item.json` per `quickstart.md` Scenario 4
- [X] T025 [P] [US3] Create `tests/fixtures/exception-surfacing/mex-id-mismatch.json`
- [X] T026 [US3] Add primary `MismatchType` mapping tests (all seven engine types) in `MismatchMappingTests.cs`
- [X] T027 [US3] Add `CatalogueMissing` subdivision tests (product missing vs price missing vs RRP mismatch) in `MismatchMappingTests.cs`
- [X] T028 [US3] Add derived detector tests for orphaned Stripe and Mex ID mismatch in `DerivedDetectorTests.cs`
- [X] T029 [US3] Add non-CSP → `NonCspManualReview` mapping test in `MismatchMappingTests.cs` per SC-008

**Checkpoint**: Collect phase produces categorised exceptions with correct domains; category mapping unit-tested.

---

## Phase 4: User Story 2 — Understand Each Exception with Clear Explanation and Evidence (Priority: P1)

**Goal**: Every surfaced exception includes plain-language explanation and labelled multi-source evidence for operator verification.

**Independent Test**: Given fixtures with quantity mismatch, mapping ambiguity, and catalogue price delta, each exception includes explanation text and evidence items from contributing sources.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T030 [P] [US2] Add `EvidenceBuilderTests` skeleton in `tests/BillDrift.Application.Tests/ExceptionSurfacing/EvidenceBuilderTests.cs`

### Implementation for User Story 2

- [X] T031 [US2] Implement `EvidenceBuilder` in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Evidence/EvidenceBuilder.cs` per research R6 and FR-019
- [X] T032 [US2] Integrate `EvidenceBuilder` into `CollectPhase` and enrich `MismatchToExceptionMapper` explanation templates per `contracts/mismatch-to-exception-mapping.md`
- [X] T033 [US2] Add quantity mismatch evidence test (subscription truth + Stripe sources) in `EvidenceBuilderTests.cs` per spec US2 acceptance scenario 1
- [X] T034 [US2] Add mapping-ambiguous evidence test listing all candidates in `EvidenceBuilderTests.cs` per spec US2 acceptance scenario 2
- [X] T035 [US2] Add catalogue price mismatch evidence test (intended RRP + Stripe amount + frequency) in `EvidenceBuilderTests.cs` per spec US2 acceptance scenario 3

**Checkpoint**: Exceptions include non-empty explanations and labelled evidence bundles.

---

## Phase 5: User Story 1 — Review Prioritised Exceptions for a Customer (Priority: P1) 🎯 MVP

**Goal**: Group exceptions by customer; order customer groups and exceptions by severity and action urgency; expose run summary counts.

**Independent Test**: Given mixed-severity exceptions across three customers, customer with Error appears first; group summaries show severity and action-required counts; repeated surfacing is identical.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T036 [P] [US1] Add `OrderingTests` skeleton in `tests/BillDrift.Application.Tests/ExceptionSurfacing/OrderingTests.cs`
- [X] T037 [P] [US1] Add `ExceptionSurfacingServiceTests` integration skeleton in `tests/BillDrift.Application.Tests/ExceptionSurfacing/ExceptionSurfacingServiceTests.cs`

### Implementation for User Story 1

- [X] T038 [US1] Implement `ExceptionOrdering` in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Ordering/ExceptionOrdering.cs` per `contracts/suppression-and-ordering-rules.md`
- [X] T039 [US1] Implement `FinalizePhase` (summary counts, customer groups, `RequiresActionNow` per AR rules) in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Phases/FinalizePhase.cs`
- [X] T040 [P] [US1] Create `tests/fixtures/exception-surfacing/mixed-three-customers.json` per `quickstart.md` Scenario 1
- [X] T041 [US1] Add customer group ordering test (Error customer before Warning-only) in `OrderingTests.cs`
- [X] T042 [US1] Add within-group ordering test (severity → action urgency → category priority) in `OrderingTests.cs`
- [X] T043 [US1] Add mixed-three-customers integration test asserting `Summary.BySeverity` and `RequiresActionNowCount` in `ExceptionSurfacingServiceTests.cs`

**Checkpoint**: MVP — prioritised customer groups with summary statistics; ordering tests pass.

---

## Phase 6: User Story 4 — Avoid Noise from Duplicate or Low-Confidence False Positives (Priority: P2)

**Goal**: Suppress dependent exceptions, consolidate catalogue gaps, strip low-confidence proposed actions; surface fewer distinct problems than raw mismatch count.

**Independent Test**: Fixtures with cascading mismatches produce fewer surfaced exceptions than raw mismatches while preserving root-cause exception; low-confidence runs have no bill-impacting action flags.

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T044 [P] [US4] Add `SuppressionRulesTests` skeleton in `tests/BillDrift.Application.Tests/ExceptionSurfacing/SuppressionRulesTests.cs`
- [X] T045 [P] [US4] Add `ConsolidationTests` skeleton in `tests/BillDrift.Application.Tests/ExceptionSurfacing/ConsolidationTests.cs`

### Implementation for User Story 4

- [X] T046 [US4] Implement `SuppressPhase` (SR-1 through SR-5) in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Phases/SuppressPhase.cs` per `contracts/suppression-and-ordering-rules.md`
- [X] T047 [US4] Implement `ConsolidatePhase` (CR-1) in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Phases/ConsolidatePhase.cs`
- [X] T048 [P] [US4] Create `tests/fixtures/exception-surfacing/suppression-mapping-root-cause.json` per `quickstart.md` Scenario 3
- [X] T049 [P] [US4] Create `tests/fixtures/exception-surfacing/catalogue-consolidation.json` per `quickstart.md` Scenario 5
- [X] T050 [P] [US4] Create `tests/fixtures/exception-surfacing/low-confidence-no-action.json` per `quickstart.md` Scenario 6
- [X] T051 [US4] Add SR-1 mapping root-cause suppression test in `SuppressionRulesTests.cs`
- [X] T052 [US4] Add SR-3 low-confidence `ProposedChangeId` strip test in `SuppressionRulesTests.cs` per SC-005
- [X] T053 [US4] Add CR-1 catalogue consolidation test in `ConsolidationTests.cs`
- [X] T054 [US4] Add suppressed count audit test asserting surfaced count < raw mismatch count in `SuppressionRulesTests.cs` per SC-004

**Checkpoint**: False-positive controls active; suppression and consolidation unit-tested.

---

## Phase 7: User Story 5 — Consume a UI-Ready View Model Without Presentation Logic (Priority: P2)

**Goal**: Complete pipeline integration; expose `FlatExceptions()`, empty-state handling, and deterministic output for API/UI consumers.

**Independent Test**: `Surface(run)` returns stable view model graph; empty run has `HasExceptions == false`; flat list derivation matches grouped order.

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T055 [P] [US5] Add `DeterminismTests` skeleton in `tests/BillDrift.Application.Tests/ExceptionSurfacing/DeterminismTests.cs`

### Implementation for User Story 5

- [X] T056 [US5] Implement `FlatExceptions()` on `ReconciliationExceptionViewModel` in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/ReconciliationExceptionViewModel.cs` per spec US5
- [X] T057 [US5] Wire all phases in `ExceptionSurfacingService.Surface()` in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/ExceptionSurfacingService.cs` per `contracts/exception-surfacing-pipeline.md`
- [X] T058 [P] [US5] Create `tests/fixtures/exception-surfacing/clean-run-empty.json` per `quickstart.md` Scenario 7
- [X] T059 [US5] Add empty run `HasExceptions=false` and zero-count summary test in `ExceptionSurfacingServiceTests.cs`
- [X] T060 [US5] Add `FlatExceptions()` ordering matches grouped structure test in `DeterminismTests.cs`
- [X] T061 [US5] Add double-`Surface` determinism test excluding `GeneratedAt` in `DeterminismTests.cs` per SC-002
- [X] T062 [US5] Add `ProposedChangeId` reference test when eligible proposed change exists in `ExceptionSurfacingServiceTests.cs`

**Checkpoint**: Full surfacing pipeline complete; view model ready for future UI/API consumption.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Comment audit, full test suite, and quickstart validation.

- [X] T063 [P] Audit billing-critical comments on suppression, mapping, and derived detection rules per constitution Principle I
- [X] T064 Run `dotnet test tests/BillDrift.Application.Tests --filter "FullyQualifiedName~ExceptionSurfacing"` and fix failures
- [X] T065 Validate all scenarios in `specs/005-reconciliation-exceptions/quickstart.md`; document coverage map in `tests/fixtures/exception-surfacing/README.md`

**Checkpoint**: All exception surfacing tests pass; quickstart scenarios covered.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **US3 (Phase 3)**: Depends on Foundational — provides categorised `CollectPhase` output
- **US2 (Phase 4)**: Depends on US3 CollectPhase — enriches exceptions with evidence
- **US1 (Phase 5)**: Depends on US2 — **MVP** grouping/ordering on enriched exceptions
- **US4 (Phase 6)**: Depends on US1 FinalizePhase stubs replaced — suppression runs before finalize in pipeline; implement Suppress/Consolidate then re-wire service order
- **US5 (Phase 7)**: Depends on US4 — full pipeline integration
- **Polish (Phase 8)**: Depends on US5 completion

### User Story Dependencies

| Story | Depends on | Delivers independently |
|-------|------------|------------------------|
| US3 (P1) | Foundational | Category mapping + derived detection via unit tests |
| US2 (P1) | US3 | Evidence bundles via unit tests |
| US1 (P1) | US2 | **MVP** — prioritised customer groups + summary |
| US4 (P2) | US1 | Suppression/consolidation via unit tests |
| US5 (P2) | US4 | Complete view model + determinism |

### Within Each User Story

- Tests marked in story phases MUST fail before implementation (constitution II)
- Mapper/detectors before CollectPhase integration
- Suppress/Consolidate before full service wiring
- Story checkpoint before next priority

### Parallel Opportunities

- **Phase 1**: T002, T003, T004, T005 in parallel
- **Phase 2**: T011–T014, T015 in parallel after T007–T010
- **Phase 3**: T017–T018, T020–T022, T024–T025 in parallel
- **Phase 4**: T030 in parallel with late US3 tasks
- **Phase 5**: T036–T037, T040 in parallel
- **Phase 6**: T044–T045, T048–T050 in parallel
- **Phase 7**: T055, T058 in parallel
- **Phase 8**: T063 in parallel with T064 prep

---

## Parallel Example: User Story 3

```bash
# Launch all US3 test skeletons and detectors together:
Task: "MismatchMappingTests skeleton in tests/BillDrift.Application.Tests/ExceptionSurfacing/MismatchMappingTests.cs"
Task: "DerivedDetectorTests skeleton in tests/BillDrift.Application.Tests/ExceptionSurfacing/DerivedDetectorTests.cs"
Task: "OrphanedStripeDetector in src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Detection/OrphanedStripeDetector.cs"
Task: "MexIdMismatchDetector in src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Detection/MexIdMismatchDetector.cs"
Task: "ProductMismatchDetector in src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Detection/ProductMismatchDetector.cs"
```

---

## Parallel Example: User Story 4 Fixtures

```bash
# Create all US4 fixtures in parallel:
Task: "suppression-mapping-root-cause.json"
Task: "catalogue-consolidation.json"
Task: "low-confidence-no-action.json"
```

---

## Implementation Strategy

### MVP First (User Story 1 path)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (**CRITICAL**)
3. Complete Phase 3: US3 (category mapping + collect)
4. Complete Phase 4: US2 (evidence)
5. Complete Phase 5: US1 (grouping/ordering/summary)
6. **STOP and VALIDATE**: Run `OrderingTests` + `ExceptionSurfacingServiceTests` with `mixed-three-customers.json`
7. Add US4 suppression + US5 integration
8. Polish

### Incremental Delivery

| Increment | Stories | Value |
|-----------|---------|-------|
| MVP | Setup + Foundational + US3 + US2 + US1 | Prioritised, categorised exceptions with evidence per customer |
| +Trust | US4 | False-positive suppression and catalogue consolidation |
| +Complete | US5 | Full view model API, determinism, empty state |
| +Polish | Phase 8 | Production-ready comments and quickstart coverage |

### Parallel Team Strategy

With multiple developers after Phase 2:

- **Developer A**: US3 mapping + derived detectors
- **Developer B**: US2 evidence builder (after T023 CollectPhase stub exists)
- Once US3+US2 land → **Developer A**: US1 ordering/finalize; **Developer B**: US4 suppression/consolidation
- **Developer C**: US5 integration + determinism tests after US4

---

## Notes

- Domain types in `BillDrift.Domain.Reconciliation` are unchanged (001); surfacing view models live in Application only
- Fixtures may chain `IReconciliationEngine.Execute` on existing `tests/fixtures/reconciliation/` inputs or define `ReconciliationRequest` JSON per `ExceptionSurfacingTestBuilder`
- Pipeline order in service: Collect → Suppress → Consolidate → Finalize (re-wire in T057 after US4 phases implemented)
- `[P]` tasks touch different files — safe to parallelize within phase constraints above
- Commit after each task or logical group
- Stop at any **Checkpoint** to validate story independence before continuing
