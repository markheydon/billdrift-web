# Tasks: Stripe Catalogue Reconciliation

**Input**: Design documents from `/specs/011-stripe-catalogue-reconciliation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md; `001-billing-domain-model` (`ProductMapping`, `IntendedPrice`, `CommercialKey`, `ProposedChange`); `003-stripe-csv-ingestion` (`RawStripeProduct`, `RawStripePrice`, ingestion blob archive); `004-reconciliation-engine` (`ProductMappingIndex`, `IntendedPriceIndex`, `ProposedChangeFactory` patterns); `007-reconciliation-approval-workflow` (`IApprovalStore`, `ApprovalEligibility`); `010-retail-pricing-ingestion` (resolved prices blob)

**UI note**: Blazor catalogue reconciliation UI is **out of scope** — API endpoints only. `BillDrift.Web` deferred; no storage clients in Web project.

**Storage note**: **Azure Blob + Table only, no SQL**; `BlobServiceClient` and `TableServiceClient` via Aspire DI in API/Infrastructure only — no manual connection string construction.

**Tests**: Included per constitution Principle II, `quickstart.md` validation scenarios, and `contracts/catalogue-check-rules.md`.

**Organization**: Tasks grouped by user story for independent implementation and testing. Persistence + API in US5 after engine stories.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US5) for story-phase tasks only

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Fixture layout and verify shared dependencies.

- [ ] T001 Verify `Azure.Data.Tables` and `Azure.Storage.Blobs` package references exist in `src/BillDrift.Infrastructure/BillDrift.Infrastructure.csproj` (reuse from 008/010)
- [ ] T002 [P] Create `tests/fixtures/catalogue-reconciliation/` directory structure per `quickstart.md`
- [ ] T003 [P] Add `tests/fixtures/catalogue-reconciliation/README.md` documenting required JSON fixtures and mapping snapshot format
- [ ] T004 [P] Add `tests/fixtures/product-mappings/sample-mappings.json` minimal `ProductMapping` fixture for engine tests

**Checkpoint**: Fixture directories ready; Azure packages confirmed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain types, Application contracts, catalogue normalizer, snapshot index, pipeline skeleton, and storage interfaces. MUST complete before user story implementation.

**⚠️ CRITICAL**: No user story work until this phase is complete.

- [ ] T005 Implement `CatalogueRunId` value object in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueRunId.cs` per `data-model.md`
- [ ] T006 [P] Implement `CatalogueExceptionId` and `CatalogueProposedFixId` value objects in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueExceptionId.cs` and `CatalogueProposedFixId.cs`
- [ ] T007 [P] Implement `CatalogueExceptionType` enum in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueExceptionType.cs` per `data-model.md`
- [ ] T008 [P] Implement `CatalogueProposedActionType` enum in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueProposedActionType.cs` per `data-model.md`
- [ ] T009 [P] Implement `StripeCatalogueProduct` record in `src/BillDrift.Domain/CatalogueReconciliation/StripeCatalogueProduct.cs` per `data-model.md`
- [ ] T010 [P] Implement `StripeCataloguePrice` record in `src/BillDrift.Domain/CatalogueReconciliation/StripeCataloguePrice.cs` per `data-model.md`
- [ ] T011 [P] Implement `CatalogueInputReferences` record in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueInputReferences.cs`
- [ ] T012 Implement `CatalogueReconciliationInputs` record in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueReconciliationInputs.cs` per `data-model.md`
- [ ] T013 [P] Implement `CatalogueReconciliationOptions` record in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueReconciliationOptions.cs` per `data-model.md`
- [ ] T014 [P] Implement `CatalogueException` record in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueException.cs` per `data-model.md`
- [ ] T015 [P] Implement `CatalogueProposedFix` record in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueProposedFix.cs` per `data-model.md`
- [ ] T016 [P] Implement `CatalogueReconciliationSummary` record in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueReconciliationSummary.cs`
- [ ] T017 Implement `CatalogueReconciliationRun` record in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueReconciliationRun.cs` per `data-model.md`
- [ ] T018 Implement `ICatalogueReconciliationEngine` interface in `src/BillDrift.Application/CatalogueReconciliation/ICatalogueReconciliationEngine.cs` per `contracts/catalogue-reconciliation-pipeline.md`
- [ ] T019 [P] Implement `IStripeCatalogueNormalizer` interface in `src/BillDrift.Application/CatalogueReconciliation/IStripeCatalogueNormalizer.cs`
- [ ] T020 [P] Implement `ICatalogueReconciliationStore` interface in `src/BillDrift.Application/CatalogueReconciliation/ICatalogueReconciliationStore.cs` per `contracts/azure-blob-catalogue-run-archive.md`
- [ ] T021 [P] Implement `ICatalogueReconciliationService` interface in `src/BillDrift.Application/CatalogueReconciliation/ICatalogueReconciliationService.cs` per `contracts/catalogue-reconciliation-api-endpoints.md`
- [ ] T022 Implement `StripeCatalogueNormalizer` in `src/BillDrift.Application/CatalogueReconciliation/StripeCatalogueNormalizer.cs` mapping `RawStripeProduct`/`RawStripePrice` to catalogue snapshots with offer/SKU metadata parsing
- [ ] T023 Implement `StripeCatalogueSnapshotIndex` in `src/BillDrift.Application/CatalogueReconciliation/StripeCatalogueSnapshotIndex.cs` with product/price lookup by ID, commercial key root, and interval per research R2
- [ ] T024 Implement `CatalogueReconciliationContext` in `src/BillDrift.Application/CatalogueReconciliation/CatalogueReconciliationContext.cs` holding indexes, options, exceptions, and proposed fixes collections
- [ ] T025 [P] Implement pipeline stage interfaces and `CatalogueReconciliationPipeline` skeleton in `src/BillDrift.Application/CatalogueReconciliation/CatalogueReconciliationPipeline.cs` per `contracts/catalogue-reconciliation-pipeline.md`
- [ ] T026 [P] Implement `ValidateInputsStage` in `src/BillDrift.Application/CatalogueReconciliation/Stages/ValidateInputsStage.cs`
- [ ] T027 [P] Implement `BuildIndexesStage` reusing `ProductMappingIndex` and `IntendedPriceIndex` in `src/BillDrift.Application/CatalogueReconciliation/Stages/BuildIndexesStage.cs`
- [ ] T028 [P] Implement `OrderOutputStage` deterministic ordering in `src/BillDrift.Application/CatalogueReconciliation/Stages/OrderOutputStage.cs`
- [ ] T029 [P] Implement `CatalogueReconciliationStorageOptions` in `src/BillDrift.Infrastructure/CatalogueReconciliation/CatalogueReconciliationStorageOptions.cs` (container/table names)
- [ ] T030 [P] Implement `InMemoryCatalogueReconciliationStore` in `src/BillDrift.Infrastructure/CatalogueReconciliation/InMemoryCatalogueReconciliationStore.cs` mirroring blob path layout for tests

