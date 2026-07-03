# Tasks: V1 MVP Operator UI

**Input**: Design documents from `/specs/013-v1-mvp-ui/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: API contract tests included per plan.md and constitution II (changed HTTP boundaries). No bUnit UI test tasks.

**Organization**: Tasks grouped by user story. Phase 2 (Foundational) delivers API enablement that blocks UI stories per research R10.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US9)

## Path Conventions

- **API**: `src/BillDrift.Api/`
- **Application enablement glue**: `src/BillDrift.Application/`
- **Infrastructure**: `src/BillDrift.Infrastructure/`
- **Web UI**: `src/BillDrift.Web/`
- **Tests**: `tests/BillDrift.Api.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify project structure and shared scaffolding

- [X] T001 Verify `tests/BillDrift.Api.Tests/BillDrift.Api.Tests.csproj` references `BillDrift.Api` and has WebApplicationFactory test infrastructure
- [X] T002 [P] Create shared UI folder `src/BillDrift.Web/Components/Shared/` and add `_Imports.razor` usings if needed
- [X] T003 [P] Add ingestion run DTO records (`GiacomPdfIngestionRun`, `StripeCsvIngestionRun`, summaries) in `src/BillDrift.Application/Import/` per data-model.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: API enablement — thin adapters over frozen Application layer. **CRITICAL**: No user story UI work until this phase completes.

**⚠️ Application-layer freeze**: New Application code MUST be orchestration-only (ingest → normalize → persist → call existing service). No domain rule changes.

- [X] T004 Extend `IIngestionBlobStore` with supplier-cost persist/load methods in `src/BillDrift.Application/Ingestion/IIngestionBlobStore.cs`
- [X] T005 Implement supplier-cost blob methods in `src/BillDrift.Infrastructure/Ingestion/AzureBlobIngestionArchiveStore.cs` and `InMemoryIngestionBlobStore.cs`
- [X] T006 [P] Create `IGiacomPdfIngestionService` and `GiacomPdfIngestionService` in `src/BillDrift.Application/Import/Giacom/` mirroring `SubscriptionManagementIngestionService`
- [X] T007 [P] Create `IStripeCsvIngestionService` and `StripeCsvIngestionService` in `src/BillDrift.Application/Import/Stripe/` including `PersistStripeCatalogueAsync` call
- [X] T008 Create `ReconciliationOrchestrationService` in `src/BillDrift.Application/Reconciliation/ReconciliationOrchestrationService.cs` per contracts/reconciliation-orchestration-api-endpoints.md
- [X] T009 [P] Create `GiacomPdfImportEndpoints` in `src/BillDrift.Api/Imports/GiacomPdfImportEndpoints.cs` per contracts/giacom-pdf-import-api-endpoints.md
- [X] T010 [P] Create `StripeCsvImportEndpoints` in `src/BillDrift.Api/Imports/StripeCsvImportEndpoints.cs` per contracts/stripe-csv-import-api-endpoints.md
- [X] T011 Create `ReconciliationEndpoints` in `src/BillDrift.Api/Reconciliation/ReconciliationEndpoints.cs` per contracts/reconciliation-orchestration-api-endpoints.md
- [X] T012 Add `POST .../approvals/ingest-from-run` to `src/BillDrift.Api/Approval/ApprovalEndpoints.cs` per contracts/approval-ingest-convenience.md
- [X] T013 Register new ingestion services, orchestration service, and map all new endpoints in `src/BillDrift.Api/Program.cs`
- [X] T014 [P] Add contract tests for Giacom PDF import in `tests/BillDrift.Api.Tests/Imports/GiacomPdfImportEndpointsTests.cs`
- [X] T015 [P] Add contract tests for Stripe CSV import in `tests/BillDrift.Api.Tests/Imports/StripeCsvImportEndpointsTests.cs`
- [X] T016 [P] Add contract tests for reconciliation orchestration in `tests/BillDrift.Api.Tests/Reconciliation/ReconciliationEndpointsTests.cs`
- [X] T017 [P] Add contract test for approval ingest-from-run in `tests/BillDrift.Api.Tests/Approval/ApprovalIngestFromRunTests.cs`

**Checkpoint**: All new API endpoints pass contract tests; quickstart Phase A scenarios (A1–A6) succeed via HTTP

