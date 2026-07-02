# Tasks: Reconciliation Change Approval Workflow

**Input**: Design documents from `/specs/007-reconciliation-approval-workflow/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md; features 001–006 complete (domain model, ingestion, reconciliation engine, exception surfacing, classification)

**Tests**: Included per constitution Principle II (billing-critical), `quickstart.md` validation scenarios, and approval safety requirements (no auto-approve, export filtering).

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US5) for story-phase tasks only

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Fluent UI package, folder structure, fixture layout, and API test project per plan.md. Azure Storage Aspire wiring already exists from feature 006 — verify only.

- [X] T001 Add `Microsoft.FluentUI.AspNetCore.Components` v5 package to `src/BillDrift.Web/BillDrift.Web.csproj` per `contracts/fluent-ui-integration.md`
- [X] T002 [P] Create `src/BillDrift.Domain/Approval/` folder structure per plan.md
- [X] T003 [P] Create `src/BillDrift.Application/Approval/` folder structure per plan.md
- [X] T004 [P] Create `src/BillDrift.Infrastructure/Approval/` folder structure per plan.md
- [X] T005 [P] Create `src/BillDrift.Api/Approval/` folder for minimal REST endpoints
- [X] T006 [P] Create `tests/BillDrift.Application.Tests/Approval/` and `tests/BillDrift.Infrastructure.Tests/Approval/` test folders
- [X] T007 [P] Create `tests/fixtures/approval/` directory and `tests/fixtures/approval/README.md` mapping fixtures to `quickstart.md` scenarios
- [X] T008 [P] Add `tests/BillDrift.Api.Tests/BillDrift.Api.Tests.csproj` to solution and reference `BillDrift.Api` for endpoint integration tests per plan.md
- [X] T009 Verify existing `AddAzureTableServiceClient("tables")` and `AddAzureBlobServiceClient("blobs")` in `src/BillDrift.Api/Program.cs` (no new manual connection string wiring)

**Checkpoint**: Solution builds; Fluent UI package restored; folder layout matches plan; storage clients already registered.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain types, store abstraction, operator context, and service skeletons. MUST complete before user story implementation.

**⚠️ CRITICAL**: No user story work until this phase is complete.

- [X] T010 [P] Implement `ApprovalDecisionState`, `ApprovalEligibility`, `ApprovalProposalCategory`, `ApprovalRiskIndicator`, and `ApprovalAuditEventType` enums in `src/BillDrift.Domain/Approval/ApprovalEnums.cs` per `data-model.md`
- [X] T011 [P] Implement `ApprovalProposalId` and related identifier types in `src/BillDrift.Domain/Approval/ApprovalIdentifiers.cs`
- [X] T012 [P] Implement `ApprovalProposal` record in `src/BillDrift.Domain/Approval/ApprovalProposal.cs` per `data-model.md`
- [X] T013 [P] Implement `ApprovalDecision` record in `src/BillDrift.Domain/Approval/ApprovalDecision.cs`
- [X] T014 [P] Implement `ApprovalAuditEvent`, `ApprovedChangeset`, and `ApprovedChangesetEntry` records in `src/BillDrift.Domain/Approval/ApprovedChangeset.cs`
- [X] T015 [P] Add domain construction and validation tests in `tests/BillDrift.Domain.Tests/Approval/ApprovalTypesTests.cs`
- [X] T016 Implement `IApprovalStore` interface in `src/BillDrift.Application/Approval/IApprovalStore.cs` per `data-model.md`
- [X] T017 Implement `InMemoryApprovalStore` in `tests/BillDrift.Application.Tests/Approval/InMemoryApprovalStore.cs` for unit and service tests
- [X] T018 [P] Implement `ApprovalStorageOptions` in `src/BillDrift.Infrastructure/Approval/ApprovalStorageOptions.cs`
- [X] T019 [P] Implement `IOperatorContext` and `HeaderOperatorContext` (dev `X-Operator-Id`) in `src/BillDrift.Application/Approval/IOperatorContext.cs`
- [X] T020 Implement `ApprovalIngestionRequest` and view model DTOs (`ApprovalQueueViewModel`, `ApprovalProposalViewModel`, `ApprovalCustomerGroupViewModel`) in `src/BillDrift.Application/Approval/ApprovalViewModels.cs`
- [X] T021 Implement `ApprovalEligibilityEvaluator` skeleton in `src/BillDrift.Application/Approval/ApprovalEligibilityEvaluator.cs`
- [X] T022 Implement `ApprovalIngestionService` skeleton in `src/BillDrift.Application/Approval/ApprovalIngestionService.cs`
- [X] T023 Implement `ApprovalService` skeleton in `src/BillDrift.Application/Approval/ApprovalService.cs`
- [X] T024 [P] Implement `ApprovalServiceCollectionExtensions` stub in `src/BillDrift.Application/Approval/ApprovalServiceCollectionExtensions.cs`
- [X] T025 [P] Implement `ApprovalStorageExtensions` stub in `src/BillDrift.Infrastructure/Approval/ApprovalStorageExtensions.cs`

**Checkpoint**: Domain compiles; in-memory store usable; service skeletons return empty queue; no Stripe mutation code present.

---

## Phase 3: User Story 1 — Review Proposed Subscription Corrections (Priority: P1) 🎯 MVP

**Goal**: Operators can ingest and review subscription proposals (create item, update quantity, switch price) and investigation flags with prior vs proposed values in the approval queue.

**Independent Test**: Given mixed subscription fixture, ingest produces pending proposals with correct action types, prior/proposed values, and investigation items marked non-approvable; queue API and Fluent UI page display them grouped by customer.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T026 [P] [US1] Add `ApprovalIngestionServiceTests` skeleton in `tests/BillDrift.Application.Tests/Approval/ApprovalIngestionServiceTests.cs`
- [X] T027 [P] [US1] Add `ApprovalEligibilityEvaluatorTests` skeleton in `tests/BillDrift.Application.Tests/Approval/ApprovalEligibilityEvaluatorTests.cs`
- [X] T028 [P] [US1] Create `tests/fixtures/approval/mixed-subscription-proposals.json` per `quickstart.md` V1

### Implementation for User Story 1

- [X] T029 [US1] Implement `ProposedChange` snapshot mapping (prior/proposed values, category, execution order) in `src/BillDrift.Application/Approval/ApprovalIngestionService.cs` per `contracts/approval-workflow-pipeline.md`
- [X] T030 [US1] Implement investigation-only proposal synthesis from exceptions without `ProposedChangeId` in `ApprovalIngestionService.cs`
- [X] T031 [US1] Implement eligibility rules mirroring 005 suppression (low confidence, non-CSP, mapping ambiguous) in `src/BillDrift.Application/Approval/ApprovalEligibilityEvaluator.cs` per research R10
- [X] T032 [US1] Implement `IngestAsync` orchestration (all proposals start `Pending`, never auto-approve) in `src/BillDrift.Application/Approval/ApprovalService.cs`
- [X] T033 [US1] Implement `GetQueueAsync` building `ApprovalQueueViewModel` with customer grouping in `ApprovalService.cs`
- [X] T034 [US1] Implement `POST /api/reconciliation/{runId}/approvals/ingest` and `GET /api/reconciliation/{runId}/approvals` in `src/BillDrift.Api/Approval/ApprovalEndpoints.cs` per `contracts/approval-api-endpoints.md`
- [X] T035 [US1] Register `AddFluentUIComponents()` and approval HttpClient in `src/BillDrift.Web/Program.cs` per `contracts/fluent-ui-integration.md`
- [X] T036 [US1] Add `<FluentProviders />` and `default-fuib.css` link in `src/BillDrift.Web/Components/App.razor`
- [X] T037 [US1] Refactor `src/BillDrift.Web/Components/Layout/MainLayout.razor` to `FluentLayout` + `FluentNav` (v5) replacing Bootstrap sidebar per skill
- [X] T038 [US1] Implement `ApprovalApiClient` in `src/BillDrift.Web/Services/ApprovalApiClient.cs` calling ingest and queue endpoints
- [X] T039 [US1] Implement read-only `ApprovalQueuePage.razor` in `src/BillDrift.Web/Pages/Approvals/ApprovalQueuePage.razor` with `FluentDataGrid`, prior/proposed columns, and `FluentBadge` for state/eligibility
- [X] T040 [US1] Add route `/approvals/{RunId:guid}` in `src/BillDrift.Web/Components/Routes.razor`
- [X] T041 [US1] Add ingest creates pending proposals test in `ApprovalIngestionServiceTests.cs` per `quickstart.md` V1
- [X] T042 [US1] Add no auto-approve on ingest test in `ApprovalIngestionServiceTests.cs` per `quickstart.md` V10 and SC-002
- [X] T043 [US1] Add mapping-ambiguous investigation ineligible test in `ApprovalEligibilityEvaluatorTests.cs` per `quickstart.md` V4

**Checkpoint**: Ingest via API; queue shows subscription proposals and investigation flags; Fluent layout renders; zero proposals auto-approved.

---

## Phase 4: User Story 2 — Approve, Reject, and Track Decision State (Priority: P1)

**Goal**: Operators approve or reject proposals with mandatory rejection reason; decisions persist with actor and timestamp; supersession on re-run marks stale/historical items.

**Independent Test**: Approve one proposal, reject another with reason, leave third pending; states and audit distinguish all three; re-ingest supersedes without mutating prior decision rows.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T044 [P] [US2] Add `ApprovalServiceDecisionTests` skeleton in `tests/BillDrift.Application.Tests/Approval/ApprovalServiceDecisionTests.cs`
- [X] T045 [P] [US2] Add `AzureTableApprovalStoreTests` skeleton in `tests/BillDrift.Infrastructure.Tests/Approval/AzureTableApprovalStoreTests.cs`
- [X] T046 [P] [US2] Create `tests/fixtures/approval/quantity-mismatch-proposal.json` per `quickstart.md` V2

### Implementation for User Story 2

- [X] T047 [US2] Implement `ApprovalTableEntities` mapping in `src/BillDrift.Infrastructure/Approval/ApprovalTableEntities.cs` per `contracts/azure-table-schema.md`
- [X] T048 [US2] Implement `AzureTableApprovalStore` using DI-injected `TableServiceClient` in `src/BillDrift.Infrastructure/Approval/AzureTableApprovalStore.cs` (no manual connection strings)
- [X] T049 [US2] Complete `ApprovalStorageExtensions` registering Azure store in `src/BillDrift.Infrastructure/Approval/ApprovalStorageExtensions.cs`
- [X] T050 [US2] Implement `ApproveAsync` with state validation and `ApprovedWhileEligible` flag in `src/BillDrift.Application/Approval/ApprovalService.cs`
- [X] T051 [US2] Implement `RejectAsync` with mandatory rejection reason in `ApprovalService.cs`
- [X] T052 [US2] Implement append-only decision and audit rows in `AzureTableApprovalStore.cs`
- [X] T053 [US2] Implement supersession algorithm (Stale/Historical) on re-ingest in `src/BillDrift.Application/Approval/ApprovalIngestionService.cs` per research R7
- [X] T054 [US2] Implement stale approval requiring `AcknowledgedStale` in `ApprovalService.cs` per `quickstart.md` V8
- [X] T055 [US2] Implement bulk approve preview token and `BulkApproveAsync` in `ApprovalService.cs` per research R12
- [X] T056 [US2] Implement approve/reject/bulk-approve API endpoints in `src/BillDrift.Api/Approval/ApprovalEndpoints.cs`
- [X] T057 [US2] Add approve/reject controls and `RejectProposalDialog.razor` in `src/BillDrift.Web/Components/Approval/RejectProposalDialog.razor`
- [X] T058 [US2] Add `BulkApproveDialog.razor` in `src/BillDrift.Web/Components/Approval/BulkApproveDialog.razor`
- [X] T059 [US2] Wire approve/reject/bulk actions in `ApprovalQueuePage.razor`
- [X] T060 [US2] Add approve quantity update test in `ApprovalServiceDecisionTests.cs` per `quickstart.md` V2
- [X] T061 [US2] Add reject requires reason test in `ApprovalServiceDecisionTests.cs` per `quickstart.md` V3
- [X] T062 [US2] Add supersession immutability test in `ApprovalServiceDecisionTests.cs` per `quickstart.md` V7 and SC-008
- [X] T063 [US2] Add Azure table decision round-trip test in `AzureTableApprovalStoreTests.cs` (Azurite)

**Checkpoint**: Approve/reject persists to Azure Tables; audit append-only; supersession works; UI actions functional.

---

## Phase 5: User Story 4 — Export Approved Changeset (Priority: P1)

**Goal**: Export deterministic JSON changeset containing only approved items to Azure Blob Storage with download via API.

**Independent Test**: Approve subset of proposals; export returns blob with only approved entries in correct order; pending/rejected/investigation excluded; empty export returns 422.

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T064 [P] [US4] Add `ApprovedChangesetBuilderTests` skeleton in `tests/BillDrift.Application.Tests/Approval/ApprovedChangesetBuilderTests.cs`
- [X] T065 [P] [US4] Add `AzureBlobChangesetExporterTests` skeleton in `tests/BillDrift.Infrastructure.Tests/Approval/AzureBlobChangesetExporterTests.cs`

### Implementation for User Story 4

- [X] T066 [US4] Implement `ApprovedChangesetBuilder` with deterministic ordering (catalogue before subscription tie-break) in `src/BillDrift.Application/Approval/ApprovedChangesetBuilder.cs` per `contracts/azure-blob-changeset-export.md`
- [X] T067 [US4] Implement `AzureBlobChangesetExporter` using DI-injected `BlobServiceClient` in `src/BillDrift.Infrastructure/Approval/AzureBlobChangesetExporter.cs`
- [X] T068 [US4] Implement `ExportApprovedChangesetAsync` blocking pending/rejected/ineligible in `src/BillDrift.Application/Approval/ApprovalService.cs`
- [X] T069 [US4] Implement export metadata rows in `AzureTableApprovalStore.cs` per `contracts/azure-table-schema.md`
- [X] T070 [US4] Implement `POST /export` and `GET /export/{exportId}/download` in `src/BillDrift.Api/Approval/ApprovalEndpoints.cs`
- [X] T071 [US4] Implement `ExportChangesetPanel.razor` in `src/BillDrift.Web/Components/Approval/ExportChangesetPanel.razor`
- [X] T072 [US4] Wire export panel into `ApprovalQueuePage.razor`
- [X] T073 [US4] Add export only approved items test in `ApprovedChangesetBuilderTests.cs` per `quickstart.md` V5 and SC-003
- [X] T074 [US4] Add deterministic ordering test in `ApprovedChangesetBuilderTests.cs` per `quickstart.md` V6
- [X] T075 [US4] Add blob round-trip export test in `AzureBlobChangesetExporterTests.cs` (Azurite)
- [X] T076 [US4] Add API export integration test in `tests/BillDrift.Api.Tests/Approval/ApprovalEndpointsTests.cs`

**Checkpoint**: Approved changeset JSON in blob storage; API download works; export excludes non-approved items.

---

## Phase 6: User Story 3 — Catalogue Fixes with Conflict Safeguards (Priority: P2)

**Goal**: Catalogue proposals shown separately from subscription items; duplicate/conflict flags non-approvable; partial catalogue approve exports only catalogue entries.

**Independent Test**: Mixed catalogue/subscription run shows separate tabs; conflict flag is `CatalogueConflict`; approving only catalogue items exports catalogue-only changeset.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T077 [P] [US3] Add catalogue eligibility tests to `ApprovalEligibilityEvaluatorTests.cs`
- [X] T078 [P] [US3] Create `tests/fixtures/approval/mapping-ambiguous-investigation.json` per `quickstart.md` V4 (reuse for investigation; add catalogue fixture if split needed)

### Implementation for User Story 3

- [X] T079 [US3] Map `CreateOrUpdateCatalogueEntry` proposals to `ApprovalProposalCategory.Catalogue` in `ApprovalIngestionService.cs`
- [X] T080 [US3] Implement duplicate/conflict catalogue detection → `ApprovalEligibility.CatalogueConflict` in `ApprovalEligibilityEvaluator.cs` per spec FR-008
- [X] T081 [US3] Add subscription vs catalogue grouping in `GetQueueAsync` view model in `ApprovalService.cs`
- [X] T082 [US3] Add `FluentTabs` subscription/catalogue/investigation filter in `ApprovalQueuePage.razor` per FR-022
- [X] T083 [US3] Block approve on `CatalogueConflict` and `InvestigationOnly` in `ApprovalService.ApproveAsync`
- [X] T084 [US3] Add catalogue-only partial export test in `ApprovedChangesetBuilderTests.cs` per spec US3 acceptance scenario 4
- [X] T085 [US3] Add conflict flag non-exportable test in `ApprovalEligibilityEvaluatorTests.cs` per SC-005

**Checkpoint**: Catalogue items separated in UI; conflicts never approvable; partial export respects category selection.

---

## Phase 7: User Story 5 — Inspect Audit History (Priority: P2)

**Goal**: Operators and reviewers query immutable audit trail showing who decided, when, prior vs proposed values, and rejection reasons.

**Independent Test**: After approve/reject/export, audit API returns chronological events; re-run does not mutate historical entries; read-only operator cannot approve/export.

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T086 [P] [US5] Add `ApprovalAuditTests` skeleton in `tests/BillDrift.Application.Tests/Approval/ApprovalAuditTests.cs`

### Implementation for User Story 5

- [X] T087 [US5] Implement `GetAuditHistoryAsync` querying `audit` and `decision` partitions in `ApprovalService.cs`
- [X] T088 [US5] Append audit events on ingest, decision, bulk decision, export, and supersession in `ApprovalService.cs` and store
- [X] T089 [US5] Implement `GET /api/reconciliation/{runId}/approvals/audit` in `src/BillDrift.Api/Approval/ApprovalEndpoints.cs`
- [X] T090 [US5] Implement audit history panel component in `src/BillDrift.Web/Components/Approval/ApprovalAuditPanel.razor`
- [X] T091 [US5] Wire audit panel into `ApprovalQueuePage.razor`
- [X] T092 [US5] Implement read-only operator mode (disable approve/export when `IOperatorContext.CanApprove == false`) in `ApprovalEndpoints.cs` and Web UI
- [X] T093 [US5] Add audit immutability on re-run test in `ApprovalAuditTests.cs` per spec US5 acceptance scenario 3
- [X] T094 [US5] Add rejected proposal audit includes reason test in `ApprovalAuditTests.cs` per spec US5 acceptance scenario 2

**Checkpoint**: Full audit trail queryable; export/decision events recorded; read-only operators blocked from mutations.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: DI registration, storage boundary verification, full quickstart validation, and constitution compliance.

- [X] T095 Register `AddApproval()` and call `AddApprovalStorage()` from `src/BillDrift.Api/Program.cs` after Aspire client registration
- [X] T096 [P] Verify `BillDrift.Web` has **no** `TableServiceClient` or `BlobServiceClient` registration (API-only storage access)
- [X] T097 [P] Grep approval codebase for Stripe mutation/API write calls — confirm none exist per FR-010
- [X] T098 Run full `quickstart.md` validation scenarios V1–V10; document pass/fail in `specs/007-reconciliation-approval-workflow/quickstart.md` checklist section
- [X] T099 Run `dotnet build` and `dotnet test` on full solution with zero errors/warnings per workspace rules
- [X] T100 [P] Principle VI simplicity review — confirm `IApprovalStore` is sole new persistence abstraction; no SQL introduced

**Checkpoint**: Full test suite passes; quickstart scenarios verified; storage and safety constraints confirmed.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **User Story 1 (Phase 3)**: Depends on Foundational — **MVP (review queue)**
- **User Story 2 (Phase 4)**: Depends on US1 ingest/queue — **MVP (decisions)**
- **User Story 4 (Phase 5)**: Depends on US2 approve — export requires approved items
- **User Story 3 (Phase 6)**: Depends on US1 ingest; can parallel with US4 after US2
- **User Story 5 (Phase 7)**: Depends on US2 store; can parallel with US3/US4
- **Polish (Phase 8)**: Depends on all desired user stories

### User Story Dependencies

| Story | Depends on | Can start after |
|-------|------------|-----------------|
| US1 | Foundational | Phase 2 complete |
| US2 | US1 ingest/queue | Phase 3 complete |
| US4 | US2 approve | Phase 4 complete |
| US3 | US1 ingest | Phase 3 complete (UI tabs after US1 page exists) |
| US5 | US2 audit storage | Phase 4 complete |

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Domain/store before services
- Services before API endpoints
- API before Web UI consumption
- Story checkpoint before next priority

### Parallel Opportunities

- Phase 1: T002–T008 can run in parallel after T001
- Phase 2: T010–T015, T018–T019, T024–T025 can run in parallel
- US1: T026–T028 tests and T028 fixture in parallel; T036–T037 UI setup parallel with T029–T034 backend after T029 starts
- US2: T044–T046 parallel; T057–T058 dialog components parallel
- US4: T064–T065 parallel
- US3/US5 can proceed in parallel once US2 complete

---

## Parallel Example: User Story 1

```bash
# Tests and fixtures together:
T026 ApprovalIngestionServiceTests.cs
T027 ApprovalEligibilityEvaluatorTests.cs
T028 mixed-subscription-proposals.json

