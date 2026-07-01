# Tasks: Stripe Billing CSV Ingestion

**Input**: Design documents from `/specs/003-stripe-csv-ingestion/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md; `001-billing-domain-model` complete (`RawStripe*` types, `ImportSourceKind.StripeExport`); `002-giacom-pdf-ingestion` complete (shared `IngestionLogEntry`, `GoldenFileComparer`)

**UI note**: This feature is **backend-only** — no Blazor upload UI, API endpoints, or Fluent UI work is in scope (see plan.md Out of Scope). `BillDrift.Web` still uses the Aspire starter Bootstrap shell with **no Fluent UI Blazor setup**. When a future upload UI feature is specified, migrate to Fluent UI Blazor v5 per `.cursor/skills/fluentui-blazor-usage/SKILL.md` (`Microsoft.FluentUI.AspNetCore.Components`, `AddFluentUIComponents()`, `<FluentProviders />`, stylesheet in `App.razor`) — refactor the skeleton starter app before adding ingestion UI.

**Tests**: Included per constitution Principle II, `quickstart.md` validation scenarios, and `contracts/csv-ingestion-pipeline.md`.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US5) for story-phase tasks only

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add CSV parser dependency, fixture layout, and package references.

- [ ] T001 Add `CsvHelper` package reference to `src/BillDrift.Infrastructure/BillDrift.Infrastructure.csproj` via central package management
- [ ] T002 [P] Create `tests/fixtures/stripe-csv/` and `tests/fixtures/stripe-csv/expected/` directory structure
- [ ] T003 [P] Add `tests/fixtures/stripe-csv/README.md` documenting required fixtures per `quickstart.md` and commit policy for sanitized CSV exports
- [ ] T004 Obtain and place sanitized CSV fixtures (minimum: `subscriptions-sample-a.csv`, `products-sample-a.csv`, `prices-sample-a.csv`, `subscriptions-column-variant.csv`, `subscriptions-mixed-status.csv`, `subscriptions-partial-metadata.csv`, `subscriptions-partial-success.csv`) under `tests/fixtures/stripe-csv/`

**Checkpoint**: Solution builds with CsvHelper; fixture directories ready (CSVs may arrive in parallel with parser work).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Application contract types, domain extensions, internal parse DTOs, and shared helpers. MUST complete before user story implementation.

**⚠️ CRITICAL**: No user story work until this phase is complete.

- [ ] T005 Extend `IngestionFailureReason` enum with Stripe CSV codes (`MandatoryHeaderMissing`, `AmountUnparseable`, `StripeIdMissing`, `MetadataIncomplete`, `MetadataInconsistent`, `CatalogueReferenceUnresolved`, `EmptyFile`) in `src/BillDrift.Application/Import/IngestionEnums.cs` per `data-model.md`
- [ ] T006 [P] Implement `StripeCsvFileKind` enum in `src/BillDrift.Application/Import/StripeCsvFileKind.cs`
- [ ] T007 [P] Implement `StripeCsvIngestionOptions` record in `src/BillDrift.Application/Import/StripeCsvIngestionOptions.cs` per `data-model.md`
- [ ] T008 [P] Implement `StripeCsvFileInput` record in `src/BillDrift.Application/Import/StripeCsvFileInput.cs`
- [ ] T009 [P] Implement `StripeCsvIngestionRequest` record in `src/BillDrift.Application/Import/StripeCsvIngestionRequest.cs`
- [ ] T010 [P] Implement `StripeCsvSourceFileInfo` record in `src/BillDrift.Application/Import/StripeCsvSourceFileInfo.cs`
- [ ] T011 [P] Implement `StripeCsvIngestionSummary` record in `src/BillDrift.Application/Import/StripeCsvIngestionSummary.cs`
- [ ] T012 Implement `StripeCsvIngestionResult` record in `src/BillDrift.Application/Import/StripeCsvIngestionResult.cs` with validation rules from `data-model.md`
- [ ] T013 Implement `IStripeBillingCsvIngester` interface in `src/BillDrift.Application/Import/IStripeBillingCsvIngester.cs` per `contracts/csv-ingestion-pipeline.md`
- [ ] T014 Extend `RawStripeSubscriptionItem` with `Id`, `CustomerId`, `ProductName`, `SubscriptionStatus`, `UnitAmountRaw`, `IntervalRaw`, `SourceRowNumber` in `src/BillDrift.Domain/Import/Stripe/RawStripeSubscriptionItem.cs`
- [ ] T015 [P] Extend `RawStripeProduct` with `Id` and `SourceRowNumber` in `src/BillDrift.Domain/Import/Stripe/RawStripeProduct.cs`
- [ ] T016 [P] Extend `RawStripePrice` with `Id`, `Description`, and `SourceRowNumber` in `src/BillDrift.Domain/Import/Stripe/RawStripePrice.cs`
- [ ] T017 [P] Implement `ParsedSubscriptionRow` in `src/BillDrift.Infrastructure/Import/Stripe/Internal/ParsedSubscriptionRow.cs`
- [ ] T018 [P] Implement `ParsedProductRow` in `src/BillDrift.Infrastructure/Import/Stripe/Internal/ParsedProductRow.cs`
- [ ] T019 [P] Implement `ParsedPriceRow` in `src/BillDrift.Infrastructure/Import/Stripe/Internal/ParsedPriceRow.cs`
- [ ] T020 Implement SHA-256 per-file hash and bundle ID helper in `src/BillDrift.Infrastructure/Import/Stripe/StripeFileIdentity.cs` per research R2
- [ ] T021 Implement intake limits (`MaxFileSizeBytes` default 10 MB) in `src/BillDrift.Infrastructure/Import/Stripe/StripeIngestionLimits.cs`
- [ ] T022 Implement `StripeCsvHeaderMap` column alias registry in `src/BillDrift.Infrastructure/Import/Stripe/StripeCsvHeaderMap.cs` per `contracts/stripe-csv-header-map.md`
- [ ] T023 Implement `StripeCsvRowReader` CsvHelper wrapper in `src/BillDrift.Infrastructure/Import/Stripe/StripeCsvRowReader.cs`
- [ ] T024 Create `StripeImportServiceCollectionExtensions` skeleton in `src/BillDrift.Infrastructure/Import/Stripe/StripeImportServiceCollectionExtensions.cs`
- [ ] T025 Reuse or extract shared `GoldenFileComparer` for Stripe tests in `tests/BillDrift.Infrastructure.Tests/Import/Stripe/GoldenFileComparer.cs` (reference Giacom implementation if not yet shared)

**Checkpoint**: Application contract compiles; domain raw types extended; internal parse types and header map available.

---

## Phase 3: User Story 1 — Import Customer Billing State from Subscriptions Export (Priority: P1) 🎯 MVP

**Goal**: Submit a Stripe subscriptions CSV and receive one `RawStripeSubscriptionItem` per subscription item row with customer, subscription, product/price IDs, quantity, interval, amount, and status.

**Independent Test**: Given `subscriptions-sample-a.csv`, pipeline returns subscription items with non-empty `CustomerId`, `SubscriptionId`, `ProductId`, `PriceId`, parsed `Quantity`, and `SubscriptionStatus`; multi-item subscriptions emit multiple rows.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T026 [P] [US1] Create integration test skeleton in `tests/BillDrift.Infrastructure.Tests/Import/Stripe/StripeBillingCsvIngesterTests.cs` with subscriptions-only ingest hooks
- [ ] T027 [P] [US1] Add multi-item subscription assertion test in `StripeBillingCsvIngesterTests.cs` per spec acceptance scenario 1

### Implementation for User Story 1

- [ ] T028 [US1] Implement `SubscriptionsCsvParser` in `src/BillDrift.Infrastructure/Import/Stripe/SubscriptionsCsvParser.cs` mapping headers via `StripeCsvHeaderMap`
- [ ] T029 [US1] Implement customer and subscription deduplication assembly in `src/BillDrift.Infrastructure/Import/Stripe/RawStripeRecordMapper.cs` (customers + subscriptions from subscription rows)
- [ ] T030 [US1] Implement subscription item mapping with quantity/amount/interval parsing in `RawStripeRecordMapper.cs`
- [ ] T031 [US1] Implement `RawImportId.Create(ImportSourceKind.StripeExport, sourceDocumentId, lineKey)` for subscription items in `RawStripeRecordMapper.cs` per research R7
- [ ] T032 [US1] Implement `StripeBillingCsvIngester` orchestrator skeleton (subscriptions-only path) in `src/BillDrift.Infrastructure/Import/Stripe/StripeBillingCsvIngester.cs` per `contracts/csv-ingestion-pipeline.md`
- [ ] T033 [US1] Complete subscriptions-only ingest path with result assembly and summary counts in `StripeBillingCsvIngester.cs`
- [ ] T034 [US1] Complete `AddStripeBillingCsvIngestion` DI registration (`Singleton<IStripeBillingCsvIngester>`) in `StripeImportServiceCollectionExtensions.cs`
- [ ] T035 [US1] Generate golden JSON `tests/fixtures/stripe-csv/expected/subscriptions-sample-a.json` from validated subscriptions-only output
- [ ] T036 [US1] Complete subscriptions golden-file assertions in `StripeBillingCsvIngesterTests.cs`

**Checkpoint**: MVP ingests subscriptions CSV independently; golden test passes; multi-item rows emitted.

---

## Phase 4: User Story 2 — Import Stripe Product and Price Catalogue (Priority: P1)

**Goal**: Ingest products and prices CSV exports; emit `RawStripeProduct` and `RawStripePrice` collections joinable by subscription item product/price IDs.

**Independent Test**: Given full fixture bundle (`subscriptions-sample-a.csv`, `products-sample-a.csv`, `prices-sample-a.csv`), every product ID and price ID referenced by subscription items resolves to catalogue output records.

### Tests for User Story 2

- [ ] T037 [P] [US2] Add full-bundle golden-file test in `StripeBillingCsvIngesterTests.cs` per `quickstart.md` Scenario 1
- [ ] T038 [P] [US2] Add catalogue resolution assertion (100% referenced IDs found) in `StripeBillingCsvIngesterTests.cs` per SC-007

### Implementation for User Story 2

- [ ] T039 [P] [US2] Implement `ProductsCsvParser` in `src/BillDrift.Infrastructure/Import/Stripe/ProductsCsvParser.cs`
- [ ] T040 [P] [US2] Implement `PricesCsvParser` in `src/BillDrift.Infrastructure/Import/Stripe/PricesCsvParser.cs`
- [ ] T041 [US2] Extend `RawStripeRecordMapper` with product and price mapping + `RawImportId` assignment in `RawStripeRecordMapper.cs`
- [ ] T042 [US2] Integrate catalogue assembly stages into `StripeBillingCsvIngester.cs` (products + prices file kinds)
- [ ] T043 [US2] Generate golden JSON `tests/fixtures/stripe-csv/expected/bundle-sample-a.json` for full three-file bundle
- [ ] T044 [US2] Preserve unknown metadata keys on product and price records in parsers per FR-013

**Checkpoint**: Full bundle ingest passes; catalogue collections populated; SC-007 resolution test passes.

---

## Phase 5: User Story 3 — Filter Subscriptions by Status (Priority: P2)

**Goal**: Include active subscriptions by default (`active`, `trialing`, `past_due`); optional inclusion of inactive statuses for diagnostics with summary counts.

**Independent Test**: Given `subscriptions-mixed-status.csv`, default ingest excludes canceled rows and reports `SubscriptionsFilteredByStatus`; inclusive option returns all rows with status preserved.

### Tests for User Story 3

- [ ] T045 [P] [US3] Create `StripeStatusFilterTests` in `tests/BillDrift.Infrastructure.Tests/Import/Stripe/StripeStatusFilterTests.cs`
- [ ] T046 [P] [US3] Add mixed-status filter tests (default vs `IncludeInactiveSubscriptions`) in `StripeBillingCsvIngesterTests.cs` per `quickstart.md` Scenario 2

### Implementation for User Story 3

- [ ] T047 [US3] Implement `StripeStatusFilter` with active/inactive status sets in `src/BillDrift.Infrastructure/Import/Stripe/StripeStatusFilter.cs` per research R6
- [ ] T048 [US3] Integrate status filtering stage into pipeline after subscription assembly in `StripeBillingCsvIngester.cs`
- [ ] T049 [US3] Populate `StripeCsvIngestionSummary.SubscriptionsFilteredByStatus` without treating exclusions as errors in `StripeBillingCsvIngester.cs`

**Checkpoint**: SC-005 filter tests pass; canceled excluded by default; diagnostic mode includes inactive rows.

---

## Phase 6: User Story 4 — Parse and Flag Mapping Metadata (Priority: P2)

**Goal**: Extract Mex ID, offer ID, SKU ID, and supplier references from metadata columns; warn on missing/inconsistent metadata without blocking row emission.

**Independent Test**: Given `subscriptions-partial-metadata.csv`, rows with full metadata parse correctly; rows with gaps emit warnings (`MetadataIncomplete`/`MetadataInconsistent`) and no invented identifiers.

### Tests for User Story 4

- [ ] T050 [P] [US4] Create `StripeMetadataParserTests` in `tests/BillDrift.Infrastructure.Tests/Import/Stripe/StripeMetadataParserTests.cs` for key alias and bracket-column patterns
- [ ] T051 [P] [US4] Add metadata gap warning tests in `StripeBillingCsvIngesterTests.cs` per `quickstart.md` Scenario 3

### Implementation for User Story 4

- [ ] T052 [US4] Implement `StripeMetadataParser` (bracket columns + flat keys + supplier ref prefixes) in `src/BillDrift.Infrastructure/Import/Stripe/StripeMetadataParser.cs` per research R5 and `contracts/stripe-csv-header-map.md`
- [ ] T053 [US4] Integrate metadata parsing into `SubscriptionsCsvParser.cs` and attach full dictionary to parsed rows
- [ ] T054 [US4] Implement metadata gap/inconsistency detection and warning log emission in `StripeBillingCsvIngester.cs` per FR-024/FR-025
- [ ] T055 [US4] Implement Mex/Offer/SKU trim and casing normalization while preserving raw values for traceability in `RawStripeRecordMapper.cs`
- [ ] T056 [US4] Increment `Summary.MetadataWarnings` in result assembly per `data-model.md`

**Checkpoint**: Metadata parser tests pass; SC-006 identifiable from log summaries; no invented mapping keys.

---

## Phase 7: User Story 5 — Tolerate CSV Format Variation and Partial Row Failures (Priority: P2)

**Goal**: Header alias matching tolerates column reorder/rename; individual bad rows skipped with structured logs; partial success outcome when valid siblings exist.

**Independent Test**: Given `subscriptions-column-variant.csv` and `subscriptions-partial-success.csv`, valid rows import with header detection; skipped row appears in log with reason and location; variant columns still map mandatory fields.

### Tests for User Story 5

- [ ] T057 [P] [US5] Add column-variant fixture test in `StripeBillingCsvIngesterTests.cs` per `quickstart.md` and `contracts/stripe-csv-header-map.md`
- [ ] T058 [P] [US5] Add partial-success fixture test expecting `PartialSuccess` and skip log entry in `StripeBillingCsvIngesterTests.cs` per SC-003
- [ ] T059 [P] [US5] Add determinism re-import test (identical `RawImportId` and bundle output) in `StripeBillingCsvIngesterTests.cs` per SC-004

### Implementation for User Story 5

- [ ] T060 [US5] Implement row-level skip handling (unparseable quantity/amount, missing Stripe IDs) with `IngestionLogEntry` location convention (file kind + row number) in `StripeBillingCsvIngester.cs`
- [ ] T061 [US5] Implement file-level failure for mandatory header missing and empty data rows in `StripeCsvRowReader.cs` / orchestrator intake
- [ ] T062 [US5] Implement `CatalogueReferenceUnresolved` warning when bundle includes catalogue files and item references unknown product/price ID in `StripeBillingCsvIngester.cs`
- [ ] T063 [US5] Suppress catalogue warnings when products/prices files not supplied (subscriptions-only mode) per FR-005
- [ ] T064 [US5] Implement `IngestionOutcomeStatus` aggregation (Success / PartialSuccess / Failure) and per-file summary in result assembly per `data-model.md`
- [ ] T065 [US5] Cap log snippets at 200 characters and avoid logging full customer email in `StripeBillingCsvIngester.cs` per security contract

**Checkpoint**: Column-variant and partial-success fixtures pass; determinism test passes; no silent data drops (FR-035).

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: DI wiring, performance validation, comment verification, and quickstart sign-off.

- [ ] T066 [P] Register `AddStripeBillingCsvIngestion()` in `src/BillDrift.Api/Program.cs` for future API/upload integration (no endpoint in this feature)
- [ ] T067 Add required code comments on public interfaces, pipeline orchestrator, metadata parser, and status filter per constitution Principle I
- [ ] T068 Run `dotnet build BillDrift.sln --configuration Release` and `dotnet test tests/BillDrift.Infrastructure.Tests --filter "FullyQualifiedName~Stripe" --configuration Release --verbosity normal`
- [ ] T069 Execute all manual validation scenarios in `specs/003-stripe-csv-ingestion/quickstart.md` and record pass/fail notes
- [ ] T070 [P] Add performance smoke test asserting 1,000-row synthetic bundle completes in <60s in `tests/BillDrift.Infrastructure.Tests/Import/Stripe/StripeBillingCsvIngesterPerformanceTests.cs` per SC-001

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **User Stories (Phase 3–7)**: All depend on Foundational completion
  - US1 (Phase 3) is MVP — subscriptions-only ingest
  - US2 (Phase 4) adds catalogue parsers — builds on orchestrator from US1
  - US3–US5 extend orchestrator (filter, metadata, error tiers) — sequential recommended after US1 core
- **Polish (Phase 8)**: Depends on US1–US5 completion

### User Story Dependencies

| Story | Priority | Depends on | Independent test |
|-------|----------|------------|------------------|
| US1 | P1 | Foundational | Subscriptions CSV → subscription items + customers |
| US2 | P1 | US1 orchestrator | Full bundle → resolvable catalogue IDs |
| US3 | P2 | US1 subscription assembly | Mixed-status filter default vs inclusive |
| US4 | P2 | US1 subscription parsing | Metadata full/partial/absent + warnings |
| US5 | P2 | US1–US2 parsers | Column variant + partial success + determinism |

### Within Each User Story

- Tests written first (fail before implementation)
- Parsers before mapper; mapper before orchestrator integration
- Story checkpoint before next priority

### Parallel Opportunities

- **Phase 1**: T002, T003 in parallel
- **Phase 2**: T006–T011, T015–T019 all parallelizable; T014–T016 parallel after T005
- **Phase 3**: T026–T027 (tests) in parallel
- **Phase 4**: T037–T038 (tests), T039–T040 (parsers) in parallel
- **Phase 5**: T045–T046 (tests) in parallel
- **Phase 6**: T050–T051 (tests) in parallel; T052 before T053–T056
- **Phase 7**: T057–T059 (tests) in parallel
- **Phase 8**: T066, T070 in parallel

---

## Parallel Example: User Story 1

```bash
# Launch US1 tests together (after T025):
Task T026: "Create StripeBillingCsvIngesterTests.cs skeleton"
Task T027: "Add multi-item subscription assertion test"