---

## Phase 3: User Story 1 — Upload and Review Data Sources (Priority: P1) 🎯 MVP

**Goal**: Operator uploads all four source types and reviews import history with clear success/error feedback

**Independent Test**: Upload valid/invalid PDF and CSV files via `/ingestion`; verify summaries, errors, and per-source import history without running reconciliation (spec US1)

### Implementation for User Story 1

- [X] T018 [P] [US1] Create `IIngestionApiClient` interface in `src/BillDrift.Web/Services/IIngestionApiClient.cs`
- [X] T019 [US1] Implement `IngestionApiClient` covering subscription, retail, PDF, and Stripe endpoints in `src/BillDrift.Web/Services/IngestionApiClient.cs`
- [X] T020 [P] [US1] Create `ImportResultBanner.razor` in `src/BillDrift.Web/Components/Shared/ImportResultBanner.razor`
- [X] T021 [P] [US1] Create `IngestionRunPicker.razor` in `src/BillDrift.Web/Components/Shared/IngestionRunPicker.razor`
- [X] T022 [US1] Create `IngestionHubPage.razor` with Fluent tabs shell in `src/BillDrift.Web/Pages/Ingestion/IngestionHubPage.razor`
- [X] T023 [P] [US1] Implement Subscription Management CSV upload tab in `src/BillDrift.Web/Pages/Ingestion/IngestionHubPage.razor`
- [X] T024 [P] [US1] Implement Retail Pricing CSV upload tab in `src/BillDrift.Web/Pages/Ingestion/IngestionHubPage.razor`
- [X] T025 [P] [US1] Implement Giacom PDF upload tab in `src/BillDrift.Web/Pages/Ingestion/IngestionHubPage.razor`
- [X] T026 [P] [US1] Implement Stripe CSV multipart upload tab in `src/BillDrift.Web/Pages/Ingestion/IngestionHubPage.razor`
- [X] T027 [US1] Add import history grids per source type with status/timestamp/record counts in `src/BillDrift.Web/Pages/Ingestion/IngestionHubPage.razor`
- [X] T028 [US1] Register `IngestionApiClient` in `src/BillDrift.Web/Program.cs` and add `/ingestion` nav item in `src/BillDrift.Web/Components/Layout/MainLayout.razor`

**Checkpoint**: quickstart B1–B2 pass; SC-001 satisfied

---

## Phase 4: User Story 2 — Run Reconciliation and Review Exceptions (Priority: P1)

**Goal**: Operator starts reconciliation from ingested snapshots and reviews filterable exceptions with detail

**Independent Test**: Trigger reconciliation via UI; filter exceptions by category; view expected vs actual and business reason (spec US2)

### Implementation for User Story 2

- [X] T029 [P] [US2] Create `IReconciliationApiClient` in `src/BillDrift.Web/Services/IReconciliationApiClient.cs`
- [X] T030 [US2] Implement `ReconciliationApiClient` in `src/BillDrift.Web/Services/ReconciliationApiClient.cs`
- [X] T031 [P] [US2] Create `ExceptionCategoryBadge.razor` in `src/BillDrift.Web/Components/Shared/ExceptionCategoryBadge.razor`
- [X] T032 [US2] Create `ReconciliationPage.razor` with input selection form in `src/BillDrift.Web/Pages/Reconciliation/ReconciliationPage.razor`
- [ ] T033 [US2] Add billing period picker and inline product mapping editor (session state) to `src/BillDrift.Web/Pages/Reconciliation/ReconciliationPage.razor`
- [X] T034 [US2] Implement start-run action with progress and summary badges in `src/BillDrift.Web/Pages/Reconciliation/ReconciliationPage.razor`
- [X] T035 [US2] Create `ExceptionDashboard.razor` with category filter in `src/BillDrift.Web/Pages/Reconciliation/ExceptionDashboard.razor`
- [X] T036 [US2] Add exception detail panel (expected vs actual, rule reference) to `src/BillDrift.Web/Pages/Reconciliation/ExceptionDashboard.razor`
- [X] T037 [US2] Add clean-run indication and link to run history in `src/BillDrift.Web/Pages/Reconciliation/ReconciliationPage.razor`
- [X] T038 [US2] Register `ReconciliationApiClient` and `/reconciliation` route in `src/BillDrift.Web/Program.cs` and `MainLayout.razor` (replace dead link)

