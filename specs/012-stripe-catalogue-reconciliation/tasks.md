# Tasks: Stripe Catalogue Reconciliation

**Input**: Design documents from `/specs/012-stripe-catalogue-reconciliation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md; `001-billing-domain-model` (`ProductMapping`, `IntendedPrice`, `CommercialKey`, `ProposedChange`); `003-stripe-csv-ingestion` (`RawStripeProduct`, `RawStripePrice`); `004-reconciliation-engine` (`ProductMappingIndex`, `IntendedPriceIndex`); `007-reconciliation-approval-workflow` (`IApprovalStore`, `ApprovalEligibility`); `010-retail-pricing-ingestion` (resolved prices blob)

**UI note**: Blazor catalogue reconciliation UI is **out of scope** — API endpoints only. `BillDrift.Web` deferred; no storage clients in Web project.

**Storage note**: **Azure Blob + Table only, no SQL**; `BlobServiceClient` and `TableServiceClient` via Aspire DI in API/Infrastructure only — no manual connection string construction.

**Implementation status**: All tasks T001–T091 complete on branch `012-stripe-catalogue-reconciliation`.

**Tests**: Included per constitution Principle II, `quickstart.md` validation scenarios, and `contracts/catalogue-check-rules.md`.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US5) for story-phase tasks only

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Fixture layout and verify shared dependencies.

- [X] T001 Verify `Azure.Data.Tables` and `Azure.Storage.Blobs` package references exist in `src/BillDrift.Infrastructure/BillDrift.Infrastructure.csproj` (reuse from 008/010)
- [X] T002 [P] Create `tests/fixtures/catalogue-reconciliation/` directory structure per `quickstart.md`
- [X] T003 [P] Add `tests/fixtures/catalogue-reconciliation/README.md` documenting required JSON fixtures and mapping snapshot format
- [X] T004 [P] Add `tests/fixtures/product-mappings/sample-mappings.json` minimal `ProductMapping` fixture for engine tests

**Checkpoint**: Fixture directories ready; Azure packages confirmed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain types, Application contracts, catalogue normalizer, snapshot index, pipeline skeleton, and storage interfaces. MUST complete before user story implementation.

**⚠️ CRITICAL**: No user story work until this phase is complete.

- [X] T005 Implement `CatalogueRunId` value object in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueRunId.cs` per `data-model.md`
- [X] T006 [P] Implement `CatalogueExceptionId` and `CatalogueProposedFixId` value objects in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueExceptionId.cs` and `CatalogueProposedFixId.cs`
- [X] T007 [P] Implement `CatalogueExceptionType` enum in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueExceptionType.cs` per `data-model.md`
- [X] T008 [P] Implement `CatalogueProposedActionType` enum in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueProposedActionType.cs` per `data-model.md`
- [X] T009 [P] Implement `StripeCatalogueProduct` record in `src/BillDrift.Domain/CatalogueReconciliation/StripeCatalogueProduct.cs` per `data-model.md`
- [X] T010 [P] Implement `StripeCataloguePrice` record in `src/BillDrift.Domain/CatalogueReconciliation/StripeCataloguePrice.cs` per `data-model.md`
- [X] T011 [P] Implement `CatalogueInputReferences` record in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueInputReferences.cs`
- [X] T012 Implement `CatalogueReconciliationInputs` record in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueReconciliationInputs.cs` per `data-model.md`
- [X] T013 [P] Implement `CatalogueReconciliationOptions` record in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueReconciliationOptions.cs` per `data-model.md`
- [X] T014 [P] Implement `CatalogueException` record in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueException.cs` per `data-model.md`
- [X] T015 [P] Implement `CatalogueProposedFix` record in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueProposedFix.cs` per `data-model.md`
- [X] T016 [P] Implement `CatalogueReconciliationSummary` record in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueReconciliationSummary.cs`
- [X] T017 Implement `CatalogueReconciliationRun` record in `src/BillDrift.Domain/CatalogueReconciliation/CatalogueReconciliationRun.cs` per `data-model.md`
- [X] T018 Implement `ICatalogueReconciliationEngine` interface in `src/BillDrift.Application/CatalogueReconciliation/ICatalogueReconciliationEngine.cs` per `contracts/catalogue-reconciliation-pipeline.md`
- [X] T019 [P] Implement `IStripeCatalogueNormalizer` interface in `src/BillDrift.Application/CatalogueReconciliation/IStripeCatalogueNormalizer.cs`
- [X] T020 [P] Implement `ICatalogueReconciliationStore` interface in `src/BillDrift.Application/CatalogueReconciliation/ICatalogueReconciliationStore.cs` per `contracts/azure-blob-catalogue-run-archive.md`
- [X] T021 [P] Implement `ICatalogueReconciliationService` interface in `src/BillDrift.Application/CatalogueReconciliation/ICatalogueReconciliationService.cs` per `contracts/catalogue-reconciliation-api-endpoints.md`
- [X] T022 Implement `StripeCatalogueNormalizer` in `src/BillDrift.Application/CatalogueReconciliation/StripeCatalogueNormalizer.cs` mapping `RawStripeProduct`/`RawStripePrice` to catalogue snapshots with offer/SKU metadata parsing
- [X] T023 Implement `StripeCatalogueSnapshotIndex` in `src/BillDrift.Application/CatalogueReconciliation/StripeCatalogueSnapshotIndex.cs` with product/price lookup by ID, commercial key root, and interval per research R2
- [X] T024 Implement `CatalogueReconciliationContext` in `src/BillDrift.Application/CatalogueReconciliation/CatalogueReconciliationContext.cs` holding indexes, options, exceptions, and proposed fixes collections
- [X] T025 [P] Implement pipeline stage interfaces and `CatalogueReconciliationPipeline` skeleton in `src/BillDrift.Application/CatalogueReconciliation/CatalogueReconciliationPipeline.cs` per `contracts/catalogue-reconciliation-pipeline.md`
- [X] T026 [P] Implement `ValidateInputsStage` in `src/BillDrift.Application/CatalogueReconciliation/Stages/ValidateInputsStage.cs`
- [X] T027 [P] Implement `BuildIndexesStage` reusing `ProductMappingIndex` and `IntendedPriceIndex` in `src/BillDrift.Application/CatalogueReconciliation/Stages/BuildIndexesStage.cs`
- [X] T028 [P] Implement `OrderOutputStage` deterministic ordering in `src/BillDrift.Application/CatalogueReconciliation/Stages/OrderOutputStage.cs`
- [X] T029 [P] Implement `CatalogueReconciliationStorageOptions` in `src/BillDrift.Infrastructure/CatalogueReconciliation/CatalogueReconciliationStorageOptions.cs` (container/table names)
- [X] T030 [P] Implement `InMemoryCatalogueReconciliationStore` in `src/BillDrift.Infrastructure/CatalogueReconciliation/InMemoryCatalogueReconciliationStore.cs` mirroring blob path layout for tests

**Checkpoint**: Domain types compile; normalizer and index available; pipeline skeleton and in-memory store ready.

---

## Phase 3: User Story 1 — Reconcile Stripe Catalogue Against Intended Retail Pricing (Priority: P1) 🎯 MVP

**Goal**: For each mapped product with intended pricing, verify Stripe product exists, required prices exist for term/frequency combos, and unit amounts match intended RRP.

**Independent Test**: Given `catalogue-clean-match.json`, zero exceptions; given `catalogue-missing-product.json`, `catalogue-missing-price.json`, `catalogue-incorrect-price.json` — each emits the correct `CatalogueExceptionType` per `quickstart.md` scenarios 2–4.

### Tests for User Story 1

- [X] T031 [P] [US1] Create engine test skeleton in `tests/BillDrift.Application.Tests/CatalogueReconciliation/CatalogueReconciliationEngineTests.cs`
- [X] T032 [P] [US1] Add clean-match assertion test in `CatalogueReconciliationEngineTests.cs` per `quickstart.md` scenario 1
- [X] T033 [P] [US1] Add missing-product, missing-price, and incorrect-price tests in `CatalogueReconciliationEngineTests.cs` per `quickstart.md` scenarios 2–4
- [X] T034 [P] [US1] Create JSON fixtures `catalogue-clean-match.json`, `catalogue-missing-product.json`, `catalogue-missing-price.json`, `catalogue-incorrect-price.json` under `tests/fixtures/catalogue-reconciliation/`

### Implementation for User Story 1

- [X] T035 [P] [US1] Implement `CatalogueExceptionFactory` for `MissingProduct`, `MissingPrice`, `IncorrectPrice` rules CAT-001–CAT-003 in `src/BillDrift.Application/CatalogueReconciliation/Detection/CatalogueExceptionFactory.cs` per `contracts/catalogue-check-rules.md`
- [X] T036 [US1] Implement `ReconcileMappedProductsStage` product resolution (by `StripeProductId` or metadata) in `src/BillDrift.Application/CatalogueReconciliation/Stages/ReconcileMappedProductsStage.cs`
- [X] T037 [US1] Add required price slot iteration and RRP amount comparison (exact minor units, currency match) in `ReconcileMappedProductsStage.cs` with mandatory comments on Stripe price immutability semantics
- [X] T038 [US1] Implement `CatalogueReconciliationEngine` orchestrating validate → build indexes → reconcile mapped products → order output in `src/BillDrift.Application/CatalogueReconciliation/CatalogueReconciliationEngine.cs`
- [X] T039 [US1] Wire `ICatalogueReconciliationEngine` registration in `src/BillDrift.Infrastructure/CatalogueReconciliation/CatalogueReconciliationServiceCollectionExtensions.cs`
- [X] T040 [US1] Complete US1 engine tests in `CatalogueReconciliationEngineTests.cs` — all scenario 1–4 assertions pass

**Checkpoint**: MVP engine detects missing products, missing prices, and incorrect RRP without subscriptions or Azure.

---

## Phase 4: User Story 2 — Detect Duplicate and Conflicting Catalogue Entries (Priority: P1)

**Goal**: Flag multiple Stripe products for the same offer/SKU and multiple active prices for the same product interval + currency; proposals limited to manual cleanup.

**Independent Test**: Given `catalogue-duplicate-products.json` and `catalogue-duplicate-prices.json`, emits duplicate exceptions with no auto-merge/delete proposals per `quickstart.md` scenarios 5–6.

### Tests for User Story 2

- [X] T041 [P] [US2] Add duplicate-product test in `CatalogueReconciliationEngineTests.cs` per `quickstart.md` scenario 5
- [X] T042 [P] [US2] Add duplicate-price test in `CatalogueReconciliationEngineTests.cs` per `quickstart.md` scenario 6
- [X] T043 [P] [US2] Create JSON fixtures `catalogue-duplicate-products.json` and `catalogue-duplicate-prices.json` under `tests/fixtures/catalogue-reconciliation/`

### Implementation for User Story 2

- [X] T044 [US2] Implement `DetectDuplicateConflictsStage` in `src/BillDrift.Application/CatalogueReconciliation/Stages/DetectDuplicateConflictsStage.cs` per rules CAT-004–CAT-005
- [X] T045 [US2] Add duplicate-product and duplicate-price factory methods in `CatalogueExceptionFactory.cs`
- [X] T046 [US2] Insert `DetectDuplicateConflictsStage` before `ReconcileMappedProductsStage` in `CatalogueReconciliationPipeline.cs` per pipeline contract
- [X] T047 [US2] Add global guard suppressing `MissingProduct` when duplicate-product conflict exists on same root in `ReconcileMappedProductsStage.cs`
- [X] T048 [US2] Complete US2 tests in `CatalogueReconciliationEngineTests.cs`

**Checkpoint**: Duplicate detection works independently; no destructive proposed actions.

---

## Phase 5: User Story 3 — Propose Catalogue Fixes for Human Approval (Priority: P1)

**Goal**: Attach approval-ready `CatalogueProposedFix` for each actionable exception; duplicate/conflict flags are non-actionable manual cleanup.

**Independent Test**: Missing product → `CreateProduct`; missing price → `CreatePrice`; incorrect price → `CreateReplacementPrice`; duplicates → `FlagManualCleanup` with `IsActionable = false`.

### Tests for User Story 3

- [X] T049 [P] [US3] Add proposed-fix presence assertions per exception type in `CatalogueReconciliationEngineTests.cs`
- [X] T050 [P] [US3] Create `CatalogueApprovalAdapterTests.cs` in `tests/BillDrift.Application.Tests/CatalogueReconciliation/CatalogueApprovalAdapterTests.cs` per `contracts/approval-integration.md`

### Implementation for User Story 3

- [X] T051 [US3] Implement `CatalogueProposedFixFactory` in `src/BillDrift.Application/CatalogueReconciliation/Detection/CatalogueProposedFixFactory.cs` for `CreateProduct`, `CreatePrice`, `CreateReplacementPrice`, `FlagManualCleanup` per `contracts/catalogue-check-rules.md`
- [X] T052 [US3] Implement `AttachProposedFixesStage` in `src/BillDrift.Application/CatalogueReconciliation/Stages/AttachProposedFixesStage.cs` linking fixes to exceptions
- [X] T053 [US3] Wire `AttachProposedFixesStage` into `CatalogueReconciliationPipeline.cs` before `OrderOutputStage`
- [X] T054 [US3] Populate `CatalogueReconciliationSummary` counts (`ProposedFixesActionable`, `ProposedFixesManualOnly`) in `CatalogueReconciliationEngine.cs`
- [X] T055 [US3] Implement `CatalogueApprovalAdapter` mapping to `ApprovalProposal` in `src/BillDrift.Application/CatalogueReconciliation/CatalogueApprovalAdapter.cs` per `contracts/approval-integration.md`
- [X] T056 [US3] Complete approval adapter tests in `CatalogueApprovalAdapterTests.cs` (eligible vs `CatalogueConflict` eligibility)

**Checkpoint**: Engine emits proposed fixes; adapter maps to approval workflow types.

---

## Phase 6: User Story 4 — Scope Reconciliation to Canonical Mapped Products (Priority: P2)

**Goal**: Drive checks from canonical mapping + intended pricing; surface pricing-reference gaps, mapping ambiguity, and unmapped Stripe catalogue entries.

**Independent Test**: Given `catalogue-pricing-gap.json`, `catalogue-unmapped-stripe.json`, and `catalogue-manual-override-rrp.json` — correct gap, unmapped, and override RRP behaviour per `quickstart.md` scenarios 7–9.

### Tests for User Story 4

- [X] T057 [P] [US4] Add pricing-reference-gap test in `CatalogueReconciliationEngineTests.cs` per `quickstart.md` scenario 7
- [X] T058 [P] [US4] Add unmapped Stripe entry test in `CatalogueReconciliationEngineTests.cs` per `quickstart.md` scenario 8
- [X] T059 [P] [US4] Add manual-override RRP precedence test in `CatalogueReconciliationEngineTests.cs` per `quickstart.md` scenario 9
- [X] T060 [P] [US4] Create JSON fixtures `catalogue-pricing-gap.json`, `catalogue-unmapped-stripe.json`, `catalogue-manual-override-rrp.json` under `tests/fixtures/catalogue-reconciliation/`

### Implementation for User Story 4

- [X] T061 [US4] Implement `DetectUnmappedCatalogueStage` in `src/BillDrift.Application/CatalogueReconciliation/Stages/DetectUnmappedCatalogueStage.cs` per rule CAT-008
- [X] T062 [US4] Add `PricingReferenceGap` and `MappingAmbiguous` handling in `ReconcileMappedProductsStage.cs` per rules CAT-006–CAT-007
- [X] T063 [US4] Respect `IncludeNonCspProducts` option and mapping confidence guards in `ReconcileMappedProductsStage.cs`
- [X] T064 [US4] Wire `DetectUnmappedCatalogueStage` into `CatalogueReconciliationPipeline.cs`
- [X] T065 [US4] Extend `CatalogueReconciliationSummary` with `UnmappedStripeProducts` / `UnmappedStripePrices` rollups in `CatalogueReconciliationEngine.cs`
- [X] T066 [US4] Complete US4 tests in `CatalogueReconciliationEngineTests.cs`

**Checkpoint**: Scoped iteration and orphan catalogue surfacing work without API layer.

---

## Phase 7: User Story 5 — Run Catalogue Reconciliation from Export Snapshots (Priority: P2)

**Goal**: Orchestrate runs from ingested Stripe + pricing archives, persist to Azure Blob/Table via Aspire DI, expose API trigger and list/detail endpoints.

**Independent Test**: `POST /api/catalogue-reconciliation/runs` with ingestion run IDs returns summary; run persisted and listable; identical inputs produce identical output (determinism).

### Tests for User Story 5

- [X] T067 [P] [US5] Create `DeterminismTests.cs` in `tests/BillDrift.Application.Tests/CatalogueReconciliation/DeterminismTests.cs` per `quickstart.md` scenario 10
- [X] T068 [P] [US5] Create `AzureCatalogueReconciliationStoreTests.cs` in `tests/BillDrift.Infrastructure.Tests/CatalogueReconciliation/AzureCatalogueReconciliationStoreTests.cs` for Azurite blob/table round-trip
- [X] T069 [P] [US5] Create JSON fixture `catalogue-determinism.json` under `tests/fixtures/catalogue-reconciliation/`

### Implementation for User Story 5

- [X] T070 [US5] Implement `CatalogueReconciliationJsonSerializerContext` in `src/BillDrift.Infrastructure/CatalogueReconciliation/CatalogueReconciliationJsonSerializerContext.cs` for blob payloads
- [X] T071 [US5] Implement `AzureCatalogueReconciliationStore` with constructor-injected `BlobServiceClient` and `TableServiceClient` in `src/BillDrift.Infrastructure/CatalogueReconciliation/CatalogueReconciliationStores.cs` per `contracts/azure-blob-catalogue-run-archive.md` and `contracts/azure-table-catalogue-run-schema.md` — **no manual connection strings**
- [X] T072 [US5] Implement `CatalogueReconciliationService` assembling pricing inputs from `IIngestionBlobStore.GetResolvedPricesAsync` in `src/BillDrift.Application/CatalogueReconciliation/CatalogueReconciliationService.cs`
- [X] T073 [US5] Add run persistence (blob archive + table index) to `CatalogueReconciliationService.cs` after engine execution
- [X] T074 [US5] Implement `CatalogueReconciliationEndpoints.cs` (`POST /runs`, `GET /runs`, `GET /runs/{id}`, `POST /runs/{id}/ingest-approvals`) in `src/BillDrift.Api/CatalogueReconciliation/CatalogueReconciliationEndpoints.cs` per `contracts/catalogue-reconciliation-api-endpoints.md`
- [X] T075 [US5] Register `AddCatalogueReconciliation()` and `AddCatalogueReconciliationStorage()` in `src/BillDrift.Infrastructure/CatalogueReconciliation/CatalogueReconciliationServiceCollectionExtensions.cs` and wire `MapCatalogueReconciliationEndpoints()` in `src/BillDrift.Api/Program.cs`
- [X] T076 [US5] Complete Azurite integration tests in `AzureCatalogueReconciliationStoreTests.cs`
- [X] T077 [US5] Complete determinism tests in `DeterminismTests.cs`
- [X] T078 [US5] Implement optional `ingestToApprovalQueue` path calling `CatalogueApprovalAdapter` + `IApprovalStore` in `CatalogueReconciliationService.cs`

### Remaining US5 gap (Stripe ingestion blob loading)

- [X] T085 [US5] Add `GetStripeCatalogueProductsAsync` and `GetStripeCataloguePricesAsync` to `src/BillDrift.Application/Ingestion/IIngestionBlobStore.cs` per research R7 and `contracts/catalogue-reconciliation-api-endpoints.md`
- [X] T086 [P] [US5] Implement Stripe catalogue blob load methods in `src/BillDrift.Infrastructure/Ingestion/AzureBlobIngestionArchiveStore.cs` and `src/BillDrift.Infrastructure/Ingestion/InMemoryIngestionBlobStore.cs` using existing 003 archive layout
- [X] T087 [US5] Wire `CatalogueReconciliationService.RunAsync` to load `stripeProducts`/`stripePrices` from `stripeIngestionRunId` when inline arrays are empty in `src/BillDrift.Application/CatalogueReconciliation/CatalogueReconciliationService.cs`
- [X] T088 [US5] Add service test for Stripe ingestion run ID resolution in `tests/BillDrift.Application.Tests/CatalogueReconciliation/CatalogueReconciliationServiceTests.cs` per `quickstart.md` scenario 13

**Checkpoint**: End-to-end API trigger with both Stripe and pricing ingestion run IDs; Azure persistence; approval ingestion functional.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Golden comparer, index unit tests, quickstart validation, build quality, storage policy audit.

- [X] T079 [P] Implement `GoldenRunComparer` for catalogue runs in `tests/BillDrift.Application.Tests/CatalogueReconciliation/GoldenRunComparer.cs`
- [X] T080 [P] Add `StripeCatalogueSnapshotIndexTests.cs` in `tests/BillDrift.Application.Tests/CatalogueReconciliation/StripeCatalogueSnapshotIndexTests.cs`
- [X] T081 [P] Add `StripeCatalogueNormalizerTests.cs` in `tests/BillDrift.Application.Tests/CatalogueReconciliation/StripeCatalogueNormalizerTests.cs`
- [X] T089 [P] Audit `src/BillDrift.Infrastructure/CatalogueReconciliation/` and `src/BillDrift.Infrastructure/Ingestion/` for manual `BlobServiceClient`/`TableServiceClient` construction — confirm Aspire DI only per plan storage policy
- [X] T090 Run full `specs/012-stripe-catalogue-reconciliation/quickstart.md` validation checklist and update Notes section if gaps remain
- [X] T091 Run `dotnet clean`, `dotnet restore`, `dotnet build --no-restore`, and `dotnet test --no-build` from solution root per constitution build quality gate

**Checkpoint**: Clean build, all tests pass, quickstart scenarios verified, storage policy confirmed.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **US1 (Phase 3)**: Depends on Foundational — **MVP** ✅
- **US2 (Phase 4)**: Depends on US1 pipeline skeleton ✅
- **US3 (Phase 5)**: Depends on US1 exceptions ✅
- **US4 (Phase 6)**: Depends on US1 reconcile stage ✅
- **US5 (Phase 7)**: ✅ Complete
- **Polish (Phase 8)**: ✅ Complete

### User Story Dependencies

| Story | Depends on | Independent test | Status |
|-------|------------|------------------|--------|
| US1 (P1) | Foundational | Engine JSON fixtures, no Azure | ✅ Complete |
| US2 (P1) | US1 pipeline | Duplicate fixtures only | ✅ Complete |
| US3 (P1) | US1 exceptions | Fix + adapter unit tests | ✅ Complete |
| US4 (P2) | US1 reconcile stage | Gap/unmapped/override fixtures | ✅ Complete |
| US5 (P2) | US1–US3 engine | API + Azurite; Stripe blob load | ✅ Complete |

### Parallel Opportunities

- **Remaining**: T086 parallel with T085 after interface defined; T089 parallel with T085–T088
- **Completed phases**: T002–T004, T006–T011, T013–T016, T019–T021, T025–T030 ran in parallel batches

### Parallel Example: Remaining US5 Work

```bash
# After T085 interface defined:
T086 AzureBlobIngestionArchiveStore.cs + InMemoryIngestionBlobStore.cs  # parallel
T087 CatalogueReconciliationService.cs                                   # after T086
T088 CatalogueReconciliationServiceTests.cs                              # after T087
```

---

## Implementation Strategy

### MVP First (User Story 1 Only) — ✅ DELIVERED

Phases 1–3 complete. Engine validates missing product, missing price, and incorrect RRP via unit tests.

### Incremental Delivery

1. Setup + Foundational → ✅
2. US1 → ✅ MVP
3. US2 → ✅ duplicate hygiene
4. US3 → ✅ approval-ready fixes
5. US4 → ✅ scoped iteration + unmapped surfacing
6. US5 → ✅ persistence + API + Stripe ingestion blob loading
7. Polish → ✅ CI green

### Suggested Next Actions

Feature implementation complete. Optional follow-on: Stripe CSV ingestion persist path writing `stripe-catalogue-products.json` / `stripe-catalogue-prices.json` blobs (003 feature extension).

---

## Notes

- Reuse `ProductMappingIndex`, `IntendedPriceIndex`, and `IntendedPriceResolver` from 004 — do not duplicate pricing precedence logic
- `StripeCatalogueSnapshotIndex` replaces subscription-derived `StripeCatalogueIndex` for this feature only
- Incorrect Stripe prices always propose **create replacement** — document in code comments (constitution I)
- No `BlobServiceClient`/`TableServiceClient` in `BillDrift.Web` or `BillDrift.Application` — Infrastructure only
- Product mappings accepted inline on API v1; dedicated mapping persistence is future work
- v1 API currently accepts inline `stripeProducts`/`stripePrices`; `stripeIngestionRunId` blob resolution is the remaining US5 deliverable