**Checkpoint**: Domain types compile; normalizer and index available; pipeline skeleton and in-memory store ready.

---

## Phase 3: User Story 1 — Reconcile Stripe Catalogue Against Intended Retail Pricing (Priority: P1) 🎯 MVP

**Goal**: For each mapped product with intended pricing, verify Stripe product exists, required prices exist for term/frequency combos, and unit amounts match intended RRP.

**Independent Test**: Given `catalogue-clean-match.json`, zero exceptions; given `catalogue-missing-product.json`, `catalogue-missing-price.json`, `catalogue-incorrect-price.json` — each emits the correct `CatalogueExceptionType` per `quickstart.md` scenarios 2–4.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T031 [P] [US1] Create engine test skeleton in `tests/BillDrift.Application.Tests/CatalogueReconciliation/CatalogueReconciliationEngineTests.cs`
- [ ] T032 [P] [US1] Add clean-match assertion test in `CatalogueReconciliationEngineTests.cs` per `quickstart.md` scenario 1
- [ ] T033 [P] [US1] Add missing-product, missing-price, and incorrect-price tests in `CatalogueReconciliationEngineTests.cs` per `quickstart.md` scenarios 2–4
- [ ] T034 [P] [US1] Create JSON fixtures `catalogue-clean-match.json`, `catalogue-missing-product.json`, `catalogue-missing-price.json`, `catalogue-incorrect-price.json` under `tests/fixtures/catalogue-reconciliation/`