**Checkpoint**: quickstart C1–C3 pass; SC-004, SC-005 satisfied

---

## Phase 5: User Story 7 — View Margin Anomalies (Priority: P2)

**Goal**: Operator reviews margin (RRP − cost, %) with visual distinction for negative/low margins

**Independent Test**: Open margin view for a reconciled run; negative/low margins identifiable at a glance (spec US7)

### Implementation for User Story 7

- [X] T039 [P] [US7] Create `MarginSeverityBadge.razor` in `src/BillDrift.Web/Components/Shared/MarginSeverityBadge.razor`
- [X] T040 [US7] Create `MarginView.razor` grid in `src/BillDrift.Web/Pages/Reconciliation/MarginView.razor`
- [X] T041 [US7] Integrate margin tab into `src/BillDrift.Web/Pages/Reconciliation/ReconciliationPage.razor` using reconciliation API margin endpoint

**Checkpoint**: quickstart C4 pass; SC-009 satisfied

---

## Phase 6: User Story 4 — Review and Approve Proposed Changes (Priority: P1)

**Goal**: Operator reviews, approves/rejects proposals individually and in bulk; triggers ingest from reconciliation results

**Independent Test**: Load approval queue; approve/reject with reason; bulk approve with preview; ingest from run (spec US4)

### Implementation for User Story 4

- [X] T042 [P] [US4] Extend `IApprovalApiClient` with bulk approve and ingest-from-run methods in `src/BillDrift.Web/Services/IApprovalApiClient.cs`
- [X] T043 [US4] Implement bulk approve preview/confirm and ingest-from-run in `src/BillDrift.Web/Services/ApprovalApiClient.cs`
- [X] T044 [US4] Create `ApprovalRunPickerPage.razor` at `/approvals` in `src/BillDrift.Web/Pages/Approvals/ApprovalRunPickerPage.razor`
- [X] T045 [US4] Wire `BulkApproveDialog.razor` into `src/BillDrift.Web/Pages/Approvals/ApprovalQueuePage.razor`
- [X] T046 [US4] Add "Send to approval queue" action on `src/BillDrift.Web/Pages/Reconciliation/ReconciliationPage.razor` calling ingest-from-run
- [X] T047 [US4] Add ingest button on run detail proposals tab in `src/BillDrift.Web/Pages/History/RunDetailPage.razor` when queue empty

**Checkpoint**: quickstart D1–D2 pass; SC-006 satisfied

---

## Phase 7: User Story 5 — Export Approved Changeset (Priority: P2)

**Goal**: Operator exports approved proposals as downloadable file for manual Stripe application

**Independent Test**: Approve subset of proposals; export; download contains approved items only (spec US5)

### Implementation for User Story 5

- [X] T048 [US5] Verify and polish `ExportChangesetPanel.razor` empty state and error handling in `src/BillDrift.Web/Components/Approval/ExportChangesetPanel.razor`
- [ ] T049 [US5] Ensure export panel visible from both `ApprovalQueuePage.razor` and post-approval summary in `ReconciliationPage.razor`

**Checkpoint**: quickstart D1 export step pass; SC-007 satisfied

---

## Phase 8: User Story 3 — Manage Product Mapping and Classification (Priority: P2)

**Goal**: Session mapping editor and classification override/config UI (no persistent mapping store)

**Independent Test**: Edit mappings in session; apply classification override; re-run reconciliation reflects changes (spec US3)

### Implementation for User Story 3

- [X] T050 [P] [US3] Create `IClassificationApiClient` in `src/BillDrift.Web/Services/IClassificationApiClient.cs`
- [X] T051 [US3] Implement `ClassificationApiClient` in `src/BillDrift.Web/Services/ClassificationApiClient.cs`
- [X] T052 [US3] Create `MappingPage.razor` with session `ProductMapping[]` grid and JSON import/export in `src/BillDrift.Web/Pages/Mapping/MappingPage.razor`
- [X] T053 [US3] Add deferred-persistence banner on `src/BillDrift.Web/Pages/Mapping/MappingPage.razor` per Application-Layer Capability Notes
- [X] T054 [US3] Create `ClassificationPage.razor` for overrides and config in `src/BillDrift.Web/Pages/Classification/ClassificationPage.razor`
- [X] T055 [US3] Register clients and add Mapping/Classification nav items in `src/BillDrift.Web/Program.cs` and `MainLayout.razor`

