# Tasks: Reconciliation Run History & Audit

**Input**: Design documents from `/specs/008-reconciliation-run-history/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md; features 001–007 complete (domain model, ingestion, reconciliation engine, exceptions, classification, approval)

**Tests**: Included per constitution Principle II (billing-critical), `quickstart.md` validation scenarios, and archive integrity requirements.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US6) for story-phase tasks only

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Folder structure, fixture layout, and verify existing Aspire storage wiring. No SQL; reuse existing `TableServiceClient` / `BlobServiceClient` registration from feature 007.

- [X] T001 [P] Create `src/BillDrift.Domain/History/` folder structure per `plan.md`
- [X] T002 [P] Create `src/BillDrift.Application/History/` folder structure per `plan.md`
- [X] T003 [P] Create `src/BillDrift.Infrastructure/History/` folder structure per `plan.md`
- [X] T004 [P] Create `src/BillDrift.Api/History/` folder for REST endpoints
- [X] T005 [P] Create `tests/BillDrift.Application.Tests/History/` test folder
- [X] T006 [P] Create `tests/BillDrift.Infrastructure.Tests/History/` test folder
- [X] T007 [P] Create `tests/BillDrift.Api.Tests/History/` test folder
- [X] T008 [P] Create `tests/fixtures/run-history/` directory and `tests/fixtures/run-history/README.md` mapping fixtures to `quickstart.md` scenarios
- [X] T009 Verify existing `AddAzureTableServiceClient("tables")` and `AddAzureBlobServiceClient("blobs")` in `src/BillDrift.Api/Program.cs` (no new manual connection string wiring)

**Checkpoint**: Solution builds; folder layout matches plan; storage clients already registered.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain types, store abstraction, JSON serializer context, and service skeletons. MUST complete before user story implementation.

**⚠️ CRITICAL**: No user story work until this phase is complete.

- [X] T010 [P] Implement `RunArchiveStatus`, `InputDomainType`, `PricingDriftEventType`, and `ExecutionOutcomeStatus` enums in `src/BillDrift.Domain/History/RunHistoryEnums.cs` per `data-model.md`
- [X] T011 [P] Implement `StableMismatchKey` readonly record struct in `src/BillDrift.Domain/History/StableMismatchKey.cs`
- [X] T012 [P] Implement `InputSnapshotMetadata` and `MappingVersionReference` records in `src/BillDrift.Domain/History/InputSnapshotMetadata.cs`
- [X] T013 [P] Implement `RunSummaryMetrics` and `ReconciliationRunRecord` records in `src/BillDrift.Domain/History/ReconciliationRunRecord.cs`
- [X] T014 [P] Implement `RunResultsSnapshot` record in `src/BillDrift.Domain/History/RunResultsSnapshot.cs`
- [X] T015 [P] Implement `RunComparisonReport`, `InputChangeSummary`, `ExceptionDeltaReport`, `ComparedMismatch`, and `PersistingMismatch` records in `src/BillDrift.Domain/History/RunComparisonReport.cs`
- [X] T016 [P] Implement `DriftTrendEntry` record in `src/BillDrift.Domain/History/DriftTrendEntry.cs`
- [X] T017 [P] Implement `PricingDriftTimelineEntry` record in `src/BillDrift.Domain/History/PricingDriftTimelineEntry.cs`
- [X] T018 [P] Implement `ProposalStatusLink` and `ExecutionOutcome` records in `src/BillDrift.Domain/History/ExecutionOutcome.cs`
- [X] T019 [P] Add domain construction and validation tests in `tests/BillDrift.Domain.Tests/History/RunHistoryTypesTests.cs`
- [X] T020 Implement `IRunHistoryStore` interface in `src/BillDrift.Application/History/IRunHistoryStore.cs` per `data-model.md`
- [X] T021 Implement `InMemoryRunHistoryStore` in `tests/BillDrift.Application.Tests/History/InMemoryRunHistoryStore.cs` for unit and service tests
- [X] T022 [P] Implement `RunHistoryStorageOptions` in `src/BillDrift.Infrastructure/History/RunHistoryStorageOptions.cs`
- [X] T023 [P] Implement `RunHistoryJsonSerializerContext` source-gen context in `src/BillDrift.Infrastructure/History/RunHistoryJsonSerializerContext.cs`
- [X] T024 [P] Implement `RunArchiveContext`, `PersistRunRequest`, and `RunHistoryListFilter` in `src/BillDrift.Application/History/RunHistoryViewModels.cs`
- [X] T025 [P] Implement `RunHistoryServiceCollectionExtensions` stub in `src/BillDrift.Application/History/RunHistoryServiceCollectionExtensions.cs`
- [X] T026 [P] Implement `RunHistoryStorageExtensions` stub in `src/BillDrift.Infrastructure/History/RunHistoryStorageExtensions.cs`

**Checkpoint**: Domain compiles; in-memory store usable; service/storage extension stubs registered; no SQL introduced.

---

## Phase 3: User Story 1 — Persist Complete Reconciliation Run Record (Priority: P1) 🎯 MVP

**Goal**: Every completed reconciliation run is stored as an immutable record with normalized input snapshots, mapping version, results, and drift index rows.

**Independent Test**: Given a completed `ReconciliationRun`, persist produces table index rows and blob snapshots; retrieving by `RunId` reproduces summary metrics and manifest hashes without re-running reconciliation.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T027 [P] [US1] Add `RunArchiveServiceTests` skeleton in `tests/BillDrift.Application.Tests/History/RunArchiveServiceTests.cs`
- [X] T028 [P] [US1] Add `AzureBlobRunArchiveStoreTests` skeleton in `tests/BillDrift.Infrastructure.Tests/History/AzureBlobRunArchiveStoreTests.cs`
- [X] T029 [P] [US1] Add `AzureTableRunHistoryStoreTests` skeleton in `tests/BillDrift.Infrastructure.Tests/History/AzureTableRunHistoryStoreTests.cs`
- [X] T030 [P] [US1] Create `tests/fixtures/run-history/jan-2026-run.json` per `quickstart.md` V1

### Implementation for User Story 1

- [X] T031 [US1] Implement `RunHistoryTableEntities` mapping in `src/BillDrift.Infrastructure/History/RunHistoryTableEntities.cs` per `contracts/azure-table-schema.md`
- [X] T032 [US1] Implement `AzureBlobRunArchiveStore` using DI-injected `BlobServiceClient` in `src/BillDrift.Infrastructure/History/AzureBlobRunArchiveStore.cs` per `contracts/azure-blob-run-archive.md` (no manual connection strings)
- [X] T033 [US1] Implement blob write protocol (inputs, results, manifest last) with SHA-256 content hashes in `AzureBlobRunArchiveStore.cs`
- [X] T034 [US1] Implement `AzureTableRunHistoryStore` using DI-injected `TableServiceClient` in `src/BillDrift.Infrastructure/History/AzureTableRunHistoryStore.cs`
- [X] T035 [US1] Implement run index, input metadata, and drift index row upserts in `AzureTableRunHistoryStore.cs`
- [X] T036 [US1] Implement `StableMismatchKeyFactory` in `src/BillDrift.Application/History/StableMismatchKeyFactory.cs` per `contracts/mismatch-comparison-rules.md`
- [X] T037 [US1] Implement `RunArchiveService.PersistAsync` orchestration in `src/BillDrift.Application/History/RunArchiveService.cs` per `contracts/run-history-pipeline.md`
- [X] T038 [US1] Implement failed-run persist with partial state and `FailureReason` in `RunArchiveService.cs`
- [X] T039 [US1] Implement idempotent re-persist rejection for `Completed` runs (409) in `RunArchiveService.cs`
- [X] T040 [US1] Implement append-only run audit events (`RunArchiveStarted`, `RunArchived`, `RunArchiveFailed`) in `AzureTableRunHistoryStore.cs`
- [X] T041 [US1] Complete `RunHistoryStorageExtensions` registering Azure stores in `src/BillDrift.Infrastructure/History/RunHistoryStorageExtensions.cs`
- [X] T042 [US1] Implement `POST /api/run-history` persist endpoint in `src/BillDrift.Api/History/RunHistoryEndpoints.cs` per `contracts/run-history-api-endpoints.md`
- [X] T043 [US1] Wire persist call from reconciliation orchestration in `src/BillDrift.Api/Program.cs` or reconciliation endpoint (post-engine hook)
- [X] T044 [US1] Add persist creates immutable run record test in `RunArchiveServiceTests.cs` per `quickstart.md` V1
- [X] T045 [US1] Add all input domains marked present or absent test in `RunArchiveServiceTests.cs` per `quickstart.md` V2
- [X] T046 [US1] Add re-persist completed run rejected test in `RunArchiveServiceTests.cs` per `quickstart.md` V3
- [X] T047 [US1] Add blob round-trip and manifest hash test in `AzureBlobRunArchiveStoreTests.cs` (Azurite)
- [X] T048 [US1] Add table run index round-trip test in `AzureTableRunHistoryStoreTests.cs` (Azurite)

**Checkpoint**: Persist via API; blobs and table rows created; manifest validates; completed runs immutable.

---

## Phase 4: User Story 2 — Browse and Inspect Run History (Priority: P1)

**Goal**: Operators browse past runs, filter by billing period and date, and open run detail with input metadata, summary metrics, and exception counts.

**Independent Test**: Given six stored runs across three billing periods, list/filter works; run detail shows input snapshots, mapping version, and summary without re-running reconciliation.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T049 [P] [US2] Add `RunHistoryServiceTests` skeleton in `tests/BillDrift.Application.Tests/History/RunHistoryServiceTests.cs`
- [X] T050 [P] [US2] Create `tests/fixtures/run-history/feb-2026-run.json` for list/filter scenarios per `quickstart.md` V4

### Implementation for User Story 2

- [X] T051 [US2] Implement `RunHistoryService.ListRunsAsync` with billing period and date filters in `src/BillDrift.Application/History/RunHistoryService.cs`
- [X] T052 [US2] Implement `RunHistoryService.GetRunSummaryAsync` (table-only read) in `RunHistoryService.cs`
- [X] T053 [US2] Implement `RunHistoryService.GetRunDetailAsync` with selective blob reads in `RunHistoryService.cs`
- [X] T054 [US2] Implement `GET /api/run-history` list endpoint with pagination in `src/BillDrift.Api/History/RunHistoryEndpoints.cs`
- [X] T055 [US2] Implement `GET /api/run-history/{runId}` detail endpoint with `includeResults` query in `RunHistoryEndpoints.cs`
- [X] T056 [US2] Implement `GET /api/run-history/{runId}/inputs/{domain}` endpoint in `RunHistoryEndpoints.cs`
- [X] T057 [US2] Implement `RunHistoryApiClient` in `src/BillDrift.Web/Services/RunHistoryApiClient.cs`
- [X] T058 [US2] Add Run History nav item to `src/BillDrift.Web/Components/Layout/MainLayout.razor` per `contracts/fluent-ui-integration.md`
- [X] T059 [US2] Implement `RunHistoryListPage.razor` in `src/BillDrift.Web/Pages/History/RunHistoryListPage.razor` with `FluentDataGrid` and filters
- [X] T060 [US2] Implement `RunDetailPage.razor` in `src/BillDrift.Web/Pages/History/RunDetailPage.razor` with Summary and Inputs tabs
- [X] T061 [US2] Add routes `/history` and `/history/{RunId:guid}` in `src/BillDrift.Web/Components/Routes.razor`
- [X] T062 [US2] Add list runs by billing period test in `RunHistoryServiceTests.cs` per `quickstart.md` V4
- [X] T063 [US2] Add run detail summary without full blob load test in `RunHistoryServiceTests.cs`
- [X] T064 [US2] Add API list/detail integration test in `tests/BillDrift.Api.Tests/History/RunHistoryEndpointsTests.cs`

**Checkpoint**: Run history list and detail pages functional; filters work; clean runs visible with zero-issue summary.

---

## Phase 5: User Story 3 — Compare Runs Month-to-Month (Priority: P2)

**Goal**: Operators compare two stored runs and receive structured delta reports for exceptions (new, resolved, persisting) and input changes.

**Independent Test**: Given Jan and Feb stored runs, comparison classifies exceptions correctly and flags mapping version changes.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T065 [P] [US3] Add `RunComparisonServiceTests` skeleton in `tests/BillDrift.Application.Tests/History/RunComparisonServiceTests.cs`
- [X] T066 [P] [US3] Add `StableMismatchKeyFactoryTests` skeleton in `tests/BillDrift.Application.Tests/History/StableMismatchKeyFactoryTests.cs`

### Implementation for User Story 3

- [X] T067 [US3] Implement `RunComparisonService.Compare` algorithm in `src/BillDrift.Application/History/RunComparisonService.cs` per `contracts/mismatch-comparison-rules.md`
- [X] T068 [US3] Implement input delta summaries (record counts and fingerprint change) in `RunComparisonService.cs`
- [X] T069 [US3] Implement mapping version change detection in `RunComparisonService.cs`
- [X] T070 [US3] Implement `RunHistoryService.CompareRunsAsync` loading two result snapshots in `RunHistoryService.cs`
- [X] T071 [US3] Implement `POST /api/run-history/compare` endpoint in `src/BillDrift.Api/History/RunHistoryEndpoints.cs`
- [X] T072 [US3] Implement `RunComparisonPage.razor` in `src/BillDrift.Web/Pages/History/RunComparisonPage.razor` with New/Resolved/Persisting sections
- [X] T073 [US3] Add route `/history/compare` in `src/BillDrift.Web/Components/Routes.razor`
- [X] T074 [US3] Add month-to-month comparison classifies exceptions test in `RunComparisonServiceTests.cs` per `quickstart.md` V6 and SC-003
- [X] T075 [US3] Add mapping version change flagged test in `RunComparisonServiceTests.cs` per `quickstart.md` V7
- [X] T076 [US3] Add stable key determinism test in `StableMismatchKeyFactoryTests.cs`

**Checkpoint**: Two-run comparison via API and UI; exception deltas classified; mapping version warning displayed.

---

## Phase 6: User Story 4 — Identify Recurring Drift Trends (Priority: P2)

**Goal**: Surface mismatches repeating across multiple runs with occurrence count, first/last seen dates, and issue category grouping.

**Independent Test**: Given four runs with same quantity mismatch, drift trend view ranks it as recurring with correct first/last seen.

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T077 [P] [US4] Add `DriftTrendAnalyzerTests` skeleton in `tests/BillDrift.Application.Tests/History/DriftTrendAnalyzerTests.cs`
- [X] T078 [P] [US4] Create `tests/fixtures/run-history/recurring-quantity-drift/` multi-run fixture per `quickstart.md` V8

### Implementation for User Story 4

- [X] T079 [US4] Implement `DriftTrendAnalyzer.Analyze` aggregating drift index rows in `src/BillDrift.Application/History/DriftTrendAnalyzer.cs`
- [X] T080 [US4] Implement recurring vs transient classification (`minOccurrences` threshold) in `DriftTrendAnalyzer.cs`
- [X] T081 [US4] Implement `RunHistoryService.GetDriftTrendsAsync` in `RunHistoryService.cs`
- [X] T082 [US4] Implement `GET /api/run-history/trends/drift` endpoint in `src/BillDrift.Api/History/RunHistoryEndpoints.cs`
- [X] T083 [US4] Add Drift Trends sub-tab to `DriftTrendsPage.razor` in `src/BillDrift.Web/Pages/History/DriftTrendsPage.razor`
- [X] T084 [US4] Add route `/history/trends` in `src/BillDrift.Web/Components/Routes.razor`
- [X] T085 [US4] Add recurring drift 3+ occurrences test in `DriftTrendAnalyzerTests.cs` per `quickstart.md` V8 and SC-004
- [X] T086 [US4] Add transient single-occurrence excluded test in `DriftTrendAnalyzerTests.cs`

**Checkpoint**: Drift trends page shows recurring mismatches; filters by date window and mismatch type work.

---

## Phase 7: User Story 5 — Track Pricing Drift (RRP vs Stripe Catalogue) (Priority: P2)

**Goal**: Timeline showing intended retail pricing, manual overrides, and Stripe catalogue price evolution across runs with lag persistence.

**Independent Test**: Given runs where RRP changed and Stripe lagged two runs, timeline shows lag events with `lagRunsPersisted >= 2`.

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T087 [P] [US5] Add `PricingDriftAnalyzerTests` skeleton in `tests/BillDrift.Application.Tests/History/PricingDriftAnalyzerTests.cs`
- [X] T088 [P] [US5] Create `tests/fixtures/run-history/pricing-lag-timeline/` multi-run fixture per `quickstart.md` V9

### Implementation for User Story 5

- [X] T089 [US5] Implement `PricingDriftAnalyzer.Analyze` in `src/BillDrift.Application/History/PricingDriftAnalyzer.cs` per `contracts/pricing-drift-timeline.md`
- [X] T090 [US5] Implement RRP change, override add/remove, Stripe price change, and catalogue missing event detection in `PricingDriftAnalyzer.cs`
- [X] T091 [US5] Implement lag persistence calculation in `PricingDriftAnalyzer.cs`
- [X] T092 [US5] Implement `RunHistoryService.GetPricingDriftTimelineAsync` loading pricing/stripe blobs only in `RunHistoryService.cs`
- [X] T093 [US5] Implement `GET /api/run-history/trends/pricing` endpoint in `src/BillDrift.Api/History/RunHistoryEndpoints.cs`
- [X] T094 [US5] Add Pricing Drift sub-tab with commercial key selector to `DriftTrendsPage.razor`
- [X] T095 [US5] Add pricing drift timeline lag test in `PricingDriftAnalyzerTests.cs` per `quickstart.md` V9 and SC-005
- [X] T096 [US5] Add override add/remove event test in `PricingDriftAnalyzerTests.cs`

**Checkpoint**: Pricing drift timeline renders; RRP vs Stripe lag visible; catalogue-missing distinct from amount mismatch.

---

## Phase 8: User Story 6 — Link Approval and Execution Outcomes (Priority: P3)

**Goal**: Run detail reflects approval decisions from feature 007 and reserves execution outcome fields for future write-back.

**Independent Test**: Run with mixed approval decisions shows proposal status links; execution outcome fields empty but present; superseded proposals linked to superseding run.

### Tests for User Story 6

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T097 [P] [US6] Add approval join tests to `RunHistoryServiceTests.cs`

### Implementation for User Story 6

- [X] T098 [US6] Implement approval status join via `IApprovalStore.ListProposalsByRunAsync` in `RunHistoryService.GetRunDetailAsync` (no duplication into blobs)
- [X] T099 [US6] Map `ProposalStatusLink` view model with decision state, actor, timestamp, rejection reason in `RunHistoryService.cs`
- [X] T100 [US6] Add Proposals tab with approval status badges to `RunDetailPage.razor` per `contracts/fluent-ui-integration.md`
- [X] T101 [US6] Add link to `/approvals/{runId}` from Proposals tab in `RunDetailPage.razor`
- [X] T102 [US6] Include empty `ExecutionOutcome` list placeholder in run detail API response in `RunHistoryEndpoints.cs`
- [X] T103 [US6] Add Exceptions tab showing mismatches from blob in `RunDetailPage.razor`
- [X] T104 [US6] Add Audit tab querying `GET /api/run-history/{runId}/audit` in `RunDetailPage.razor`
- [X] T105 [US6] Implement `GET /api/run-history/{runId}/audit` endpoint in `RunHistoryEndpoints.cs`
- [X] T106 [US6] Add run detail includes approval status links test in `RunHistoryServiceTests.cs` per `quickstart.md` V5 and SC-006
- [X] T107 [US6] Add execution outcomes empty placeholder test in `RunHistoryServiceTests.cs`

**Checkpoint**: Run detail shows live approval state; audit tab works; execution fields reserved; blob proposals unchanged.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: DI registration, storage boundary verification, export support, retention options, full quickstart validation, and constitution compliance.

- [X] T108 Register `AddRunHistory()` and call `AddRunHistoryStorage()` from `src/BillDrift.Api/Program.cs` after Aspire client registration
- [X] T109 [P] Verify `BillDrift.Web` has **no** `TableServiceClient` or `BlobServiceClient` registration (API-only storage access)
- [X] T110 [P] Grep run history codebase for SQL/EF Core references — confirm none introduced
- [X] T111 Implement `POST /api/run-history/compare/export` optional comparison export to blob in `src/BillDrift.Api/History/RunHistoryEndpoints.cs`
- [X] T112 Implement retention options (`DefaultRetentionMonths`, `ArchivedAt`) enforcement stub in `RunHistoryService.cs` per research R10
- [X] T113 Add blob integrity mismatch error handling in `AzureBlobRunArchiveStore.cs` per `quickstart.md` V11
- [X] T114 Add failed run retained test in `RunArchiveServiceTests.cs` per `quickstart.md` V10
- [X] T115 Add audit events on persist and compare test in `AzureTableRunHistoryStoreTests.cs` per `quickstart.md` V12
- [X] T116 Run full `quickstart.md` validation scenarios V1–V12; document pass/fail in `specs/008-reconciliation-run-history/quickstart.md` checklist section
- [X] T117 Run `dotnet clean`, `dotnet restore`, `dotnet build --no-restore`, and `dotnet test --no-build` on full solution with zero errors/warnings per workspace rules
- [X] T118 [P] Principle VI simplicity review — confirm `IRunHistoryStore` is sole new persistence abstraction; no SQL introduced

**Checkpoint**: Full test suite passes; quickstart scenarios verified; storage and safety constraints confirmed.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **User Story 1 (Phase 3)**: Depends on Foundational — **MVP (persist runs)**
- **User Story 2 (Phase 4)**: Depends on US1 persist — list/detail reads stored runs
- **User Story 3 (Phase 5)**: Depends on US1 persist — needs two stored runs
- **User Story 4 (Phase 6)**: Depends on US1 drift index — can parallel with US3 after US1
- **User Story 5 (Phase 7)**: Depends on US1 input blobs — can parallel with US3/US4 after US1
- **User Story 6 (Phase 8)**: Depends on US2 detail page — approval join needs detail view
- **Polish (Phase 9)**: Depends on all desired user stories

### User Story Dependencies

| Story | Depends on | Can start after |
|-------|------------|-----------------|
| US1 | Foundational | Phase 2 complete |
| US2 | US1 persist | Phase 3 complete |
| US3 | US1 persist (2+ runs) | Phase 3 complete |
| US4 | US1 drift index | Phase 3 complete |
| US5 | US1 input blobs | Phase 3 complete |
| US6 | US2 detail page | Phase 4 complete |

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Domain/store before services
- Services before API endpoints
- API before Web UI consumption
- Story checkpoint before next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (T001–T008)
- Foundational domain types T010–T018 can run in parallel
- After US1 complete: US3, US4, US5 can proceed in parallel (different analyzers/files)
- US2 and US6 are sequential (detail page before approval tab completion)

---

## Parallel Example: User Story 1

```bash
# Launch store tests together:
Task: "AzureBlobRunArchiveStoreTests skeleton in tests/BillDrift.Infrastructure.Tests/History/"
Task: "AzureTableRunHistoryStoreTests skeleton in tests/BillDrift.Infrastructure.Tests/History/"

