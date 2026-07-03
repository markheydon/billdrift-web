# Tasks: Retail Pricing and Pricing Strategy Ingestion

**Input**: Design documents from `/specs/010-retail-pricing-ingestion/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md; `001-billing-domain-model` complete (`RawPriceListRow`, `RawManualPriceEntry`, `IntendedPrice`, `IPriceListNormalizer` stub, `IntendedPriceResolver`); `003-stripe-csv-ingestion` and `009-giacom-subscription-csv` complete (shared `IngestionLogEntry`, `GoldenFileComparer`, CsvHelper, `IIngestionBlobStore`, `IIngestionRunIndexStore`, Aspire DI storage pattern)

**UI note**: Blazor upload UI is **out of scope** — API endpoints only. `BillDrift.Web` calls API; no `BlobServiceClient` or `TableServiceClient` in Web project.

**Storage note**: Extend existing 009 ingestion stores for retail pricing — **Azure Blob + Table only, no SQL**, Aspire-injected clients only.

**Tests**: Included per constitution Principle II, `quickstart.md` validation scenarios, and `contracts/csv-ingestion-pipeline.md`.

**Organization**: Tasks grouped by user story for independent implementation and testing. Azure persistence + API in dedicated phase after parser stories.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US6) for story-phase tasks only

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Fixture layout and verify shared dependencies.

- [ ] T001 Verify `CsvHelper` package reference exists in `src/BillDrift.Infrastructure/BillDrift.Infrastructure.csproj` (reuse from 003/009; no duplicate add)
- [ ] T002 [P] Create `tests/fixtures/reseller-pricing/` and `tests/fixtures/reseller-pricing/expected/` directory structure
- [ ] T003 [P] Add `tests/fixtures/reseller-pricing/README.md` documenting required fixtures per `quickstart.md` and commit policy for sanitized CSV exports
- [ ] T004 Obtain and place sanitized CSV fixtures (minimum: `reseller-pricing-sample-a.csv`, `column-variant.csv`, `partial-bad-rows.csv`, `duplicate-keys.csv`, `end-of-sale.csv`) under `tests/fixtures/reseller-pricing/`

**Checkpoint**: Fixture directories ready; CsvHelper available; production CSVs may arrive in parallel with parser work.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain extensions, Application contracts, internal parse types, header map, and shared helpers. MUST complete before user story implementation.

**⚠️ CRITICAL**: No user story work until this phase is complete.

- [ ] T005 Extend `IngestionFailureReason` enum with retail-pricing codes (`TermUnparseable`, `FrequencyUnparseable`, `WholesaleUnparseable`, `RrpUnparseable`, `MissingCommercialKey`, `DuplicateCommercialKey`, `UnsupportedCurrency`, `ManualOverrideValidationFailed`, `PlatformUnrecognised`) in `src/BillDrift.Application/Import/IngestionEnums.cs` per `data-model.md`
- [ ] T006 [P] Add `Triennial` value to `Term` enum in `src/BillDrift.Domain/Common/Term.cs` per research R4
- [ ] T007 [P] Implement `PricingPlatform` enum in `src/BillDrift.Domain/Common/PricingPlatform.cs` per `data-model.md`
- [ ] T008 Extend `RawPriceListRow` with `PlatformRaw` and `CurrencyRaw` in `src/BillDrift.Domain/Import/RawPriceListRow.cs` per `data-model.md`
- [ ] T009 Extend `IntendedPrice` with `Platform` and `Classification` parameters in `src/BillDrift.Domain/Billing/IntendedPrice.cs` per `data-model.md`
- [ ] T010 [P] Implement `RetailPricingCsvIngestionOptions` record in `src/BillDrift.Application/Import/RetailPricingCsvIngestionOptions.cs` per `data-model.md`
- [ ] T011 [P] Implement `ManualPriceOverrideRequest` record in `src/BillDrift.Application/Import/ManualPriceOverrideRequest.cs` per `data-model.md`
- [ ] T012 [P] Implement `RetailPricingCsvIngestionRequest` record in `src/BillDrift.Application/Import/RetailPricingCsvIngestionRequest.cs`
- [ ] T013 [P] Implement `RetailPricingCsvIngestionSummary` record in `src/BillDrift.Application/Import/RetailPricingCsvIngestionSummary.cs`
- [ ] T014 [P] Implement `PricingResolutionDetail` record in `src/BillDrift.Application/Import/PricingResolutionDetail.cs`
- [ ] T015 Implement `RetailPricingCsvIngestionResult` record in `src/BillDrift.Application/Import/RetailPricingCsvIngestionResult.cs` per `data-model.md`
- [ ] T016 Implement `IResellerPricingCsvIngester` interface in `src/BillDrift.Application/Import/IResellerPricingCsvIngester.cs` per `contracts/csv-ingestion-pipeline.md`
- [ ] T017 [P] Implement `RetailPricingIngestionRun` record in `src/BillDrift.Application/Ingestion/RetailPricingIngestionRun.cs` per `data-model.md`
- [ ] T018 Extend `IIngestionBlobStore` with retail-pricing persist/load methods in `src/BillDrift.Application/Ingestion/IIngestionBlobStore.cs` per `contracts/azure-blob-ingestion-archive.md`
- [ ] T019 Extend `IIngestionRunIndexStore` with retail-pricing index methods in `src/BillDrift.Application/Ingestion/IIngestionRunIndexStore.cs` per `contracts/azure-table-ingestion-index.md`
- [ ] T020 [P] Define `IRetailPricingIngestionService` interface in `src/BillDrift.Application/Import/RetailPricing/IRetailPricingIngestionService.cs` per `contracts/csv-ingestion-pipeline.md`
- [ ] T021 [P] Implement `ParsedResellerPricingRow` in `src/BillDrift.Infrastructure/Import/Giacom/RetailPricing/Internal/ParsedResellerPricingRow.cs`
- [ ] T022 Implement SHA-256 file hash helper in `src/BillDrift.Infrastructure/Import/Giacom/RetailPricing/RetailPricingFileIdentity.cs` per research R2
- [ ] T023 Implement intake limits (`MaxFileSizeBytes` default 10 MB) in `src/BillDrift.Infrastructure/Import/Giacom/RetailPricing/RetailPricingIngestionLimits.cs`
- [ ] T024 Implement `ResellerPricingCsvHeaderMap` column alias registry in `src/BillDrift.Infrastructure/Import/Giacom/RetailPricing/ResellerPricingCsvHeaderMap.cs` per `contracts/reseller-pricing-header-map.md`
- [ ] T025 Implement `ResellerPricingCsvRowReader` CsvHelper wrapper in `src/BillDrift.Infrastructure/Import/Giacom/RetailPricing/ResellerPricingCsvRowReader.cs`
- [ ] T026 [P] Implement bounded stream reader in `src/BillDrift.Application/Import/RetailPricingCsvContentReader.cs` (mirror `SubscriptionManagementCsvContentReader`)
- [ ] T027 Extend `GiacomImportServiceCollectionExtensions` skeleton for retail pricing ingester in `src/BillDrift.Infrastructure/Import/Giacom/GiacomImportServiceCollectionExtensions.cs`
- [ ] T028 Reuse or reference shared `GoldenFileComparer` in `tests/BillDrift.Infrastructure.Tests/Import/Giacom/RetailPricing/GoldenFileComparer.cs` (from Giacom/Stripe tests)

**Checkpoint**: Application contract compiles; domain types extended; header map and row reader available.

---

## Phase 3: User Story 1 — Import Intended Retail Pricing from Giacom Price List (Priority: P1) 🎯 MVP

**Goal**: Submit `ResellerPricingVsRRP.csv` and receive `RawPriceListRow` plus `IntendedPrice` records with commercial keys, wholesale, RRP, margin fields, product status, and platform classification.

**Independent Test**: Given `reseller-pricing-sample-a.csv`, pipeline returns intended pricing records with offer ID, SKU ID, term, frequency, wholesale, RRP, and status for each valid row.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T029 [P] [US1] Create integration test skeleton in `tests/BillDrift.Infrastructure.Tests/Import/Giacom/RetailPricing/ResellerPricingCsvIngesterTests.cs`
- [ ] T030 [P] [US1] Add multi-key catalogue extraction assertion test in `ResellerPricingCsvIngesterTests.cs` per spec acceptance scenario 2

### Implementation for User Story 1

- [ ] T031 [US1] Implement `RawPriceListRowMapper` core field mapping in `src/BillDrift.Infrastructure/Import/Giacom/RetailPricing/RawPriceListRowMapper.cs`
- [ ] T032 [US1] Implement `RawImportId.Create(ImportSourceKind.GiacomPriceList, sourceDocumentId, lineKey)` assignment in `RawPriceListRowMapper.cs` per research R2
- [ ] T033 [US1] Implement mandatory field validation (offer ID or SKU ID, term, frequency, wholesale, RRP) in `RawPriceListRowMapper.cs`
- [ ] T034 [US1] Implement `ResellerPricingCsvIngester` orchestrator skeleton in `src/BillDrift.Infrastructure/Import/Giacom/RetailPricing/ResellerPricingCsvIngester.cs` per `contracts/csv-ingestion-pipeline.md`
- [ ] T035 [US1] Complete intake, header detection, row parsing, and raw mapping stages in `ResellerPricingCsvIngester.cs`
- [ ] T036 [US1] Wire basic normalization pass-through so `CataloguePrices` populated for happy path in `ResellerPricingCsvIngester.cs`
- [ ] T037 [US1] Complete result assembly with `IngestionOutcomeStatus`, summary counts, and `IngestionLogEntry` list in `ResellerPricingCsvIngester.cs`
- [ ] T038 [US1] Register `AddGiacomRetailPricingCsvIngestion` (`Singleton<IResellerPricingCsvIngester>`) in `GiacomImportServiceCollectionExtensions.cs`
- [ ] T039 [US1] Register ingester in `src/BillDrift.Api/Program.cs` via Giacom import extension
- [ ] T040 [US1] Generate golden JSON `tests/fixtures/reseller-pricing/expected/sample-a.json` from validated output
- [ ] T041 [US1] Complete golden-file assertions in `ResellerPricingCsvIngesterTests.cs` per `quickstart.md` Scenario 1

**Checkpoint**: MVP ingests sample CSV independently; golden test passes; mandatory catalogue fields captured.

---

## Phase 4: User Story 2 — Apply Default Pricing Strategy (Charge Catalogue RRP) (Priority: P1)

**Goal**: Catalogue-sourced prices establish RRP as the default intended retail charge; `ResolvedPrices` equals catalogue RRP when no manual override exists.

**Independent Test**: Given catalogue-only fixture, each `ResolvedPrices` entry has `PriceSource.Catalogue` and effective RRP equals catalogue RRP; end-of-sale rows retain RRP.

### Tests for User Story 2

- [ ] T042 [P] [US2] Add catalogue-only RRP strategy test in `ResellerPricingCsvIngesterTests.cs` per spec acceptance scenario 1
- [ ] T043 [P] [US2] Add end-of-sale RRP retention test using `end-of-sale.csv` in `ResellerPricingCsvIngesterTests.cs` per spec acceptance scenario 3

### Implementation for User Story 2

- [ ] T044 [US2] Implement `PriceListNormalizer` for catalogue rows implementing `IPriceListNormalizer` in `src/BillDrift.Application/Normalization/PriceListNormalizer.cs` per `contracts/pricing-strategy-rules.md`
- [ ] T045 [US2] Map catalogue rows to `PriceSource.Catalogue` and `ProductClassification.Csp` in `PriceListNormalizer.cs`
- [ ] T046 [US2] Integrate pricing resolution stage using existing `IntendedPriceResolver` in `ResellerPricingCsvIngester.cs` per research R8
- [ ] T047 [US2] Populate `ResolvedPrices` and `PricingResolutionDetail` with `WinningSource = Catalogue` for catalogue-only keys in `ResellerPricingCsvIngester.cs`
- [ ] T048 [US2] Update `RetailPricingCsvIngestionSummary.CatalogueOnlyCount` rollup in `ResellerPricingCsvIngester.cs`
- [ ] T049 [US2] Replace ingester inline normalization with `PriceListNormalizer` in `ResellerPricingCsvIngester.cs`

**Checkpoint**: Default charge-RRP strategy verified; end-of-sale rows keep RRP; SC-002 path for catalogue-only runs.

---

## Phase 5: User Story 3 — Support Manual RRP Overrides (Priority: P1)

**Goal**: Accept manual RRP entries for products absent from catalogue; classify as Non-CSP / bespoke; manual override wins on conflict.

**Independent Test**: Given manual override for key absent from catalogue → `ManualOverride` + `NonCsp`; given conflict on same key → manual RRP wins (`OverrideWinsCount >= 1`).

### Tests for User Story 3

- [ ] T050 [P] [US3] Add override-only bespoke product test in `ResellerPricingCsvIngesterTests.cs` per `quickstart.md` Scenario 3
- [ ] T051 [P] [US3] Add override-wins-over-catalogue precedence test in `ResellerPricingCsvIngesterTests.cs` per SC-003 and `quickstart.md` Scenario 4

### Implementation for User Story 3

- [ ] T052 [US3] Implement manual override request validation in `src/BillDrift.Infrastructure/Import/Giacom/RetailPricing/ManualOverrideValidator.cs` per `contracts/pricing-strategy-rules.md`
- [ ] T053 [US3] Map accepted overrides to `RawManualPriceEntry` with `ImportSourceKind.ManualPriceEntry` in `ResellerPricingCsvIngester.cs`
- [ ] T054 [US3] Extend `PriceListNormalizer` with `Normalize(RawManualPriceEntry)` mapping to `PriceSource.ManualOverride` and `ProductClassification.NonCsp` in `PriceListNormalizer.cs`
- [ ] T055 [US3] Integrate manual override parse stage into `ResellerPricingCsvIngester.cs` per pipeline contract stage 6–7
- [ ] T056 [US3] Apply `IntendedPriceResolver` precedence so manual beats catalogue in `ResellerPricingCsvIngester.cs` per FR-015
- [ ] T057 [US3] Populate `OverrideWinsCount` and resolution details when both sources exist in `ResellerPricingCsvIngester.cs`
- [ ] T058 [US3] Preserve `Reason` and `EffectiveDate` on manual source references in `PriceListNormalizer.cs`

**Checkpoint**: Manual overrides work standalone and on conflict; bespoke classification applied; SC-003 test passes.

---

## Phase 6: User Story 4 — Normalise Commercial Keys for Cross-Domain Matching (Priority: P1)

**Goal**: Offer ID + SKU ID + term + frequency normalised consistently; raw values retained; missing commercial keys skipped with warning.

**Independent Test**: Given mixed-case/whitespace IDs and term/frequency variants, normalized `CommercialKey` is consistent; absent offer+SKU rows skipped.

### Tests for User Story 4

- [ ] T059 [P] [US4] Create `TermFrequencyParserTests` in `tests/BillDrift.Infrastructure.Tests/Import/Giacom/RetailPricing/TermFrequencyParserTests.cs`
- [ ] T060 [P] [US4] Create `PriceListNormalizerTests` commercial-key tests in `tests/BillDrift.Application.Tests/Normalization/PriceListNormalizerTests.cs` per spec acceptance scenario 1–2
- [ ] T061 [P] [US4] Add missing commercial key skip test in `ResellerPricingCsvIngesterTests.cs` per spec acceptance scenario 3

### Implementation for User Story 4

- [ ] T062 [P] [US4] Implement `TermFrequencyParser` in `src/BillDrift.Infrastructure/Import/Giacom/RetailPricing/TermFrequencyParser.cs` per research R4
- [ ] T063 [US4] Implement Offer ID + SKU ID trim normalisation into `CommercialKey` in `PriceListNormalizer.cs` per research R6
- [ ] T064 [US4] Map `Term.Triennial` and billing frequency enums in `PriceListNormalizer.cs`
- [ ] T065 [US4] Emit `MissingCommercialKey` log and skip row when both IDs absent in `ResellerPricingCsvIngester.cs` per FR-021
- [ ] T066 [US4] Retain raw identifier values in `SourceReference` for traceability in `PriceListNormalizer.cs`

**Checkpoint**: Commercial keys align with subscription truth and Stripe matching conventions; normalizer tests pass.

---

## Phase 7: User Story 5 — Enable Stripe Catalogue Validation and Margin Analysis (Priority: P2)

**Goal**: Output records carry wholesale, RRP, margin amount, and margin percentage; platform classification when column present.

**Independent Test**: Fixture with margin columns populated → all monetary fields present; blank margin columns → absent not invented.

### Tests for User Story 5

- [ ] T067 [P] [US5] Create `PlatformClassifierTests` in `tests/BillDrift.Infrastructure.Tests/Import/Giacom/RetailPricing/PlatformClassifierTests.cs` per research R5
- [ ] T068 [P] [US5] Add margin field capture integration test in `ResellerPricingCsvIngesterTests.cs` per spec acceptance scenario 2
- [ ] T069 [P] [US5] Add blank-margin-not-invented test in `PriceListNormalizerTests.cs` per spec acceptance scenario 3

### Implementation for User Story 5

- [ ] T070 [P] [US5] Implement `PlatformClassifier` in `src/BillDrift.Infrastructure/Import/Giacom/RetailPricing/PlatformClassifier.cs` per research R5
- [ ] T071 [US5] Extend `RawPriceListRowMapper` to map platform and currency optional columns in `RawPriceListRowMapper.cs`
- [ ] T072 [US5] Implement GBP `Money` parsing for wholesale, RRP, and margin in `PriceListNormalizer.cs` per research R7
- [ ] T073 [US5] Map `Margin` and `MarginPercent` when present; leave absent when blank in `PriceListNormalizer.cs`
- [ ] T074 [US5] Map `PricingPlatform` on `IntendedPrice` in `PriceListNormalizer.cs`
- [ ] T075 [US5] Implement `UnsupportedCurrency` row skip when non-GBP currency column present in `ResellerPricingCsvIngester.cs`

**Checkpoint**: Margin and platform fields ready for reconciliation and catalogue validation; SC-004/SC-005 consumer smoke path unblocked.

---

## Phase 8: User Story 6 — Tolerate Format Variation and Partial Row Failures (Priority: P2)

**Goal**: Tolerate column reordering/synonyms; continue on individual row failures with structured logging and partial success status.

**Independent Test**: Given `partial-bad-rows.csv` and `column-variant.csv`, valid rows emitted, skipped rows logged, `PartialSuccess` when applicable; missing headers fail entire import.

### Tests for User Story 6

- [ ] T076 [P] [US6] Add partial-success fixture test in `ResellerPricingCsvIngesterTests.cs` per SC-006
- [ ] T077 [P] [US6] Add column-variant header reorder test in `ResellerPricingCsvIngesterTests.cs` per spec acceptance scenario 2
- [ ] T078 [P] [US6] Add duplicate commercial key last-wins warning test using `duplicate-keys.csv` in `ResellerPricingCsvIngesterTests.cs`
- [ ] T079 [P] [US6] Add determinism re-parse test (identical bytes → identical `RawImportId` keys) in `ResellerPricingCsvIngesterTests.cs`

### Implementation for User Story 6

- [ ] T080 [US6] Implement row skip for unparseable wholesale/RRP (`WholesaleUnparseable`, `RrpUnparseable`) in `ResellerPricingCsvIngester.cs`
- [ ] T081 [US6] Implement duplicate key detection with last-row-wins and `DuplicateCommercialKey` warning in `ResellerPricingCsvIngester.cs` per research R12
- [ ] T082 [US6] Implement file-level fail for missing mandatory headers (`MissingMandatoryHeader`) in `ResellerPricingCsvIngester.cs` per FR-029
- [ ] T083 [US6] Implement `PartialSuccess` vs `Success` vs `Failure` outcome resolution in `ResellerPricingCsvIngester.cs` per pipeline contract
- [ ] T084 [US6] Validate header alias coverage against `column-variant.csv` and extend `ResellerPricingCsvHeaderMap.cs` as needed

**Checkpoint**: Partial success and format tolerance verified; SC-001/SC-006 scenarios pass.

---

## Phase 9: Azure Persistence and API Upload

**Purpose**: Extend 009 ingestion stores for retail pricing via Aspire-injected `BlobServiceClient` and `TableServiceClient` only — **no SQL**, no manual connection strings.

**Independent Test**: `POST /api/imports/retail-pricing` stores source CSV blob, resolved-prices JSON, table index row; `GET` returns run summary and resolved prices.

### Tests for Azure Persistence

- [ ] T085 [P] Extend `InMemoryIngestionBlobStore` with retail-pricing methods in `src/BillDrift.Infrastructure/Ingestion/InMemoryIngestionBlobStore.cs`
- [ ] T086 [P] Extend `InMemoryIngestionRunIndexStore` with retail-pricing methods in `src/BillDrift.Infrastructure/Ingestion/InMemoryIngestionRunIndexStore.cs`
- [ ] T087 [P] Add retail-pricing round-trip tests to `tests/BillDrift.Infrastructure.Tests/Ingestion/AzureBlobIngestionArchiveStoreTests.cs` (Azurite when available)
- [ ] T088 [P] Add retail-pricing index tests to `tests/BillDrift.Infrastructure.Tests/Ingestion/AzureTableIngestionRunIndexStoreTests.cs` (Azurite when available)

### Implementation for Azure Persistence

- [ ] T089 Extend `AzureBlobIngestionArchiveStore` with `PersistRetailPricingResultAsync` using constructor `(BlobServiceClient, IOptions<IngestionStorageOptions>)` in `src/BillDrift.Infrastructure/Ingestion/AzureBlobIngestionArchiveStore.cs` per `contracts/azure-blob-ingestion-archive.md`
- [ ] T090 Extend `AzureTableIngestionRunIndexStore` with retail-pricing partition `GiacomPriceList` using constructor `(TableServiceClient, IOptions<IngestionStorageOptions>)` in `src/BillDrift.Infrastructure/Ingestion/AzureTableIngestionRunIndexStore.cs` per `contracts/azure-table-ingestion-index.md`
- [ ] T091 Extend `IngestionJsonSerializerContext` with `IntendedPrice`, `RawPriceListRow`, and retail-pricing result types in `src/BillDrift.Infrastructure/Ingestion/IngestionJsonSerializerContext.cs`
- [ ] T092 Register `IRetailPricingIngestionService` in `src/BillDrift.Infrastructure/Ingestion/IngestionServiceCollectionExtensions.cs`
- [ ] T093 Implement `RetailPricingIngestionService` orchestrating blob upload → ingest → resolve → persist → table index in `src/BillDrift.Application/Import/RetailPricing/RetailPricingIngestionService.cs`
- [ ] T094 Implement `RetailPricingImportEndpoints` (`POST`, `GET` list, `GET` detail, `GET` resolved-prices) in `src/BillDrift.Api/Imports/RetailPricingImportEndpoints.cs` per `contracts/azure-table-ingestion-index.md`
- [ ] T095 Register retail pricing import endpoints in `src/BillDrift.Api/Program.cs` (Aspire `BlobServiceClient`/`TableServiceClient` already registered)
- [ ] T096 Add API integration test for multipart upload with optional manual-overrides JSON in `tests/BillDrift.Infrastructure.Tests/Ingestion/RetailPricingImportApiTests.cs`

**Checkpoint**: Upload API persists to blob + table; manifest-last write protocol; `resolved-prices.json` primary payload; no SQL introduced.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Run-history integration, reconciliation smoke path, validation, and final quality gates.

- [ ] T097 [P] Ensure `RetailPricingIngestionRun` exposes `ContentFingerprint` and blob paths consumable by `InputSnapshotMetadata` for `InputDomainType.IntendedPricing` (feature 008)
- [ ] T098 [P] Add XML doc comments on public retail-pricing interfaces and `PriceListNormalizer` per constitution Principle I
- [ ] T099 Add reconciliation consumer smoke test loading resolved prices into price-mismatch fixture in `tests/BillDrift.Application.Tests/Reconciliation/RetailPricingConsumerTests.cs` per `quickstart.md` Scenario 9
- [ ] T100 Run full `quickstart.md` validation scenarios (1–10) and document pass/fail in `specs/010-retail-pricing-ingestion/quickstart.md` checklist section
- [ ] T101 Run `dotnet clean`, `dotnet restore`, `dotnet build --no-restore`, `dotnet test --no-build` from solution root per workspace build-quality rules
- [ ] T102 Verify no `new BlobServiceClient(connectionString)` or `new TableServiceClient(connectionString)` introduced — Aspire DI only (grep audit)
- [ ] T103 Verify no SQL database dependencies introduced for retail pricing ingestion (grep audit for EF Core / SqlClient in new code paths)

**Checkpoint**: All tests pass; quickstart validated; storage constraints verified; fourth reconciliation domain ingestion complete.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **User Stories (Phases 3–8)**: Depend on Foundational; recommended order US1 → US2 → US3 → US4 → US5 → US6 (US4 normalizer tasks can start in parallel with US2 after US1 mapper exists)
- **Azure Persistence (Phase 9)**: Depends on US1 ingester minimum; full value after US2–US3 resolution stages
- **Polish (Phase 10)**: Depends on Phases 3–9

### User Story Dependencies

| Story | Depends on | Independent test fixture |
|-------|------------|--------------------------|
| US1 (P1) | Foundational | `reseller-pricing-sample-a.csv` |
| US2 (P1) | US1 ingester + normalizer skeleton | Catalogue-only subset of sample fixture |
| US3 (P1) | US2 resolution stage | Manual override JSON + sample fixture |
| US4 (P1) | US1 raw rows | Casing variant rows in sample fixture |
| US5 (P2) | US4 normalizer | Sample fixture with margin/platform columns |
| US6 (P2) | US1 pipeline | `partial-bad-rows.csv`, `column-variant.csv` |

### Parallel Opportunities

- **Phase 1**: T002, T003 in parallel
- **Phase 2**: T006–T007, T010–T014, T017, T020–T021 in parallel after T005–T009
- **Phase 3**: T029–T030 in parallel
- **Phase 4**: T042–T043 in parallel
- **Phase 5**: T050–T051 in parallel
- **Phase 6**: T059–T061, T062 in parallel
- **Phase 7**: T067–T070 in parallel
- **Phase 8**: T076–T079 in parallel
- **Phase 9**: T085–T088, T089–T090 in parallel (store extensions independent)
- **Phase 10**: T097–T098 in parallel

---

## Parallel Example: User Story 1

```bash
# Launch US1 tests together:
Task T029: Create ResellerPricingCsvIngesterTests.cs skeleton
Task T030: Add multi-key catalogue extraction assertion