**Checkpoint**: quickstart E4–E5 pass

---

## Phase 9: User Story 6 — Run Catalogue Reconciliation (Priority: P2)

**Goal**: Operator runs catalogue reconciliation and ingests fix proposals into approval queue

**Independent Test**: Start catalogue run; review missing/misaligned items; ingest to approval Catalogue tab (spec US6)

### Implementation for User Story 6

- [X] T056 [P] [US6] Create `ICatalogueReconciliationApiClient` in `src/BillDrift.Web/Services/ICatalogueReconciliationApiClient.cs`
- [X] T057 [US6] Implement `CatalogueReconciliationApiClient` in `src/BillDrift.Web/Services/CatalogueReconciliationApiClient.cs`
- [X] T058 [US6] Create `CatalogueReconciliationPage.razor` with run form and results grid in `src/BillDrift.Web/Pages/Catalogue/CatalogueReconciliationPage.razor`
- [X] T059 [US6] Add ingest-fixes-to-approval action on `src/BillDrift.Web/Pages/Catalogue/CatalogueReconciliationPage.razor`
- [X] T060 [US6] Register client and `/catalogue` nav item in `src/BillDrift.Web/Program.cs` and `MainLayout.razor`

**Checkpoint**: quickstart E3 pass

---

## Phase 10: User Story 8 — Browse Run History and Trends (Priority: P3)

**Goal**: Polish run history list/detail, compare with run pickers, trends navigation

**Independent Test**: Filter runs; view all detail tabs; compare via dropdowns; view drift trends (spec US8)

### Implementation for User Story 8

- [ ] T061 [P] [US8] Extend `IRunHistoryApiClient` with persist, input download, compare export in `src/BillDrift.Web/Services/IRunHistoryApiClient.cs`
- [ ] T062 [US8] Implement extended methods in `src/BillDrift.Web/Services/RunHistoryApiClient.cs`
- [ ] T063 [US8] Add billing period, date range, and input-presence badges to `src/BillDrift.Web/Pages/History/RunHistoryListPage.razor`
- [ ] T064 [US8] Polish Summary/Inputs/Exceptions/Proposals/Audit tabs in `src/BillDrift.Web/Pages/History/RunDetailPage.razor`
- [X] T065 [US8] Replace GUID text fields with run dropdowns on `src/BillDrift.Web/Pages/History/RunComparisonPage.razor`
- [X] T066 [US8] Add Compare and Trends links to nav in `src/BillDrift.Web/Components/Layout/MainLayout.razor`

**Checkpoint**: quickstart D3–D4 pass; SC-008 satisfied

---

## Phase 11: User Story 9 — Workflow Home and Navigation (Priority: P3)

**Goal**: Home page orients operators to workflow; all nav links resolve; no dead routes

**Independent Test**: Home shows workflow steps; every nav item loads a working page (spec US9)

### Implementation for User Story 9

- [X] T067 [P] [US9] Create `WorkflowStepIndicator.razor` in `src/BillDrift.Web/Components/Shared/WorkflowStepIndicator.razor`
- [X] T068 [P] [US9] Create `EmptyStatePanel.razor` in `src/BillDrift.Web/Components/Shared/EmptyStatePanel.razor`
- [X] T069 [US9] Replace template `Home.razor` with `WorkflowHomePage.razor` in `src/BillDrift.Web/Pages/Home/WorkflowHomePage.razor`
- [ ] T070 [US9] Add latest import status, pending approval count, and quick links on `WorkflowHomePage.razor`
- [X] T071 [US9] Audit and fix all nav routes in `src/BillDrift.Web/Components/Layout/MainLayout.razor`; remove or redirect template pages (`Counter.razor`)

**Checkpoint**: quickstart E1–E2 pass; SC-003, SC-010 satisfied

---

## Phase 12: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, consistency, cleanup