### Implementation for User Story 1

- [ ] T035 [P] [US1] Implement `CatalogueExceptionFactory` for `MissingProduct`, `MissingPrice`, `IncorrectPrice` rules CAT-001–CAT-003 in `src/BillDrift.Application/CatalogueReconciliation/Detection/CatalogueExceptionFactory.cs` per `contracts/catalogue-check-rules.md`
- [ ] T036 [US1] Implement `ReconcileMappedProductsStage` product resolution (by `StripeProductId` or metadata) in `src/BillDrift.Application/CatalogueReconciliation/Stages/ReconcileMappedProductsStage.cs`
- [ ] T037 [US1] Add required price slot iteration and RRP amount comparison (exact minor units, currency match) in `ReconcileMappedProductsStage.cs` with mandatory comments on Stripe price immutability semantics
- [ ] T038 [US1] Implement `CatalogueReconciliationEngine` orchestrating validate → build indexes → reconcile mapped products → order output in `src/BillDrift.Application/CatalogueReconciliation/CatalogueReconciliationEngine.cs`
- [ ] T039 [US1] Wire `ICatalogueReconciliationEngine` registration in `src/BillDrift.Infrastructure/CatalogueReconciliation/CatalogueReconciliationServiceCollectionExtensions.cs`
- [ ] T040 [US1] Complete US1 engine tests in `CatalogueReconciliationEngineTests.cs` — all scenario 1–4 assertions pass

**Checkpoint**: MVP engine detects missing products, missing prices, and incorrect RRP without subscriptions or Azure.

---

## Phase 4: User Story 2 — Detect Duplicate and Conflicting Catalogue Entries (Priority: P1)

**Goal**: Flag multiple Stripe products for the same offer/SKU and multiple active prices for the same product interval + currency; proposals limited to manual cleanup.

**Independent Test**: Given `catalogue-duplicate-products.json` and `catalogue-duplicate-prices.json`, emits duplicate exceptions with no auto-merge/delete proposals per `quickstart.md` scenarios 5–6.

### Tests for User Story 2

- [ ] T041 [P] [US2] Add duplicate-product test in `CatalogueReconciliationEngineTests.cs` per `quickstart.md` scenario 5
- [ ] T042 [P] [US2] Add duplicate-price test in `CatalogueReconciliationEngineTests.cs` per `quickstart.md` scenario 6
- [ ] T043 [P] [US2] Create JSON fixtures `catalogue-duplicate-products.json` and `catalogue-duplicate-prices.json` under `tests/fixtures/catalogue-reconciliation/`

### Implementation for User Story 2

- [ ] T044 [US2] Implement `DetectDuplicateConflictsStage` in `src/BillDrift.Application/CatalogueReconciliation/Stages/DetectDuplicateConflictsStage.cs` per rules CAT-004–CAT-005
- [ ] T045 [US2] Add duplicate-product and duplicate-price factory methods in `CatalogueExceptionFactory.cs`
- [ ] T046 [US2] Insert `DetectDuplicateConflictsStage` before `ReconcileMappedProductsStage` in `CatalogueReconciliationPipeline.cs` per pipeline contract
- [ ] T047 [US2] Add global guard suppressing `MissingProduct` when duplicate-product conflict exists on same root in `ReconcileMappedProductsStage.cs`
- [ ] T048 [US2] Complete US2 tests in `CatalogueReconciliationEngineTests.cs`

**Checkpoint**: Duplicate detection works independently; no destructive proposed actions.

---

## Phase 5: User Story 3 — Propose Catalogue Fixes for Human Approval (Priority: P1)

