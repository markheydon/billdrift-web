# Tasks: Giacom Subscription Management CSV Ingestion

**Input**: Design documents from `/specs/009-giacom-subscription-csv/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md; `001-billing-domain-model` complete (`RawSubscriptionManagementRow`, `MicrosoftSubscriptionLine`, `ISubscriptionManagementNormalizer` stub); `002-giacom-pdf-ingestion` and `003-stripe-csv-ingestion` complete (shared `IngestionLogEntry`, `GoldenFileComparer`, CsvHelper in Infrastructure); `008-reconciliation-run-history` complete (`InputSnapshotMetadata` consumer)

**UI note**: Blazor upload UI is **out of scope** — API endpoints only. `BillDrift.Web` calls API; no `BlobServiceClient` or `TableServiceClient` in Web project.

**Tests**: Included per constitution Principle II, `quickstart.md` validation scenarios, and `contracts/csv-ingestion-pipeline.md`.

**Organization**: Tasks grouped by user story for independent implementation and testing. Azure persistence + API in dedicated phase after parser stories.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US5) for story-phase tasks only

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Fixture layout, storage options, and verify shared dependencies.

- [ ] T001 Verify `CsvHelper` package reference exists in `src/BillDrift.Infrastructure/BillDrift.Infrastructure.csproj` (reuse from 003; no duplicate add)
- [ ] T002 [P] Create `tests/fixtures/subscription-management/` and `tests/fixtures/subscription-management/expected/` directory structure
- [ ] T003 [P] Add `tests/fixtures/subscription-management/README.md` documenting required fixtures per `quickstart.md` and commit policy for sanitized CSV exports
- [ ] T004 Obtain and place sanitized CSV fixtures (minimum: `subscription-management-sample-a.csv`, `mixed-products.csv`, `column-variant.csv`, `partial-success.csv`, `lifecycle-columns.csv`) under `tests/fixtures/subscription-management/`

**Checkpoint**: Fixture directories ready; CsvHelper available; production CSVs may arrive in parallel with parser work.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain extensions, Application contracts, internal parse types, header map, and shared helpers. MUST complete before user story implementation.

**⚠️ CRITICAL**: No user story work until this phase is complete.

- [ ] T005 Extend `IngestionFailureReason` enum with subscription-management codes (`ProductOutOfScope`, `ProductScopeAmbiguous`, `LicenceCountUnparseable`, `PriceUnparseable`, `CommercialKeyMissing`, `DateUnparseable`) in `src/BillDrift.Application/Import/IngestionEnums.cs` per research R11
- [ ] T006 [P] Implement `ProductDisplayFacts` record in `src/BillDrift.Domain/Billing/ProductDisplayFacts.cs` per `data-model.md`
- [ ] T007 [P] Implement `SubscriptionLifecycleFacts` record in `src/BillDrift.Domain/Billing/SubscriptionLifecycleFacts.cs` per `data-model.md`
- [ ] T008 Extend `RawSubscriptionManagementRow` with optional raw lifecycle/pricing/display fields in `src/BillDrift.Domain/Import/RawSubscriptionManagementRow.cs` per `data-model.md`
- [ ] T009 Extend `MicrosoftSubscriptionLine` with optional `ProductDisplay` and `Lifecycle` parameters in `src/BillDrift.Domain/Billing/MicrosoftSubscriptionLine.cs` per `data-model.md`
- [ ] T010 [P] Implement `SubscriptionManagementCsvIngestionOptions` record in `src/BillDrift.Application/Import/SubscriptionManagementCsvIngestionOptions.cs` per `data-model.md`
- [ ] T011 [P] Implement `SubscriptionManagementCsvIngestionRequest` record in `src/BillDrift.Application/Import/SubscriptionManagementCsvIngestionRequest.cs`
- [ ] T012 [P] Implement `SubscriptionManagementSourceFileInfo` record in `src/BillDrift.Application/Import/SubscriptionManagementSourceFileInfo.cs`
- [ ] T013 [P] Implement `SubscriptionManagementCsvIngestionSummary` record in `src/BillDrift.Application/Import/SubscriptionManagementCsvIngestionSummary.cs`
- [ ] T014 Implement `SubscriptionManagementCsvIngestionResult` record in `src/BillDrift.Application/Import/SubscriptionManagementCsvIngestionResult.cs` per `data-model.md`
- [ ] T015 Implement `ISubscriptionManagementCsvIngester` interface in `src/BillDrift.Application/Import/ISubscriptionManagementCsvIngester.cs` per `contracts/csv-ingestion-pipeline.md`
- [ ] T016 [P] Implement `IngestionRunStatus` enum in `src/BillDrift.Application/Ingestion/IngestionRunStatus.cs`
- [ ] T017 [P] Implement `SubscriptionManagementIngestionRun` record in `src/BillDrift.Application/Ingestion/SubscriptionManagementIngestionRun.cs` per `data-model.md`
- [ ] T018 [P] Define `IIngestionBlobStore` interface in `src/BillDrift.Application/Ingestion/IIngestionBlobStore.cs` per `contracts/azure-blob-ingestion-archive.md`
- [ ] T019 [P] Define `IIngestionRunIndexStore` interface in `src/BillDrift.Application/Ingestion/IIngestionRunIndexStore.cs` per `contracts/azure-table-ingestion-index.md`
- [ ] T020 [P] Define `ISubscriptionManagementIngestionService` interface in `src/BillDrift.Application/Import/SubscriptionManagement/ISubscriptionManagementIngestionService.cs` per `contracts/csv-ingestion-pipeline.md`
- [ ] T021 [P] Implement `ParsedSubscriptionManagementRow` in `src/BillDrift.Infrastructure/Import/Giacom/SubscriptionManagement/Internal/ParsedSubscriptionManagementRow.cs`
- [ ] T022 Implement SHA-256 file hash helper in `src/BillDrift.Infrastructure/Import/Giacom/SubscriptionManagement/SubscriptionManagementFileIdentity.cs` per research R2
- [ ] T023 Implement intake limits (`MaxFileSizeBytes` default 10 MB) in `src/BillDrift.Infrastructure/Import/Giacom/SubscriptionManagement/SubscriptionManagementIngestionLimits.cs`
- [ ] T024 Implement `SubscriptionManagementCsvHeaderMap` column alias registry in `src/BillDrift.Infrastructure/Import/Giacom/SubscriptionManagement/SubscriptionManagementCsvHeaderMap.cs` per `contracts/subscription-csv-header-map.md`
- [ ] T025 Implement `SubscriptionManagementCsvRowReader` CsvHelper wrapper in `src/BillDrift.Infrastructure/Import/Giacom/SubscriptionManagement/SubscriptionManagementCsvRowReader.cs`
- [ ] T026 [P] Implement `IngestionStorageOptions` in `src/BillDrift.Infrastructure/Ingestion/IngestionStorageOptions.cs` per `contracts/azure-table-ingestion-index.md`
- [ ] T027 [P] Create `IngestionJsonSerializerContext` source-gen skeleton in `src/BillDrift.Infrastructure/Ingestion/IngestionJsonSerializerContext.cs`
- [ ] T028 Extend `GiacomImportServiceCollectionExtensions` skeleton for subscription CSV ingester in `src/BillDrift.Infrastructure/Import/Giacom/GiacomImportServiceCollectionExtensions.cs`
- [ ] T029 Reuse or reference shared `GoldenFileComparer` in `tests/BillDrift.Infrastructure.Tests/Import/Giacom/SubscriptionManagement/GoldenFileComparer.cs` (from Giacom PDF or Stripe tests)

**Checkpoint**: Application contract compiles; domain types extended; header map and row reader available.

---

## Phase 3: User Story 1 — Import Microsoft Subscription Truth (Priority: P1) 🎯 MVP

**Goal**: Submit `SubscriptionManagementReport.csv` and receive `RawSubscriptionManagementRow` plus `MicrosoftSubscriptionLine` records with customer, commercial keys, licence count, term, frequency, renewal date, status, and supplier subscription reference.

**Independent Test**: Given `subscription-management-sample-a.csv`, pipeline returns subscription truth lines with non-empty Mex ID, licence count, status, offer ID, and SKU ID for each qualifying M365 row; multiple customers correctly associated.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T030 [P] [US1] Create integration test skeleton in `tests/BillDrift.Infrastructure.Tests/Import/Giacom/SubscriptionManagement/SubscriptionManagementCsvIngesterTests.cs`
- [ ] T031 [P] [US1] Add multi-customer association assertion test in `SubscriptionManagementCsvIngesterTests.cs` per spec acceptance scenario 2

### Implementation for User Story 1

- [ ] T032 [US1] Implement `RawSubscriptionManagementRowMapper` core field mapping in `src/BillDrift.Infrastructure/Import/Giacom/SubscriptionManagement/RawSubscriptionManagementRowMapper.cs`
- [ ] T033 [US1] Implement `RawImportId.Create(ImportSourceKind.GiacomSubscriptionManagement, sourceDocumentId, lineKey)` assignment in `RawSubscriptionManagementRowMapper.cs` per research R2
- [ ] T034 [US1] Implement mandatory field validation (Mex ID, licences, status, offer ID, SKU ID presence) in `RawSubscriptionManagementRowMapper.cs`
- [ ] T035 [US1] Implement `SubscriptionManagementCsvIngester` orchestrator skeleton in `src/BillDrift.Infrastructure/Import/Giacom/SubscriptionManagement/SubscriptionManagementCsvIngester.cs` per `contracts/csv-ingestion-pipeline.md`
- [ ] T036 [US1] Complete intake, header detection, row parsing, and raw mapping stages in `SubscriptionManagementCsvIngester.cs`
- [ ] T037 [US1] Wire basic normalization pass-through (identity normalizer stub or inline) so `SubscriptionLines` populated for happy path in `SubscriptionManagementCsvIngester.cs`
- [ ] T038 [US1] Complete result assembly with `IngestionOutcomeStatus`, summary counts, and `IngestionLogEntry` list in `SubscriptionManagementCsvIngester.cs`
- [ ] T039 [US1] Register `AddGiacomSubscriptionManagementCsvIngestion` (`Singleton<ISubscriptionManagementCsvIngester>`) in `GiacomImportServiceCollectionExtensions.cs`
- [ ] T040 [US1] Register ingester in `src/BillDrift.Api/Program.cs` via `AddGiacomBillingPdfIngestion` extension or dedicated call
- [ ] T041 [US1] Generate golden JSON `tests/fixtures/subscription-management/expected/sample-a.json` from validated output
- [ ] T042 [US1] Complete golden-file assertions in `SubscriptionManagementCsvIngesterTests.cs` per `quickstart.md` Scenario 1

**Checkpoint**: MVP ingests sample CSV independently; golden test passes; mandatory fields captured.

---

## Phase 4: User Story 2 — Scope to Microsoft 365 CSP Products Only (Priority: P1)

**Goal**: Exclude non-M365 / non-CSP products (e.g., Exclaimer) from output with logged exclusion summary counts.

**Independent Test**: Given `mixed-products.csv`, Exclaimer rows absent from output; `Summary.RowsExcludedByScope` matches excluded row count.

### Tests for User Story 2

- [ ] T043 [P] [US2] Create `ProductScopeClassifierTests` in `tests/BillDrift.Infrastructure.Tests/Import/Giacom/SubscriptionManagement/ProductScopeClassifierTests.cs` per `contracts/product-scope-rules.md`
- [ ] T044 [P] [US2] Add mixed-products integration test in `SubscriptionManagementCsvIngesterTests.cs` per SC-002

### Implementation for User Story 2

- [ ] T045 [P] [US2] Implement `SubscriptionManagementScopeOptions` with deny/allow token lists in `src/BillDrift.Infrastructure/Import/Giacom/SubscriptionManagement/SubscriptionManagementScopeOptions.cs` per `contracts/product-scope-rules.md`
- [ ] T046 [US2] Implement `ProductScopeClassifier` in `src/BillDrift.Infrastructure/Import/Giacom/SubscriptionManagement/ProductScopeClassifier.cs` per research R4
- [ ] T047 [US2] Integrate scope filter stage (before raw mapping) into `SubscriptionManagementCsvIngester.cs` per pipeline contract stage 4
- [ ] T048 [US2] Emit `ProductOutOfScope` and `ProductScopeAmbiguous` log entries with row location in `SubscriptionManagementCsvIngester.cs`
- [ ] T049 [US2] Update `SubscriptionManagementCsvIngestionSummary.RowsExcludedByScope` rollup in `SubscriptionManagementCsvIngester.cs`

**Checkpoint**: Scope filter active; non-CSP rows excluded with summary; SC-002 test passes.

---

## Phase 5: User Story 3 — Normalise Customer and Product Identity (Priority: P1)

**Goal**: Mex ID as primary customer key; Offer ID + SKU ID as primary product keys; raw values retained for traceability.

**Independent Test**: Given rows with mixed-case/whitespace Mex ID and commercial keys, normalized `CustomerIdentity` and `CommercialKeyRoot` are consistent; raw fields preserved.

### Tests for User Story 3

- [ ] T050 [P] [US3] Create `SubscriptionManagementNormalizerTests` in `tests/BillDrift.Application.Tests/Normalization/SubscriptionManagementNormalizerTests.cs`
- [ ] T051 [P] [US3] Add casing/whitespace normalization assertion tests in `SubscriptionManagementNormalizerTests.cs` per spec acceptance scenario 1–2

### Implementation for User Story 3

- [ ] T052 [US3] Implement `SubscriptionManagementNormalizer` implementing `ISubscriptionManagementNormalizer` in `src/BillDrift.Application/Normalization/SubscriptionManagementNormalizer.cs` per `contracts/csv-ingestion-pipeline.md` normalization table
- [ ] T053 [US3] Implement Mex ID trim + uppercase normalisation with raw traceability in `SubscriptionManagementNormalizer.cs` per research R6
- [ ] T054 [US3] Implement Offer ID + SKU ID trim normalisation into `CommercialKeyRoot` in `SubscriptionManagementNormalizer.cs`
- [ ] T055 [US3] Map `CustomerNameRaw` to `CustomerIdentity.DisplayName` without cross-row merge in `SubscriptionManagementNormalizer.cs` per spec acceptance scenario 3
- [ ] T056 [US3] Implement `ProductDisplayFacts` mapping from raw service/product/product-type fields in `SubscriptionManagementNormalizer.cs`
- [ ] T057 [US3] Replace ingester inline normalization with `SubscriptionManagementNormalizer` in `SubscriptionManagementCsvIngester.cs`
- [ ] T058 [US3] Add commercial-key-missing warning (`CommercialKeyMissing`) without inventing IDs in `SubscriptionManagementCsvIngester.cs` per FR-017

**Checkpoint**: Normalizer tests pass; cross-domain correlation keys consistent with 002/003 conventions.

---

## Phase 6: User Story 4 — Extended Lifecycle and Pricing Fields (Priority: P2)

**Goal**: Capture NCE/trial flags, end-of-term action, cancellable-until, migration-to-NCE, assigned licences, price/ERP when columns present.

**Independent Test**: Given `lifecycle-columns.csv`, populated optional columns appear on `SubscriptionLifecycleFacts`; blank columns remain absent.

### Tests for User Story 4

- [ ] T059 [P] [US4] Create `BooleanFlagParserTests` in `tests/BillDrift.Infrastructure.Tests/Import/Giacom/SubscriptionManagement/BooleanFlagParserTests.cs` per research R5
- [ ] T060 [P] [US4] Add lifecycle-column integration test in `SubscriptionManagementCsvIngesterTests.cs` per SC-006

### Implementation for User Story 4

- [ ] T061 [P] [US4] Implement `BooleanFlagParser` in `src/BillDrift.Infrastructure/Import/Giacom/SubscriptionManagement/BooleanFlagParser.cs` per research R5
- [ ] T062 [US4] Extend `RawSubscriptionManagementRowMapper` to map optional lifecycle/pricing columns in `RawSubscriptionManagementRowMapper.cs`
- [ ] T063 [US4] Extend `SubscriptionManagementNormalizer` to populate `SubscriptionLifecycleFacts` (dates, flags, assigned licences, Money) in `SubscriptionManagementNormalizer.cs` per research R12
- [ ] T064 [US4] Implement price/ERP parse with `PriceUnparseable` row skip when column non-empty and invalid in `SubscriptionManagementCsvIngester.cs`
- [ ] T065 [US4] Ensure blank optional columns emit absent lifecycle fields (no defaults) in `SubscriptionManagementNormalizer.cs` per spec acceptance scenario 3

**Checkpoint**: Lifecycle and pricing fields captured; SC-006 test passes.

---

## Phase 7: User Story 5 — Format Variation and Partial Row Failures (Priority: P2)

**Goal**: Tolerate column reordering/synonyms; continue on individual row failures with structured logging and partial success status.

**Independent Test**: Given `partial-success.csv` and `column-variant.csv`, valid rows emitted, skipped rows logged, `PartialSuccess` when applicable.

### Tests for User Story 5

- [ ] T066 [P] [US5] Add partial-success fixture test in `SubscriptionManagementCsvIngesterTests.cs` per spec acceptance scenario 1
- [ ] T067 [P] [US5] Add column-variant header reorder test in `SubscriptionManagementCsvIngesterTests.cs` per spec acceptance scenario 2
- [ ] T068 [P] [US5] Add determinism re-parse test (identical bytes → identical `RawImportId` keys) in `SubscriptionManagementCsvIngesterTests.cs` per SC-004

### Implementation for User Story 5

- [ ] T069 [US5] Implement row skip for missing Mex ID (`MexIdMissing`) in `SubscriptionManagementCsvIngester.cs` per spec acceptance scenario 4
- [ ] T070 [US5] Implement row skip for unparseable licence count (`LicenceCountUnparseable`) in `SubscriptionManagementCsvIngester.cs`
- [ ] T071 [US5] Implement file-level fail for missing mandatory headers (`MandatoryHeaderMissing`) in `SubscriptionManagementCsvIngester.cs` per FR-025
- [ ] T072 [US5] Implement `PartialSuccess` vs `Success` vs `Failure` outcome resolution in `SubscriptionManagementCsvIngester.cs` per pipeline contract
- [ ] T073 [US5] Validate header alias coverage against `column-variant.csv` and extend `SubscriptionManagementCsvHeaderMap.cs` as needed

**Checkpoint**: Partial success and format tolerance verified; SC-003/SC-004/SC-005 scenarios pass.

---

## Phase 8: Azure Persistence and API Upload

**Purpose**: Persist uploads and results via Aspire-injected `BlobServiceClient` and `TableServiceClient` only — **no SQL**, no manual connection strings.

**Independent Test**: `POST /api/imports/subscription-management` stores source CSV blob, result JSON blobs, table index row; `GET` returns run summary.

### Tests for Azure Persistence

- [ ] T074 [P] Implement `InMemoryIngestionBlobStore` in `src/BillDrift.Infrastructure/Ingestion/InMemoryIngestionBlobStore.cs` for unit tests
- [ ] T075 [P] Implement `InMemoryIngestionRunIndexStore` in `src/BillDrift.Infrastructure/Ingestion/InMemoryIngestionRunIndexStore.cs` for unit tests
- [ ] T076 [P] Create `AzureBlobIngestionArchiveStoreTests` in `tests/BillDrift.Infrastructure.Tests/Ingestion/AzureBlobIngestionArchiveStoreTests.cs` (Azurite when available)
- [ ] T077 [P] Create `AzureTableIngestionRunIndexStoreTests` in `tests/BillDrift.Infrastructure.Tests/Ingestion/AzureTableIngestionRunIndexStoreTests.cs` (Azurite when available)

### Implementation for Azure Persistence

- [ ] T078 Implement `AzureBlobIngestionArchiveStore` with constructor `(BlobServiceClient, IOptions<IngestionStorageOptions>)` in `src/BillDrift.Infrastructure/Ingestion/AzureBlobIngestionArchiveStore.cs` per `contracts/azure-blob-ingestion-archive.md`
- [ ] T079 Implement `AzureTableIngestionRunIndexStore` with constructor `(TableServiceClient, IOptions<IngestionStorageOptions>)` in `src/BillDrift.Infrastructure/Ingestion/AzureTableIngestionRunIndexStore.cs` per `contracts/azure-table-ingestion-index.md`
- [ ] T080 Complete `IngestionJsonSerializerContext` with domain types for raw rows and subscription lines in `src/BillDrift.Infrastructure/Ingestion/IngestionJsonSerializerContext.cs`
- [ ] T081 Implement `IngestionServiceCollectionExtensions.AddIngestionStorage` registering Azure + in-memory stores in `src/BillDrift.Infrastructure/Ingestion/IngestionServiceCollectionExtensions.cs`
- [ ] T082 Implement `SubscriptionManagementIngestionService` orchestrating blob upload → ingest → result persist → table index in `src/BillDrift.Application/Import/SubscriptionManagement/SubscriptionManagementIngestionService.cs`
- [ ] T083 Implement `SubscriptionManagementImportEndpoints` (`POST`, `GET` list, `GET` detail, `GET` subscription-truth) in `src/BillDrift.Api/Imports/SubscriptionManagementImportEndpoints.cs` per `contracts/azure-table-ingestion-index.md`
- [ ] T084 Register `AddIngestionStorage` and map import endpoints in `src/BillDrift.Api/Program.cs` (Aspire `BlobServiceClient`/`TableServiceClient` already registered)
- [ ] T085 Add API integration test for multipart upload round-trip in `tests/BillDrift.Infrastructure.Tests/Ingestion/SubscriptionManagementImportApiTests.cs` or dedicated API test project

**Checkpoint**: Upload API persists to blob + table; manifest-last write protocol; no SQL introduced.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Run-history integration, validation, and final quality gates.

- [ ] T086 [P] Ensure ingestion result exposes `ContentFingerprint` and `UploadedAt` fields consumable by `InputSnapshotMetadata` for feature 008 in `SubscriptionManagementIngestionRun.cs` and blob manifest
- [ ] T087 [P] Add XML doc comments on public ingestion interfaces and normalizer per constitution Principle I
- [ ] T088 Run full `quickstart.md` validation scenarios (V1–V6) and document pass/fail in `specs/009-giacom-subscription-csv/quickstart.md` checklist section
- [ ] T089 Run `dotnet clean`, `dotnet restore`, `dotnet build --no-restore`, `dotnet test --no-build` from solution root per workspace build-quality rules
- [ ] T090 Verify no `new BlobServiceClient(connectionString)` or `new TableServiceClient(connectionString)` introduced — Aspire DI only (grep audit)

**Checkpoint**: All tests pass; quickstart validated; storage constraints verified.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **User Stories (Phases 3–7)**: Depend on Foundational; recommended order US1 → US2 → US3 → US4 → US5 (US2 can parallel US3 after US1 mapper exists)
- **Azure Persistence (Phase 8)**: Depends on US1 ingester minimum; full value after US3 normalizer
- **Polish (Phase 9)**: Depends on Phases 3–8

### User Story Dependencies

| Story | Depends on | Independent test fixture |
|-------|------------|--------------------------|
| US1 (P1) | Foundational | `subscription-management-sample-a.csv` |
| US2 (P1) | US1 ingester skeleton | `mixed-products.csv` |
| US3 (P1) | US1 raw rows | Casing variant rows in sample fixture |
| US4 (P2) | US3 normalizer | `lifecycle-columns.csv` |
| US5 (P2) | US1 pipeline | `partial-success.csv`, `column-variant.csv` |

### Parallel Opportunities

- **Phase 1**: T002, T003 in parallel
- **Phase 2**: T006–T007, T010–T013, T016–T021, T026–T027 in parallel after T005–T009
- **Phase 3**: T030–T031 in parallel
- **Phase 4**: T043–T044, T045 in parallel
- **Phase 5**: T050–T051 in parallel
- **Phase 6**: T059–T061 in parallel
- **Phase 7**: T066–T068 in parallel
- **Phase 8**: T074–T077, T078–T079 in parallel (stores independent)
- **Phase 9**: T086–T087 in parallel

---

## Parallel Example: User Story 1

```bash
# Tests first (fail before implementation):
T030: SubscriptionManagementCsvIngesterTests.cs skeleton
T031: Multi-customer assertion test