# Launch domain records together (Phase 2):
Task: "ReconciliationRunRecord in src/BillDrift.Domain/History/ReconciliationRunRecord.cs"
Task: "RunResultsSnapshot in src/BillDrift.Domain/History/RunResultsSnapshot.cs"
```

---

## Parallel Example: After User Story 1

```bash
# US3, US4, US5 in parallel (different developers):
Developer A: RunComparisonService + RunComparisonPage (US3)
Developer B: DriftTrendAnalyzer + DriftTrendsPage recurring tab (US4)
Developer C: PricingDriftAnalyzer + DriftTrendsPage pricing tab (US5)
```

---

## Implementation Strategy

### MVP First (User Story 1 + User Story 2)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1 (persist runs)
4. Complete Phase 4: User Story 2 (browse/inspect)
5. **STOP and VALIDATE**: Persist a run; list and view detail in UI
6. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US1 → Persist runs → Validate V1–V3
3. US2 → Browse history → Validate V4
4. US3 → Month-to-month compare → Validate V6–V7
5. US4 → Drift trends → Validate V8
6. US5 → Pricing drift → Validate V9
7. US6 → Approval links → Validate V5
8. Polish → Full quickstart V1–V12

### Suggested MVP Scope

**Minimum viable**: Phase 1 + Phase 2 + Phase 3 (US1) + Phase 4 (US2) — operators can persist and inspect run history.

**High value add**: Phase 5 (US3) — month-to-month comparison is the primary audit workflow after inspection.

---

## Notes

- Azure Table + Blob only — **no SQL** per user guardrail and plan
- Use Aspire DI-injected `TableServiceClient` and `BlobServiceClient` only in Infrastructure
- Approval decision state joined from `IApprovalStore` at read time — never copied into blob snapshots
- `StableMismatchKey` is distinct from run-scoped `MismatchId` — required for cross-run analysis
- Manual upload input metadata (Phase 1 build queue) populated from ingestion pipeline when persisting
- Write-back `ExecutionOutcome` fields modeled but left empty until future apply feature
