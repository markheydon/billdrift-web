# Tasks: Giacom Supplier Billing PDF Ingestion

**Input**: Design documents from `/specs/002-giacom-pdf-ingestion/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md; `001-billing-domain-model` complete (`RawGiacomBillingLine`, `RawImportId`)

**UI note**: This feature is **backend-only** — no Blazor upload UI, API endpoints, or Fluent UI work is in scope (see plan.md Out of Scope). `BillDrift.Web` still uses the Aspire starter Bootstrap shell. When a future upload UI feature is specified, migrate to Fluent UI Blazor v5 per `.cursor/skills/fluentui-blazor-usage/` (NuGet package, `AddFluentUIComponents()`, `<FluentProviders />`, stylesheet in `App.razor`).

**Tests**: Included per constitution Principle II, `quickstart.md` validation scenarios, and `contracts/pdf-ingestion-pipeline.md` test contract.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US4) for story-phase tasks only

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add parser dependencies, test project, and PDF fixture layout.

- [X] T001 Add `UglyToad.PdfPig` package reference to `src/BillDrift.Infrastructure/BillDrift.Infrastructure.csproj`
- [X] T002 Create `tests/BillDrift.Infrastructure.Tests/BillDrift.Infrastructure.Tests.csproj` with xUnit + FluentAssertions referencing `BillDrift.Infrastructure`
- [X] T003 Add `tests/BillDrift.Infrastructure.Tests` to `BillDrift.slnx`
- [X] T004 [P] Create `tests/fixtures/giacom-pdf/` and `tests/fixtures/giacom-pdf/expected/` directory structure
- [X] T005 [P] Add `tests/fixtures/giacom-pdf/README.md` documenting required fixtures per `quickstart.md` and commit policy for sanitized PDFs
- [X] T006 Obtain and place sanitized PDF fixtures (minimum: `pre-billing-sample-a.pdf`, `pre-billing-sample-b.pdf`, `post-billing-sample-a.pdf`, `post-billing-sample-b.pdf`, `partial-success-sample.pdf`, `encrypted-sample.pdf`) under `tests/fixtures/giacom-pdf/`

**Checkpoint**: Solution builds; test project exists; fixture directories ready (PDFs may arrive in parallel with parser work).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Application contract types, internal parse types, and shared helpers. MUST complete before user story implementation.

**⚠️ CRITICAL**: No user story work until this phase is complete.

- [X] T007 [P] Implement `GiacomReportType` enum in `src/BillDrift.Application/Import/GiacomReportType.cs` per `data-model.md`
- [X] T008 [P] Implement `IngestionOutcomeStatus`, `IngestionLogSeverity`, and `IngestionFailureReason` enums in `src/BillDrift.Application/Import/IngestionEnums.cs`
- [X] T009 [P] Implement `IngestionLocation` record in `src/BillDrift.Application/Import/IngestionLocation.cs`
- [X] T010 [P] Implement `IngestionLogEntry` record in `src/BillDrift.Application/Import/IngestionLogEntry.cs`
- [X] T011 [P] Implement `GiacomPdfIngestionSummary` record in `src/BillDrift.Application/Import/GiacomPdfIngestionSummary.cs`
- [X] T012 Implement `GiacomPdfIngestionResult` record in `src/BillDrift.Application/Import/GiacomPdfIngestionResult.cs` with validation rules from `data-model.md`
- [X] T013 Implement `IGiacomBillingPdfIngester` interface in `src/BillDrift.Application/Import/IGiacomBillingPdfIngester.cs` per `contracts/pdf-ingestion-pipeline.md`
- [X] T014 [P] Implement `PdfWord` and `PdfTextLine` in `src/BillDrift.Infrastructure/Import/Giacom/Internal/PdfWord.cs` and `PdfTextLine.cs`
- [X] T015 [P] Implement `ColumnDefinition` in `src/BillDrift.Infrastructure/Import/Giacom/Internal/ColumnDefinition.cs`
- [X] T016 [P] Implement `CustomerBlock` in `src/BillDrift.Infrastructure/Import/Giacom/Internal/CustomerBlock.cs`
- [X] T017 [P] Implement `ParsedProductLine` in `src/BillDrift.Infrastructure/Import/Giacom/Internal/ParsedProductLine.cs`
- [X] T018 Implement intake limits (`MaxFileSizeBytes`, `MaxPageCount`, `MaxLogSnippetLength`) in `src/BillDrift.Infrastructure/Import/Giacom/GiacomIngestionLimits.cs` per `data-model.md`
- [X] T019 Implement SHA-256 `SourceDocumentId` helper in `src/BillDrift.Infrastructure/Import/Giacom/DocumentIdentity.cs` per research R2
- [X] T020 Create `GiacomImportServiceCollectionExtensions` skeleton in `src/BillDrift.Infrastructure/Import/Giacom/GiacomImportServiceCollectionExtensions.cs`
- [X] T021 Create `GoldenFileComparer` helper in `tests/BillDrift.Infrastructure.Tests/Import/Giacom/GoldenFileComparer.cs` for field-level golden JSON comparison

**Checkpoint**: Application contract compiles; internal parse types available; test helper ready.

---

## Phase 3: User Story 1 — Import Monthly Supplier Billing PDF (Priority: P1) 🎯 MVP

**Goal**: Submit a Giacom pre-billing or post-billing PDF and receive structured `RawGiacomBillingLine` records for every parseable charge row, with correct customer/Mex ID association and report type detection.

**Independent Test**: Given `pre-billing-sample-a.pdf` or `post-billing-sample-a.pdf`, pipeline returns lines with non-empty `MexIdRaw`, `ProductNameRaw`, `QuantityRaw`, `LineCostRaw`, and `ReportType` of `PreBilling` or `PostBilling` respectively.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T022 [P] [US1] Create integration test skeleton in `tests/BillDrift.Infrastructure.Tests/Import/Giacom/GiacomBillingPdfIngesterTests.cs` with golden-file comparison hooks
- [X] T023 [P] [US1] Create `ReportClassifierTests` in `tests/BillDrift.Infrastructure.Tests/Import/Giacom/ReportClassifierTests.cs` for pre/post marker detection
- [X] T024 [P] [US1] Create `BlockSegmenterTests` in `tests/BillDrift.Infrastructure.Tests/Import/Giacom/BlockSegmenterTests.cs` for Mex ID block boundary detection

### Implementation for User Story 1

- [X] T025 [P] [US1] Implement `PdfTextExtractor` (PdfPig word extraction + Y-cluster line grouping) in `src/BillDrift.Infrastructure/Import/Giacom/PdfTextExtractor.cs`
- [X] T026 [US1] Implement `ReportClassifier` (first-page text markers) in `src/BillDrift.Infrastructure/Import/Giacom/ReportClassifier.cs` per research R8
- [X] T027 [US1] Implement `CustomerBlockSegmenter` (Mex ID header patterns) in `src/BillDrift.Infrastructure/Import/Giacom/CustomerBlockSegmenter.cs` per `contracts/giacom-block-grammar.md`
- [X] T028 [US1] Implement `ProductLineParser` (basic column-to-field mapping) in `src/BillDrift.Infrastructure/Import/Giacom/ProductLineParser.cs`
- [X] T029 [US1] Implement `RawGiacomBillingLineMapper` (parsed row → domain line) in `src/BillDrift.Infrastructure/Import/Giacom/RawGiacomBillingLineMapper.cs`
- [X] T030 [US1] Implement `GiacomBillingPdfIngester` pipeline orchestrator (intake through output assembly) in `src/BillDrift.Infrastructure/Import/Giacom/GiacomBillingPdfIngester.cs` per `contracts/pdf-ingestion-pipeline.md`
- [X] T031 [US1] Complete `AddGiacomBillingPdfIngestion` DI registration (`Singleton<IGiacomBillingPdfIngester>`) in `GiacomImportServiceCollectionExtensions.cs`
- [X] T032 [US1] Generate golden JSON `tests/fixtures/giacom-pdf/expected/pre-billing-sample-a.json` from validated parser output
- [X] T033 [US1] Generate golden JSON `tests/fixtures/giacom-pdf/expected/post-billing-sample-a.json` from validated parser output
- [X] T034 [US1] Complete pre-billing and post-billing golden-file assertions in `GiacomBillingPdfIngesterTests.cs`

**Checkpoint**: MVP parser ingests standard pre/post PDFs; golden tests pass; `ReportType` correctly classified.

---

## Phase 4: User Story 2 — Handle Format Variation and Multi-Line Product Entries (Priority: P1)

**Goal**: Tolerate minor column/header drift across monthly PDFs; merge wrapped product names into single values; preserve distinct charge types for recurring vs pro-rated lines.

**Independent Test**: Given `pre-billing-sample-b.pdf` and `post-billing-sample-b.pdf` (format variants), pipeline extracts equivalent logical line counts and field values; wrapped product names appear as single concatenated `ProductNameRaw`.

### Tests for User Story 2

- [X] T035 [P] [US2] Add format-variant golden-file tests for `pre-billing-sample-b.pdf` and `post-billing-sample-b.pdf` in `GiacomBillingPdfIngesterTests.cs`
- [X] T036 [P] [US2] Add wrapped product name assertion test per `quickstart.md` Scenario 3 in `GiacomBillingPdfIngesterTests.cs`

### Implementation for User Story 2

- [X] T037 [US2] Implement `ColumnDetector` (header-anchored column X-ranges with drift tolerance) in `src/BillDrift.Infrastructure/Import/Giacom/ColumnDetector.cs` per `contracts/giacom-block-grammar.md`
- [X] T038 [US2] Integrate `ColumnDetector` into `ProductLineParser` in `src/BillDrift.Infrastructure/Import/Giacom/ProductLineParser.cs`
- [X] T039 [US2] Implement `ProductNameMerger` (continuation row detection and merge) in `src/BillDrift.Infrastructure/Import/Giacom/ProductNameMerger.cs` per research R5
- [X] T040 [US2] Integrate `ProductNameMerger` into pipeline before mapping in `GiacomBillingPdfIngester.cs`
- [X] T041 [US2] Add charge type raw text preservation (Recurring, Pro-rated adjustment variants) in `ProductLineParser.cs` per `contracts/giacom-block-grammar.md`
- [X] T042 [US2] Generate golden JSON for format-variant fixtures in `tests/fixtures/giacom-pdf/expected/pre-billing-sample-b.json` and `post-billing-sample-b.json`

**Checkpoint**: Format-variant fixtures pass; wrapped names merged; charge types not merged across distinct rows.

---

## Phase 5: User Story 3 — Recover from Partial Extraction Failures (Priority: P2)

**Goal**: Continue ingestion when individual lines or blocks fail; emit structured skip/warning logs; summarize outcome as Success, PartialSuccess, or Failure.

**Independent Test**: Given `partial-success-sample.pdf` with one corrupt line, result is `PartialSuccess` with valid lines emitted and skip log entry containing `QuantityUnparseable` and location; encrypted PDF returns `Failure` with no lines.

### Tests for User Story 3

- [X] T043 [P] [US3] Add partial-success fixture test in `tests/BillDrift.Infrastructure.Tests/Import/Giacom/GiacomBillingPdfIngesterTests.cs` per `quickstart.md` Scenario 4
- [X] T044 [P] [US3] Add encrypted PDF rejection test in `GiacomBillingPdfIngesterTests.cs` expecting `IngestionFailureReason.DocumentEncrypted`

### Implementation for User Story 3

- [X] T045 [US3] Implement three-tier error handling (line skip, block skip, document fail) in `GiacomBillingPdfIngester.cs` per research R7 and `contracts/giacom-block-grammar.md` skip matrix
- [X] T046 [US3] Implement `IngestionLogEntry` emission with page/block/line location, reason code, and 200-char capped `RawSnippet` in `GiacomBillingPdfIngester.cs`
- [X] T047 [US3] Implement `IngestionOutcomeStatus` aggregation and `GiacomPdfIngestionSummary` counts in result assembly per `data-model.md` state transitions
- [X] T048 [US3] Implement document-level failure paths (encrypted, unreadable, page/size limits, zero blocks) in intake and extraction stages
- [X] T049 [US3] Implement period-unparseable warning path (emit line with null period fields + warning log, do not skip) in `RawGiacomBillingLineMapper.cs` per FR-017
- [X] T050 [US3] Handle zero-line valid document edge case (cover sheet) with `Success` status and informational log per spec edge cases

**Checkpoint**: Partial-success and encrypted fixtures pass; no silent data drops (FR-023).

---

## Phase 6: User Story 4 — Prepare Lines for Downstream Product Mapping (Priority: P2)

**Goal**: Emit domain-ready lines with stable idempotency keys, trim-only Mex ID normalization, character-preserving product names, and no premature Offer/SKU assignment.

**Independent Test**: Re-parsing same PDF twice yields identical `RawImportId` values; product names match PDF text character-for-character; lines include supplier reference IDs when present; no canonical product keys invented.

### Tests for User Story 4

- [X] T051 [P] [US4] Add determinism re-parse test (deep-equal lines and IDs) in `GiacomBillingPdfIngesterTests.cs` per `quickstart.md` Scenario 5
- [X] T052 [P] [US4] Add assertion that output lines contain no Offer/SKU/CSP fields and preserve raw product names in `GiacomBillingPdfIngesterTests.cs`

### Implementation for User Story 4

- [X] T053 [US4] Implement `LineKeyResolver` (supplier reference first, `{page}:{blockIndex}:{lineIndex}` fallback) in `src/BillDrift.Infrastructure/Import/Giacom/LineKeyResolver.cs` per research R6
- [X] T054 [US4] Wire `RawImportId.Create(ImportSourceKind.GiacomBillingPdf, sourceDocumentId, lineKey)` in `RawGiacomBillingLineMapper.cs`
- [X] T055 [US4] Implement Mex ID trim-only normalization (preserve casing in `MexIdRaw`) in `RawGiacomBillingLineMapper.cs` per research R10
- [X] T056 [US4] Ensure `SourceDocumentId` and `ExtractedAt` populated consistently on every emitted line matching result metadata in `GiacomBillingPdfIngester.cs`

**Checkpoint**: Determinism test passes; line keys stable; handoff-ready for `IGiacomBillingNormalizer` (not implemented here).

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: DI wiring, performance validation, and quickstart sign-off.

- [X] T057 [P] Register `AddGiacomBillingPdfIngestion()` in `src/BillDrift.Api/Program.cs` for future API/upload integration (no endpoint in this feature)
- [X] T058 Run `dotnet build BillDrift.slnx --configuration Release` and `dotnet test tests/BillDrift.Infrastructure.Tests --configuration Release --verbosity normal`
- [X] T059 Execute all manual validation scenarios in `quickstart.md` and record pass/fail notes in feature notes
- [X] T060 [P] Add performance smoke test or benchmark asserting typical monthly PDF parses in <30s design target (500+ line fixture when available) in `tests/BillDrift.Infrastructure.Tests/Import/Giacom/`
- [X] T061 Document test-only `*.columns.json` sidecar calibration format in `tests/fixtures/giacom-pdf/README.md` per `contracts/giacom-block-grammar.md` Fixture Calibration section

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **User Stories (Phase 3–6)**: All depend on Foundational completion
  - US1 (Phase 3) is MVP — complete before US2–US4 for incremental delivery
  - US2 builds on US1 parser components (column detection, name merge)
  - US3 extends orchestrator error handling (can parallelize tests with US2 after US1 core exists)
  - US4 completes mapping/idempotency (depends on US1 mapper; determinism tests need stable parser)
- **Polish (Phase 7)**: Depends on US1–US4 completion

### User Story Dependencies

| Story | Priority | Depends on | Independent test |
|-------|----------|------------|------------------|
| US1 | P1 | Foundational | Pre/post golden PDF → structured lines |
| US2 | P1 | US1 core pipeline | Format-variant + wrapped name fixtures |
| US3 | P2 | US1 core pipeline | Partial-success + encrypted fixtures |
| US4 | P2 | US1 mapper | Determinism + downstream handoff fields |

### Within Each User Story

- Tests written first (fail before implementation)
- Extractors/segmenters before orchestrator integration
- Orchestrator stages before golden JSON generation
- Story checkpoint before next priority

### Parallel Opportunities

- **Phase 1**: T004, T005 in parallel
- **Phase 2**: T007–T011, T014–T017 all parallelizable
- **Phase 3**: T022–T024 (tests), T025 (extractor) in parallel; T026–T028 sequential on parser chain
- **Phase 4**: T035–T036 (tests) parallel; T037–T039 parallel before integration tasks
- **Phase 5**: T043–T044 (tests) parallel
- **Phase 6**: T051–T052 (tests) parallel; T053–T055 parallel before T056
- **Phase 7**: T057, T060 parallel

---

## Parallel Example: User Story 1

```bash
# Launch US1 tests together (after T021):
Task T022: "Create GiacomBillingPdfIngesterTests.cs skeleton"
Task T023: "Create ReportClassifierTests.cs"
Task T024: "Create BlockSegmenterTests.cs"

# Launch independent parser components together (after T021):
Task T025: "Implement PdfTextExtractor.cs"
# Then sequentially: T026 → T027 → T028 → T029 → T030
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Golden tests for pre/post billing fixtures pass
5. Demo via test runner or CLI stub — no UI required

### Incremental Delivery

1. Setup + Foundational → contract and types ready
2. US1 → standard PDF ingestion (MVP)
3. US2 → format drift tolerance
4. US3 → partial failure visibility
5. US4 → idempotent downstream handoff
6. Polish → quickstart sign-off

### Parallel Team Strategy

With multiple developers after Foundational:

- Developer A: US1 core pipeline (T025–T034)
- Developer B: US2 column/name merge (after US1 T028 complete)
- Developer C: US3 error tiers (after US1 T030 skeleton exists)

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to user story for traceability
- PDF fixtures (T006) may block golden tests but not type/contract work
- **No Fluent UI tasks** in this feature — UI deferred to future upload feature; refer to `.cursor/skills/fluentui-blazor-usage/` when that feature is planned
- Parser MUST NOT throw for parse failures — return `GiacomPdfIngestionResult` with appropriate `Status` (only `ArgumentNullException` / `OperationCanceledException` per contract)
- Commit after each task or logical group