**Goal**: Attach approval-ready `CatalogueProposedFix` for each actionable exception; duplicate/conflict flags are non-actionable manual cleanup.

**Independent Test**: Missing product → `CreateProduct`; missing price → `CreatePrice`; incorrect price → `CreateReplacementPrice`; duplicates → `FlagManualCleanup` with `IsActionable = false`.

### Tests for User Story 3

- [ ] T049 [P] [US3] Add proposed-fix presence assertions per exception type in `CatalogueReconciliationEngineTests.cs`
- [ ] T050 [P] [US3] Create `CatalogueApprovalAdapterTests.cs` in `tests/BillDrift.Application.Tests/CatalogueReconciliation/CatalogueApprovalAdapterTests.cs` per `contracts/approval-integration.md`

### Implementation for User Story 3

- [ ] T051 [US3] Implement `CatalogueProposedFixFactory` in `src/BillDrift.Application/CatalogueReconciliation/Detection/CatalogueProposedFixFactory.cs` for `CreateProduct`, `CreatePrice`, `CreateReplacementPrice`, `FlagManualCleanup` per `contracts/catalogue-check-rules.md`
- [ ] T052 [US3] Implement `AttachProposedFixesStage` in `src/BillDrift.Application/CatalogueReconciliation/Stages/AttachProposedFixesStage.cs` linking fixes to exceptions
- [ ] T053 [US3] Wire `AttachProposedFixesStage` into `CatalogueReconciliationPipeline.cs` before `OrderOutputStage`
- [ ] T054 [US3] Populate `CatalogueReconciliationSummary` counts (`ProposedFixesActionable`, `ProposedFixesManualOnly`) in `CatalogueReconciliationEngine.cs`
- [ ] T055 [US3] Implement `CatalogueApprovalAdapter` mapping to `ApprovalProposal` in `src/BillDrift.Application/CatalogueReconciliation/CatalogueApprovalAdapter.cs` per `contracts/approval-integration.md`
- [ ] T056 [US3] Complete approval adapter tests in `CatalogueApprovalAdapterTests.cs` (eligible vs `CatalogueConflict` eligibility)

**Checkpoint**: Engine emits proposed fixes; adapter maps to approval workflow types.

---

## Phase 6: User Story 4 — Scope Reconciliation to Canonical Mapped Products (Priority: P2)

**Goal**: Drive checks from canonical mapping + intended pricing; surface pricing-reference gaps, mapping ambiguity, and unmapped Stripe catalogue entries.

**Independent Test**: Given `catalogue-pricing-gap.json`, `catalogue-unmapped-stripe.json`, and `catalogue-manual-override-rrp.json` — correct gap, unmapped, and override RRP behaviour per `quickstart.md` scenarios 7–9.

### Tests for User Story 4

- [ ] T057 [P] [US4] Add pricing-reference-gap test in `CatalogueReconciliationEngineTests.cs` per `quickstart.md` scenario 7
- [ ] T058 [P] [US4] Add unmapped Stripe entry test in `CatalogueReconciliationEngineTests.cs` per `quickstart.md` scenario 8
- [ ] T059 [P] [US4] Add manual-override RRP precedence test in `CatalogueReconciliationEngineTests.cs` per `quickstart.md` scenario 9
- [ ] T060 [P] [US4] Create JSON fixtures `catalogue-pricing-gap.json`, `catalogue-unmapped-stripe.json`, `catalogue-manual-override-rrp.json` under `tests/fixtures/catalogue-reconciliation/`

### Implementation for User Story 4

- [ ] T061 [US4] Implement `DetectUnmappedCatalogueStage` in `src/BillDrift.Application/CatalogueReconciliation/Stages/DetectUnmappedCatalogueStage.cs` per rule CAT-008
- [ ] T062 [US4] Add `PricingReferenceGap` and `MappingAmbiguous` handling in `ReconcileMappedProductsStage.cs` per rules CAT-006–CAT-007
- [ ] T063 [US4] Respect `IncludeNonCspProducts` option and mapping confidence guards in `ReconcileMappedProductsStage.cs`
- [ ] T064 [US4] Wire `DetectUnmappedCatalogueStage` into `CatalogueReconciliationPipeline.cs`
- [ ] T065 [US4] Extend `CatalogueReconciliationSummary` with `UnmappedStripeProducts` / `UnmappedStripePrices` rollups in `CatalogueReconciliationEngine.cs`
- [ ] T066 [US4] Complete US4 tests in `CatalogueReconciliationEngineTests.cs`