# After T031–T033 mapper tasks, parallel registration:
Task T038: Register in GiacomImportServiceCollectionExtensions.cs
Task T039: Register in Program.cs
```

---

## Parallel Example: User Story 5

```bash
# Launch US5 tests and classifier in parallel:
Task T067: PlatformClassifierTests.cs
Task T068: Margin field integration test
Task T070: PlatformClassifier.cs implementation
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Golden-file test + parser-only ingest per `quickstart.md` Scenario 1
5. Demo catalogue extraction without Azure if needed

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US1 → catalogue CSV ingestion (MVP parser)
3. US2 + US4 → normalizer + default RRP strategy (reconciliation-ready catalogue prices)
4. US3 → manual overrides + precedence (bespoke pricing)
5. US5 → margin/platform fields (full intended-price shape)
6. US6 → production-hardening (partial success, variants)
7. Phase 9 → persisted uploads + API
8. Phase 10 → run-history wire-up + full validation

### Parallel Team Strategy

With multiple developers after Foundational:

- **Developer A**: US1 → US6 parser pipeline
- **Developer B**: US4 → US5 normalizer + classifiers
- **Developer C**: US2 → US3 resolution + manual overrides
- **Developer D** (after US1): Phase 9 Azure extensions + API

---

## Notes

- [P] tasks = different files, no dependencies on incomplete work in the same phase
- [Story] label maps task to spec user story for traceability
- Extend 009 stores — do not create parallel `AzureBlob*` / `AzureTable*` classes
- `IntendedPriceResolver` already exists — do not reimplement precedence logic
- Obtain production `ResellerPricingVsRRP.csv` samples before locking `ResellerPricingCsvHeaderMap.cs` (T004, T084)
- Stop at any checkpoint to validate story independently
- Commit after each task or logical group