# Then sequentially: T028 → T029 → T030 → T031 → T032 → T033 → T034 → T035 → T036
```

---

## Parallel Example: User Story 2

```bash
# After US1 orchestrator skeleton (T032):
Task T039: "Implement ProductsCsvParser.cs"
Task T040: "Implement PricesCsvParser.cs"
# Then: T041 → T042 → T043 → T044
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Subscriptions golden test passes; multi-item rows correct
5. Demo via test runner — no UI required

### Incremental Delivery

1. Setup + Foundational → contract and types ready
2. US1 → subscriptions CSV ingestion (MVP)
3. US2 → full catalogue bundle
4. US3 → status filtering for reconciliation focus
5. US4 → metadata visibility for mapping
6. US5 → format drift + partial failure resilience
7. Polish → quickstart sign-off

### Parallel Team Strategy

With multiple developers after Foundational:

- Developer A: US1 subscriptions pipeline (T028–T036)
- Developer B: US2 catalogue parsers (after T032 orchestrator skeleton)
- Developer C: US4 metadata parser (T052) in parallel with US2 once T028 parser pattern exists

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to user story for traceability
- CSV fixtures (T004) may block golden tests but not type/contract work
- **No Fluent UI tasks** in this feature — UI deferred to future upload feature; when planned, follow `.cursor/skills/fluentui-blazor-usage/SKILL.md` to refactor `BillDrift.Web` from Bootstrap skeleton to Fluent UI Blazor v5 before building ingestion screens
- Ingester MUST NOT throw for parse failures — return `StripeCsvIngestionResult` with appropriate `Status` (only `ArgumentNullException` / `OperationCanceledException` per contract)
- Normalization via `IStripeBillingNormalizer` is **not** implemented in this feature
- Commit after each task or logical group