# Domain parallel work (already in Phase 2):
T010 ApprovalEnums.cs
T012 ApprovalProposal.cs

# After ingest service started — UI in parallel with API:
T034 ApprovalEndpoints.cs (ingest/GET)
T036 App.razor FluentProviders
T037 MainLayout.razor FluentLayout
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 + 4)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: US1 — ingest and review queue (Fluent UI shell)
4. Complete Phase 4: US2 — approve/reject with audit persistence
5. Complete Phase 5: US4 — export approved changeset
6. **STOP and VALIDATE**: Run quickstart V1–V6, V10; demo operator approve → export flow

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. US1 → review queue live (read-only) → Demo
3. US2 → decisions + persistence → Demo
4. US4 → export handoff → Demo (**core safety workflow complete**)
5. US3 → catalogue separation → Demo
6. US5 → audit panel → Demo

### Parallel Team Strategy

With multiple developers after Phase 2:

- Developer A: US1 backend ingest + eligibility
- Developer B: US1 Fluent UI layout + queue page
- After US1: Developer A → US2 decisions; Developer B → US4 export UI
- Developer C (optional): US3 catalogue + US5 audit after US2

---

## Notes

- [P] tasks = different files, no dependencies on incomplete work
- [Story] label maps task to spec user story for traceability
- Storage: Azure Tables + Blobs only; Aspire DI clients in API/Infrastructure only
- Fluent UI: v5 component names only (`FluentNav`, not `FluentNavMenu`)
- Commit after each task or logical group
- Avoid: Stripe writes in approval code, SQL, direct storage access from Web

---

## Task Summary

| Phase | Story | Task count |
|-------|-------|------------|
| 1 Setup | — | 9 |
| 2 Foundational | — | 16 |
| 3 US1 | Review subscription corrections | 18 |
| 4 US2 | Approve/reject/state | 20 |
| 5 US4 | Export changeset | 13 |
| 6 US3 | Catalogue fixes | 9 |
| 7 US5 | Audit history | 9 |
| 8 Polish | — | 6 |
| **Total** | | **100** |

**Suggested MVP scope**: Phase 1 + Phase 2 + US1 + US2 + US4 (T001–T076) — ingest, review, decide, export without catalogue tabs polish or full audit UI.

**Format validation**: All 100 tasks use `- [ ] [TaskID] [P?] [Story?] Description with file path` format.