**Checkpoint**: Scoped iteration and orphan catalogue surfacing work without API layer.

---

## Phase 7: User Story 5 — Run Catalogue Reconciliation from Export Snapshots (Priority: P2)

**Goal**: Orchestrate runs from ingested Stripe + pricing archives, persist to Azure Blob/Table via Aspire DI, expose API trigger and list/detail endpoints.

**Independent Test**: `POST /api/catalogue-reconciliation/runs` with ingestion run IDs returns summary; run persisted and listable; identical inputs produce identical output (determinism).

### Tests for User Story 5

- [ ] T067 [P] [US5] Create `DeterminismTests.cs` in `tests/BillDrift.Application.Tests/CatalogueReconciliation/DeterminismTests.cs` per `quickstart.md` scenario 10
- [ ] T068 [P] [US5] Create `AzureCatalogueReconciliationStoreTests.cs` in `tests/BillDrift.Infrastructure.Tests/CatalogueReconciliation/AzureCatalogueReconciliationStoreTests.cs` for Azurite blob/table round-trip
- [ ] T069 [P] [US5] Create JSON fixture `catalogue-determinism.json` under `tests/fixtures/catalogue-reconciliation/`

### Implementation for User Story 5

- [ ] T070 [US5] Implement `CatalogueReconciliationJsonSerializerContext` in `src/BillDrift.Infrastructure/CatalogueReconciliation/CatalogueReconciliationJsonSerializerContext.cs` for blob payloads
- [ ] T071 [US5] Implement `AzureCatalogueReconciliationStore` with constructor-injected `BlobServiceClient` and `TableServiceClient` in `src/BillDrift.Infrastructure/CatalogueReconciliation/AzureCatalogueReconciliationStore.cs` per `contracts/azure-blob-catalogue-run-archive.md` and `contracts/azure-table-catalogue-run-schema.md` — **no manual connection strings**
- [ ] T072 [US5] Implement `CatalogueReconciliationService` assembling inputs from `IIngestionBlobStore` (Stripe products/prices + resolved pricing) in `src/BillDrift.Application/CatalogueReconciliation/CatalogueReconciliationService.cs`
- [ ] T073 [US5] Add run persistence (blob archive + table index) to `CatalogueReconciliationService.cs` after engine execution
- [ ] T074 [US5] Implement `CatalogueReconciliationEndpoints.cs` (`POST /runs`, `GET /runs`, `GET /runs/{id}`, `POST /runs/{id}/ingest-approvals`) in `src/BillDrift.Api/CatalogueReconciliation/CatalogueReconciliationEndpoints.cs` per `contracts/catalogue-reconciliation-api-endpoints.md`
- [ ] T075 [US5] Register `AddCatalogueReconciliation()` and `AddCatalogueReconciliationStorage()` in `src/BillDrift.Infrastructure/CatalogueReconciliation/CatalogueReconciliationServiceCollectionExtensions.cs` and wire `MapCatalogueReconciliationEndpoints()` in `src/BillDrift.Api/Program.cs`
- [ ] T076 [US5] Complete Azurite integration tests in `AzureCatalogueReconciliationStoreTests.cs`
- [ ] T077 [US5] Complete determinism tests in `DeterminismTests.cs`
- [ ] T078 [US5] Implement optional `ingestToApprovalQueue` path calling `CatalogueApprovalAdapter` + `IApprovalStore` in `CatalogueReconciliationService.cs`

**Checkpoint**: End-to-end API trigger, Azure persistence, and approval ingestion functional.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Golden comparer, index unit tests, quickstart validation, build quality.