- [ ] T072 [P] Remove unused Bootstrap layout remnants from `src/BillDrift.Web/Components/Layout/` per 007 fluent-ui contract
- [ ] T073 [P] Align terminology (exception, proposal, approval) across all new pages per FR-039
- [ ] T074 Verify error/empty/loading states on all primary workflows per FR-038 and constitution III
- [ ] T075 Run Application-Layer Freeze Checklist from plan.md against all new Application code
- [ ] T076 Run full quickstart.md validation (Phases A–E) via Aspire AppHost
- [X] T077 Run `dotnet clean`, `dotnet restore`, `dotnet build --no-restore`, `dotnet test --no-build` from solution root

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS all user story phases**
- **Phases 3–11 (User Stories)**: Depend on Phase 2 completion
- **Phase 12 (Polish)**: Depends on desired user stories being complete

### User Story Dependencies

| Story | Priority | Depends on | Notes |
|-------|----------|------------|-------|
| US1 | P1 | Phase 2 | MVP entry point |
| US2 | P1 | Phase 2, US1 (ingestion IDs) | Can test via API before US1 UI |
| US7 | P2 | US2 | Margin tab on reconciliation page |
| US4 | P1 | US2 | Needs reconciliation run + ingest-from-run |
| US5 | P2 | US4 | Export panel mostly exists |
| US3 | P2 | Phase 2 | Session mappings feed US2 form |
| US6 | P2 | Phase 2, US1 (Stripe/pricing IDs) | Catalogue API exists |
| US8 | P3 | US2 (archived runs) | Polish existing pages |
| US9 | P3 | All primary routes exist | Best done last |

### Within Each User Story

- API clients before pages
- Shared components before pages that use them
- Page shell before tab/feature additions

### Parallel Opportunities

- **Phase 2**: T006+T007 (PDF vs Stripe services), T009+T010 (endpoints), T014–T017 (contract tests)
- **Phase 3**: T023–T026 (upload tabs in parallel after T022 shell)
- **Phase 4–11**: Stories US3, US6, US8, US9 can proceed in parallel once US1+US2 foundational path works
- **Phase 12**: T072+T073 in parallel

---

## Parallel Example: User Story 1

```bash
# After T022 shell exists, launch upload tabs together:
T023: Subscription Management tab in IngestionHubPage.razor
T024: Retail Pricing tab in IngestionHubPage.razor
T025: Giacom PDF tab in IngestionHubPage.razor
T026: Stripe CSV tab in IngestionHubPage.razor
```

---

## Parallel Example: Foundational API Enablement

```bash
# Parallel endpoint + service creation:
T006: GiacomPdfIngestionService
T007: StripeCsvIngestionService
T009: GiacomPdfImportEndpoints
T010: StripeCsvImportEndpoints

# Parallel contract tests after T013 registration:
T014: GiacomPdfImportEndpointsTests
T015: StripeCsvImportEndpointsTests
T016: ReconciliationEndpointsTests
T017: ApprovalIngestFromRunTests
```

---

## Implementation Strategy

### MVP First (User Story 1 + Foundational)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational API enablement (**critical**)
3. Complete Phase 3: User Story 1 (Ingestion UI)
4. **STOP and VALIDATE**: quickstart B1–B2, SC-001

### Core Value Path (P1 stories)

5. Phase 4: User Story 2 (Reconciliation + exceptions)
6. Phase 6: User Story 4 (Approval workflow completion)
7. **VALIDATE**: quickstart D1 — full review cycle SC-002

### Incremental Delivery

8. Phase 5 (US7 margin) → Phase 7 (US5 export polish) → Phase 8 (US3 mapping) → Phase 9 (US6 catalogue) → Phase 10 (US8 history) → Phase 11 (US9 home)
9. Phase 12: Polish and full quickstart validation

### Parallel Team Strategy

With multiple developers after Phase 2:

- **Dev A**: US1 ingestion UI
- **Dev B**: US2 reconciliation UI + US7 margin
- **Dev C**: US4 approval extensions
- Then: US3, US6, US8, US9 in parallel

---

## Notes

- [P] tasks = different files, no incomplete-task dependencies
- Application layer frozen — run plan.md freeze checklist before each PR
- Product mapping persistence deferred — session/inline only (FR-018)
- No Stripe write UI — export-only per FR-025
- Commit after each phase checkpoint or logical task group