# Then sequential mapper → orchestrator → DI → golden file
```

---

## Parallel Example: Foundational Domain Types

```bash
# Launch together:
T006: ProductDisplayFacts.cs
T007: SubscriptionLifecycleFacts.cs
T010: SubscriptionManagementCsvIngestionOptions.cs
T011: SubscriptionManagementCsvIngestionRequest.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Golden-file test + manual CSV ingest via test helper
5. Demo subscription truth output without Azure upload

### Incremental Delivery

1. Setup + Foundational → contracts ready
2. US1 → core CSV → subscription truth (MVP)
3. US2 → M365 scope filter
4. US3 → production-grade normalization
5. US4 → lifecycle/pricing columns
6. US5 → partial success + format tolerance
7. Phase 8 → Azure persist + API upload
8. Phase 9 → polish + 008 metadata wiring

### Suggested MVP Scope

**Phases 1–3** (T001–T042): Parser emits `MicrosoftSubscriptionLine` from sample CSV without Azure upload. Adds reconciliation-ready subscription truth for local/testing workflows.

**Production MVP**: Add **Phase 8** (T074–T085) for operator upload via API.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete work in same phase
- [Story] label maps task to spec user story for traceability
- Header aliases in contract are **provisional** until T004 production CSVs validate them (T073)
- Constitution VI: no extra interfaces beyond plan; `ISubscriptionManagementNormalizer` already stubbed in 001
- Storage: **Blob + Table only**; never introduce SQL for ingestion index or payloads

---

## Task Summary

| Phase | Tasks | Story |
|-------|-------|-------|
| 1 Setup | T001–T004 (4) | — |
| 2 Foundational | T005–T029 (25) | — |
| 3 US1 | T030–T042 (13) | US1 |
| 4 US2 | T043–T049 (7) | US2 |
| 5 US3 | T050–T058 (9) | US3 |
| 6 US4 | T059–T065 (7) | US4 |
| 7 US5 | T066–T073 (8) | US5 |
| 8 Azure + API | T074–T085 (12) | — |
| 9 Polish | T086–T090 (5) | — |
| **Total** | **90 tasks** | |

**Format validation**: All tasks use `- [ ] [TaskID] [P?] [Story?] Description with file path` format.