- [ ] T079 [P] Implement `GoldenRunComparer` for catalogue runs in `tests/BillDrift.Application.Tests/CatalogueReconciliation/GoldenRunComparer.cs`
- [ ] T080 [P] Add `StripeCatalogueSnapshotIndexTests.cs` in `tests/BillDrift.Application.Tests/CatalogueReconciliation/StripeCatalogueSnapshotIndexTests.cs`
- [ ] T081 [P] Add `StripeCatalogueNormalizerTests.cs` in `tests/BillDrift.Application.Tests/CatalogueReconciliation/StripeCatalogueNormalizerTests.cs`
- [ ] T082 Run full `quickstart.md` validation checklist and document any gaps in `specs/011-stripe-catalogue-reconciliation/quickstart.md` Notes section if needed
- [ ] T083 Run `dotnet build --no-incremental` from solution root and resolve all errors and warnings
- [ ] T084 Run `dotnet test` from solution root and resolve all failures

**Checkpoint**: Clean build, all tests pass, quickstart scenarios verified.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **US1 (Phase 3)**: Depends on Foundational — **MVP**
- **US2 (Phase 4)**: Depends on US1 pipeline skeleton (stages wired)
- **US3 (Phase 5)**: Depends on US1 exceptions (fixes attach to exceptions)
- **US4 (Phase 6)**: Depends on US1 reconcile stage; parallel with US2/US3 after US1
- **US5 (Phase 7)**: Depends on US1–US3 engine complete; needs full run output for persistence
- **Polish (Phase 8)**: Depends on US5

### User Story Dependencies

| Story | Depends on | Independent test |
|-------|------------|------------------|
| US1 (P1) | Foundational | Engine JSON fixtures, no Azure |
| US2 (P1) | US1 pipeline | Duplicate fixtures only |
| US3 (P1) | US1 exceptions | Fix + adapter unit tests |
| US4 (P2) | US1 reconcile stage | Gap/unmapped/override fixtures |
| US5 (P2) | US1–US3 engine | API + Azurite integration |

### Parallel Opportunities

- **Phase 1**: T002, T003, T004 in parallel
- **Phase 2**: T006–T011, T013–T016, T019–T021, T025–T030 in parallel after T005/T012/T017/T018
- **US1 tests**: T031–T034 in parallel before implementation
- **US2–US4**: Can proceed in parallel once US1 checkpoint reached (different stage files)
- **Polish**: T079–T081 in parallel

### Parallel Example: User Story 1

```bash
# Tests and fixtures first (parallel):
T031 CatalogueReconciliationEngineTests.cs skeleton
T032 clean-match test
T033 missing/incorrect tests
T034 JSON fixtures

# Then implementation (sequential on ReconcileMappedProductsStage):
T035 CatalogueExceptionFactory.cs
T036–T037 ReconcileMappedProductsStage.cs
T038 CatalogueReconciliationEngine.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Run `CatalogueReconciliationEngineTests` — scenarios 1–4 pass
5. Demo catalogue gap detection without Azure or API

### Incremental Delivery

1. Setup + Foundational → engine skeleton ready
2. US1 → core RRP validation (MVP)
3. US2 → duplicate hygiene
4. US3 → approval-ready fixes
5. US4 → scoped iteration + unmapped surfacing
6. US5 → persistence + API (production operator workflow)
7. Polish → CI green

### Suggested MVP Scope

**User Story 1 only** (Phases 1–3): delivers missing product, missing price, and incorrect RRP detection via pure engine + unit tests — highest value for catalogue hygiene before subscription reconciliation.

---

## Notes

- Reuse `ProductMappingIndex`, `IntendedPriceIndex`, and `IntendedPriceResolver` from 004 — do not duplicate pricing precedence logic
- `StripeCatalogueSnapshotIndex` replaces subscription-derived `StripeCatalogueIndex` for this feature only
- Incorrect Stripe prices always propose **create replacement** — document in code comments (constitution I)
- No `BlobServiceClient`/`TableServiceClient` in `BillDrift.Web` or `BillDrift.Application` — Infrastructure only
- Product mappings accepted inline on API v1; dedicated mapping persistence is future work
