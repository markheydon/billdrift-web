# Tasks: Reconciliation Item Classification

**Input**: Design documents from `/specs/006-reconciliation-classification/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md; features 001–005 complete (domain model, ingestion, reconciliation engine, exception surfacing)

**UI note**: Backend-only — no Blazor classification UI in scope (see plan.md). Delivers `ClassificationService`, persistence, and minimal API for future Fluent UI consumers.

**Tests**: Included per constitution Principle II (billing-critical), `quickstart.md` validation scenarios, and contract rule matrices in `contracts/classification-rules.md`.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US5) for story-phase tasks only

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Aspire Azure Storage wiring, package references, folder structure, and fixture layout per plan.md.

- [X] T001 Add `Azure.Data.Tables` and `Azure.Storage.Blobs` package versions to `Directory.Packages.props` and reference from `src/BillDrift.Infrastructure/BillDrift.Infrastructure.csproj`
- [X] T002 Add Aspire Azure Storage hosting packages to `src/BillDrift.AppHost/BillDrift.AppHost.csproj` per research R6
- [X] T003 Wire `AddAzureStorage("storage").RunAsEmulator()`, `AddTables("tables")`, and `AddBlobs("blobs")` with API references in `src/BillDrift.AppHost/AppHost.cs`
- [X] T004 Register `AddAzureTableServiceClient("tables")` and `AddAzureBlobServiceClient("blobs")` in `src/BillDrift.Api/Program.cs` (no manual connection string construction)
- [X] T005 [P] Create `src/BillDrift.Domain/Classification/`, `src/BillDrift.Application/Classification/`, and `src/BillDrift.Infrastructure/Classification/` folder structure per plan.md
- [X] T006 [P] Create `tests/BillDrift.Application.Tests/Classification/` and `tests/BillDrift.Infrastructure.Tests/Classification/` test folders
- [X] T007 [P] Create `tests/fixtures/classification/` directory and `tests/fixtures/classification/README.md` mapping fixtures to `quickstart.md` scenarios

**Checkpoint**: Solution builds; Aspire AppHost declares storage resources; folder layout matches plan.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain types, store abstraction, stable item keys, rule engine skeleton, and in-memory store. MUST complete before user story implementation.

**⚠️ CRITICAL**: No user story work until this phase is complete.

- [X] T008 [P] Implement `ReconciliationItemClassification`, `ClassificationSource`, and `ClassificationConfidence` enums in `src/BillDrift.Domain/Classification/ReconciliationItemClassification.cs`
- [X] T009 [P] Implement `ReconciliationItemKind`, `ProductCategory`, and `ProductCategoryMatchKind` enums in `src/BillDrift.Domain/Classification/ClassificationEnums.cs`
- [X] T010 [P] Implement `ReconciliationItemRef` with validation in `src/BillDrift.Domain/Classification/ReconciliationItemRef.cs` per `data-model.md`
- [X] T011 [P] Implement `ItemClassification`, `ClassificationOverride`, and `ClassificationHistoryEntry` records in `src/BillDrift.Domain/Classification/ItemClassification.cs`
- [X] T012 [P] Implement `ProductCategoryRule` and `ClassificationRuleConfiguration` in `src/BillDrift.Domain/Classification/ClassificationRuleConfiguration.cs`
- [X] T013 [P] Add domain construction/validation tests in `tests/BillDrift.Domain.Tests/Classification/ClassificationTypesTests.cs`
- [X] T014 Implement `IItemClassificationStore` in `src/BillDrift.Application/Classification/IItemClassificationStore.cs` per `data-model.md`
- [X] T015 Implement `InMemoryItemClassificationStore` in `tests/BillDrift.Application.Tests/Classification/InMemoryItemClassificationStore.cs` for rule and service tests
- [X] T016 Implement `ReconciliationItemRefFactory` with stable key algorithms in `src/BillDrift.Application/Classification/ReconciliationItemRefFactory.cs` per research R2 and `contracts/classification-pipeline.md`
- [X] T017 [P] Add stable key derivation tests in `tests/BillDrift.Application.Tests/Classification/ReconciliationItemRefFactoryTests.cs`
- [X] T018 Implement `ClassificationContext` record in `src/BillDrift.Application/Classification/ClassificationContext.cs`
- [X] T019 Implement `ClassificationRuleEngine` skeleton (rule chain stubs CR-0 through CR-FALLBACK) in `src/BillDrift.Application/Classification/ClassificationRuleEngine.cs` with precedence comments per `contracts/classification-rules.md`
- [X] T020 Implement `ClassificationService` skeleton with `ClassifyAsync` orchestration stub in `src/BillDrift.Application/Classification/ClassificationService.cs`
- [X] T021 [P] Implement `ClassificationStorageOptions` in `src/BillDrift.Infrastructure/Classification/ClassificationStorageOptions.cs`
- [X] T022 [P] Add `ClassificationStorageExtensions` stub registering in-memory store for tests in `src/BillDrift.Infrastructure/Classification/ClassificationStorageExtensions.cs`

**Checkpoint**: Domain compiles; in-memory store usable; `ClassifyAsync` returns empty context; stable key tests pass.

---

## Phase 3: User Story 1 — Automatically Classify Reconciliation Items by Origin (Priority: P1) 🎯 MVP

**Goal**: Every reconciliation item receives one of four origin classifications with rule basis and confidence via deterministic automatic rules.

**Independent Test**: Given fixtures covering all four types, `ClassificationService.ClassifyAsync` assigns exactly one classification per item with recorded rule basis and confidence; repeated runs are identical.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T023 [P] [US1] Add `ClassificationRuleEngineTests` skeleton in `tests/BillDrift.Application.Tests/Classification/ClassificationRuleEngineTests.cs` (one case per classification type)
- [X] T024 [P] [US1] Add `ClassificationServiceTests` skeleton in `tests/BillDrift.Application.Tests/Classification/ClassificationServiceTests.cs`

### Implementation for User Story 1

- [X] T025 [US1] Implement signal extraction helpers (`HasOfferSku`, `InSubscriptionTruth`, `InIntendedPriceList`, etc.) in `src/BillDrift.Application/Classification/ClassificationSignalBuilder.cs` per `contracts/classification-pipeline.md` Stage 3
- [X] T026 [US1] Implement product category resolution (PCR-1) in `ClassificationRuleEngine` in `src/BillDrift.Application/Classification/ClassificationRuleEngine.cs`
- [X] T027 [US1] Implement CR-1 Internal customer rule in `ClassificationRuleEngine.cs`
- [X] T028 [US1] Implement CR-2 Custom/service rule in `ClassificationRuleEngine.cs`
- [X] T029 [US1] Implement CR-3 Non-CSP supplier rule in `ClassificationRuleEngine.cs`
- [X] T030 [US1] Implement CR-4 Microsoft CSP rule with confidence tiers in `ClassificationRuleEngine.cs`
- [X] T031 [US1] Implement CR-5 conservative default fallback in `ClassificationRuleEngine.cs`
- [X] T032 [US1] Implement `ClassifyAsync` item enumeration and rule invocation in `src/BillDrift.Application/Classification/ClassificationService.cs`
- [X] T033 [P] [US1] Create `tests/fixtures/classification/classify-csp-full-signals.json` per `quickstart.md` V1
- [X] T034 [P] [US1] Create `tests/fixtures/classification/non-csp-supplier-only.json` per `quickstart.md` V3
- [X] T035 [P] [US1] Create `tests/fixtures/classification/classify-custom-stripe-only.json` per `quickstart.md` V8
- [X] T036 [P] [US1] Create `tests/fixtures/classification/classify-conservative-partial-sku.json` per `quickstart.md` V7
- [X] T037 [US1] Add Microsoft CSP high-confidence test in `ClassificationRuleEngineTests.cs` per SC-007
- [X] T038 [US1] Add Non-CSP supplier and Custom/service rule tests in `ClassificationRuleEngineTests.cs`
- [X] T039 [US1] Add internal customer rule test (config-driven) in `ClassificationRuleEngineTests.cs`
- [X] T040 [US1] Add conservative partial-SKU default test in `ClassificationRuleEngineTests.cs` per SC-008
- [X] T041 [US1] Add determinism test (classify twice, compare snapshots) in `ClassificationServiceTests.cs` per SC-004

**Checkpoint**: All four classification types assignable automatically; rule engine unit-tested; determinism verified.

---

## Phase 4: User Story 2 — Apply Manual Classification Overrides with Audit Notes (Priority: P1)

**Goal**: Operators can override automatic classification, persist notes, and retain audit history; overrides take precedence on subsequent runs.

**Independent Test**: Override a Non-CSP item to Microsoft CSP with notes; next `ClassifyAsync` returns manual source; clear override reverts to automatic rules.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T042 [P] [US2] Add `ClassificationOverrideTests` skeleton in `tests/BillDrift.Application.Tests/Classification/ClassificationOverrideTests.cs`
- [X] T043 [P] [US2] Add `AzureTableItemClassificationStoreTests` skeleton in `tests/BillDrift.Infrastructure.Tests/Classification/AzureTableItemClassificationStoreTests.cs`

### Implementation for User Story 2

- [X] T044 [US2] Implement `ClassificationTableEntities` mapping in `src/BillDrift.Infrastructure/Classification/ClassificationTableEntities.cs` per `contracts/azure-table-schema.md`
- [X] T045 [US2] Implement `AzureTableItemClassificationStore` using DI-injected `TableServiceClient` in `src/BillDrift.Infrastructure/Classification/AzureTableItemClassificationStore.cs`
- [X] T046 [US2] Complete `ClassificationStorageExtensions` registering Azure store and options in `src/BillDrift.Infrastructure/Classification/ClassificationStorageExtensions.cs`
- [X] T047 [US2] Implement `ApplyOverrideAsync` with notes validation (OV-1) and history append in `src/BillDrift.Application/Classification/ClassificationService.cs`
- [X] T048 [US2] Implement `ClearOverrideAsync` with history append in `ClassificationService.cs`
- [X] T049 [US2] Integrate CR-0 manual override precedence in `ClassificationRuleEngine.cs` loading from store
- [X] T050 [US2] Implement `GetHistoryAsync` usage for audit retrieval in `ClassificationService.cs`
- [X] T051 [US2] Implement minimal API endpoints (`GET/PUT/DELETE /api/classifications/{stableKey}`) in `src/BillDrift.Api/Classification/ClassificationEndpoints.cs`
- [X] T052 [US2] Add override precedence test in `ClassificationOverrideTests.cs` per `quickstart.md` V4
- [X] T053 [US2] Add override clear re-evaluation test in `ClassificationOverrideTests.cs` per `quickstart.md` V5
- [X] T054 [US2] Add notes-required validation test for Internal override in `ClassificationOverrideTests.cs` per OV-1
- [X] T055 [US2] Add Azure table round-trip override test in `AzureTableItemClassificationStoreTests.cs` (Azurite or test container)

**Checkpoint**: Overrides persist to Azure Tables; history auditable; API callable; manual override wins over automatic rules.

---

## Phase 5: User Story 3 — Suppress False Missing-Billing Alerts for Internal Items (Priority: P1)

**Goal**: Internal-classified items do not generate missing-billing mismatches or surfaced exceptions when no Stripe counterpart exists.

**Independent Test**: Internal customer fixture with truth lines and no Stripe match produces zero `MissingInStripe` and zero `MissingBillingItem` exceptions.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T056 [P] [US3] Add `ClassificationIntegrationTests` skeleton in `tests/BillDrift.Application.Tests/Classification/ClassificationIntegrationTests.cs`
- [X] T057 [P] [US3] Create `tests/fixtures/classification/internal-customer-no-missing-billing.json` per `quickstart.md` V2

### Implementation for User Story 3

- [X] T058 [US3] Add `ClassificationContext? Classifications` to `ReconciliationRequest` in `src/BillDrift.Application/Reconciliation/IReconciliationEngine.cs` (or request record file)
- [X] T059 [US3] Add `ClassificationContext?` to `ReconciliationContext` in `src/BillDrift.Application/Reconciliation/ReconciliationContext.cs`
- [X] T060 [US3] Implement `ClassificationEnrichmentStage` in `src/BillDrift.Application/Classification/Stages/ClassificationEnrichmentStage.cs` per `contracts/reconciliation-integration.md`
- [X] T061 [US3] Register enrichment stage in reconciliation pipeline in `src/BillDrift.Application/Reconciliation/ReconciliationEngine.cs`
- [X] T062 [US3] Add RI-1a guard skipping `MissingInStripe` for `Internal` items in `src/BillDrift.Application/Reconciliation/Detection/MismatchDetector.cs`
- [X] T063 [US3] Add `SuppressionRule.ClassificationInternal` and `ClassificationCustomService` to `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/ReconciliationExceptionViewModel.cs`
- [X] T064 [US3] Implement SR-6 classification suppression in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/Phases/SuppressPhase.cs` per `contracts/classification-rules.md`
- [X] T065 [US3] Pass `ClassificationContext` into `SurfacingContext` from `ExceptionSurfacingService` in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/ExceptionSurfacingService.cs`
- [X] T066 [US3] Add internal customer missing-billing suppression integration test in `ClassificationIntegrationTests.cs` per SC-002
- [X] T067 [US3] Add reclassification override removes missing-billing test in `ClassificationIntegrationTests.cs` per spec US3 acceptance scenario 3

**Checkpoint**: Internal items produce no missing-billing engine mismatches or surfaced exceptions; quantity/price checks still run when Stripe matched.

---

## Phase 6: User Story 4 — Route Non-CSP Items to Manual Mapping and Pricing Workflows (Priority: P1)

**Goal**: Non-CSP-classified supplier lines surface manual review, block CSP auto-match, and emit no bill-impacting proposals without operator mapping.

**Independent Test**: Non-CSP fixture surfaces `NonCspManualReview`, no high-confidence CSP fuzzy match, zero bill-impacting `ProposedChange`.

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T068 [P] [US4] Add non-CSP integration test cases to `ClassificationIntegrationTests.cs`

### Implementation for User Story 4

- [X] T069 [US4] Add `IsNonCspForReconciliation` helper using `ClassificationContext` in `src/BillDrift.Application/Reconciliation/Stages/MatchGroupBuildStage.cs` per `contracts/reconciliation-integration.md`
- [X] T070 [US4] Update `SupplierCostReconcileStage` to use item classification in `src/BillDrift.Application/Reconciliation/Stages/SupplierCostReconcileStage.cs`
- [X] T071 [US4] Block bill-impacting proposals for `NonCspSupplier` classification in `src/BillDrift.Application/Reconciliation/Detection/ProposedChangeFactory.cs`
- [X] T072 [US4] Prevent fuzzy CSP auto-match for `NonCspSupplier` items in `src/BillDrift.Application/Reconciliation/Stages/MatchGroupBuildStage.cs` per spec US4 acceptance scenario 2
- [X] T073 [US4] Add non-CSP manual review surfacing test in `ClassificationIntegrationTests.cs` per SC-003
- [X] T074 [US4] Add ambiguous CSP name similarity does not auto-match test in `ClassificationIntegrationTests.cs`
- [X] T075 [US4] Add Custom/service missing-billing suppression test in `ClassificationIntegrationTests.cs` per FR-015

**Checkpoint**: Non-CSP and Custom/service items follow manual/conservative reconciliation paths; no false CSP auto-match.

---

## Phase 7: User Story 5 — Configure Classification Rules for Operators (Priority: P2)

**Goal**: Administrators can configure internal Mex IDs and product category rules without code changes; config changes affect next classification run.

**Independent Test**: Add internal Mex ID via config API; items for that customer classify as Internal on next run without per-item overrides.

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T076 [P] [US5] Add `ClassificationConfigTests` skeleton in `tests/BillDrift.Application.Tests/Classification/ClassificationConfigTests.cs`

### Implementation for User Story 5

- [X] T077 [US5] Implement config entity read/write in `AzureTableItemClassificationStore` for internal Mex IDs and category rules in `src/BillDrift.Infrastructure/Classification/AzureTableItemClassificationStore.cs`
- [X] T078 [US5] Implement `GetConfigurationAsync` and `UpdateConfigurationAsync` in `src/BillDrift.Application/Classification/ClassificationService.cs`
- [X] T079 [US5] Implement config API endpoints (`GET/PUT /api/classification-config/internal-mex-ids`, `GET/PUT /api/classification-config/product-category-rules`) in `src/BillDrift.Api/Classification/ClassificationEndpoints.cs`
- [X] T080 [US5] Wire config load into `ClassifyAsync` in `ClassificationService.cs`
- [X] T081 [US5] Add internal Mex ID config affects classification test in `ClassificationConfigTests.cs` per spec US5 acceptance scenario 1
- [X] T082 [US5] Add product category Custom/service rule test in `ClassificationConfigTests.cs` per spec US5 acceptance scenario 2
- [X] T083 [US5] Add config change does not alter manual overrides test in `ClassificationConfigTests.cs` per spec US5 acceptance scenario 4

**Checkpoint**: Configuration persisted in Azure Tables; API updates config; classification reacts on next run.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: DI registration, backward compatibility, optional evidence enrichment, and full quickstart validation.

- [X] T084 Register `ClassificationService` in `src/BillDrift.Application/Classification/ClassificationServiceCollectionExtensions.cs` and call from Application DI setup
- [X] T085 Call `AddClassificationStorage()` from `src/BillDrift.Api/Program.cs` after Aspire client registration
- [X] T086 [P] Verify `ClassificationContext == null` preserves existing 004 reconciliation test behaviour in `tests/BillDrift.Application.Tests/Reconciliation/ReconciliationEngineTests.cs`
- [X] T087 [P] Add optional classification evidence fields to `SurfacedException` in `src/BillDrift.Application/Reconciliation/ExceptionSurfacing/ReconciliationExceptionViewModel.cs` per FR-019 (if not deferred)
- [X] T088 Run full `quickstart.md` validation scenarios V1–V8 and document results in `specs/006-reconciliation-classification/quickstart.md` checklist section
- [X] T089 [P] Principle VI simplicity review — confirm no extra interfaces beyond `IItemClassificationStore` and no SQL dependencies introduced

**Checkpoint**: Full test suite passes; quickstart scenarios verified; backward compatibility intact.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **User Story 1 (Phase 3)**: Depends on Foundational — **MVP**
- **User Story 2 (Phase 4)**: Depends on US1 rule engine (override integrates CR-0)
- **User Story 3 (Phase 5)**: Depends on US1 classifications; can parallel with US2 after US1
- **User Story 4 (Phase 6)**: Depends on US1; best after US3 engine hooks started
- **User Story 5 (Phase 7)**: Depends on US2 Azure store; can overlap with US3/US4
- **Polish (Phase 8)**: Depends on all desired user stories

### User Story Dependencies

| Story | Depends on | Can start after |
|-------|------------|-----------------|
| US1 | Foundational | Phase 2 complete |
| US2 | US1 rule engine | Phase 3 complete |
| US3 | US1 | Phase 3 complete |
| US4 | US1 | Phase 3 complete |
| US5 | US2 store | Phase 4 complete |

### Parallel Opportunities

- **Phase 1**: T005, T006, T007 in parallel after T001–T004
- **Phase 2**: T008–T012 (domain types), T017, T021, T022 in parallel
- **Phase 3**: T023–T024 (test skeletons), T033–T036 (fixtures) in parallel
- **Phase 4**: T042–T043 in parallel
- **Phase 5–6**: Integration test skeletons parallel with engine edits where files differ
- **Phase 8**: T086, T087, T089 in parallel

---

## Parallel Example: User Story 1

```bash
# Test skeletons together:
T023 ClassificationRuleEngineTests.cs
T024 ClassificationServiceTests.cs

# Fixtures together:
T033 classify-csp-full-signals.json
T034 non-csp-supplier-only.json
T035 classify-custom-stripe-only.json
T036 classify-conservative-partial-sku.json
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: `dotnet test --filter "FullyQualifiedName~Classification"` — all US1 tests pass
5. Demo automatic classification on fixtures

### Incremental Delivery

1. Setup + Foundational → storage and domain ready
2. US1 → automatic classification (MVP)
3. US2 → persistence and overrides
4. US3 + US4 → reconciliation impact (false positive reduction)
5. US5 → operator configurability
6. Polish → quickstart sign-off

### Suggested MVP Scope

**User Story 1 only** (Phases 1–3): delivers deterministic four-type classification with in-memory store — sufficient to prove rule engine before Azure persistence.

---

## Notes

- Use Aspire-injected `TableServiceClient` / `BlobServiceClient` only — never parse connection strings in application code
- No SQL database; Azure Tables primary, Blobs optional for config snapshots
- No Fluent UI in this feature — API endpoints ready for future Blazor work
- `[P]` tasks = different files, no ordering dependency within the same phase checkpoint
- Commit after each phase checkpoint
- Total tasks: **89** (T001–T089)
