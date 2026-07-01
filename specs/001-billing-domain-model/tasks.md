# Tasks: Billing Drift Domain Model

**Input**: Design documents from `/specs/001-billing-domain-model/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Authority**: `specs/001-billing-domain-model` is the source of truth. The installed .NET Aspire starter template (`BillDrift.ApiService`, Weather sample pages, `src/BillDrift.Tests` integration tests) is scaffolding only and MUST be refactored to match the planned modular layout before domain work proceeds.

**Tests**: Included per constitution Principle II, quickstart.md validation scenarios, and contract test requirements in `contracts/reconciliation-engine.md`.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1–US5) for story-phase tasks only

---

## Phase 1: Setup (Aspire Template → Planned Solution Structure)

**Purpose**: Reconcile the installed Aspire starter with the architecture in `plan.md`. Remove sample Weather/demo code; add domain-layer projects; relocate tests to `tests/`.

- [x] T001 Audit current `BillDrift.slnx` and `src/` layout against `plan.md` Project Structure; document rename/move list in feature notes if needed
- [x] T002 [P] Rename `src/BillDrift.ApiService/` to `src/BillDrift.Api/` (`BillDrift.Api.csproj`, namespace `BillDrift.Api`) and update all project references
- [x] T003 [P] Create `src/BillDrift.Domain/BillDrift.Domain.csproj` — .NET 10 class library, zero NuGet dependencies (BCL only per `contracts/domain-assembly.md`)
- [x] T004 [P] Create `src/BillDrift.Application/BillDrift.Application.csproj` with project reference to `BillDrift.Domain`
- [x] T005 [P] Create `src/BillDrift.Infrastructure/BillDrift.Infrastructure.csproj` as empty placeholder referencing `BillDrift.Application` and `BillDrift.Domain`
- [x] T006 Create `tests/BillDrift.Domain.Tests/BillDrift.Domain.Tests.csproj` with xUnit + FluentAssertions referencing `BillDrift.Domain`
- [x] T007 [P] Create `tests/BillDrift.Application.Tests/BillDrift.Application.Tests.csproj` with xUnit + FluentAssertions referencing `BillDrift.Application`
- [x] T008 Update `BillDrift.slnx` to include Domain, Application, Infrastructure, `BillDrift.Api`, `tests/BillDrift.Domain.Tests`, and `tests/BillDrift.Application.Tests`; rename `src/BillDrift.Tests` → `tests/BillDrift.AppHost.Tests` (Aspire orchestration tests — keep separate from domain unit tests)
- [x] T009 Update `src/BillDrift.AppHost/AppHost.cs` and `BillDrift.AppHost.csproj` to reference renamed `BillDrift.Api` project (replace `BillDrift_ApiService`/`apiservice` identifiers)
- [x] T010 Strip Weather sample from `src/BillDrift.Api/Program.cs` — retain `AddServiceDefaults`, health/OpenAPI scaffold, remove `/weatherforecast` endpoint and `WeatherForecast` record
- [x] T011 [P] Strip Weather sample from `src/BillDrift.Web/` — remove `Weather.razor`, `WeatherApiClient.cs`, NavMenu weather link, and `HttpClient` registration in `Program.cs`; keep minimal Blazor SSR shell
- [x] T012 Wire project references: `BillDrift.Api` → `BillDrift.Application` + `BillDrift.ServiceDefaults`; verify `BillDrift.Web` → `BillDrift.ServiceDefaults` only (no domain coupling in scaffold)
- [x] T013 Run `dotnet build BillDrift.slnx` from repo root and fix any broken references after refactor

**Checkpoint**: Solution builds; Aspire AppHost starts with minimal Api + Web; no Weather sample code remains; planned project folders exist.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared value objects, enums, and validation infrastructure required by ALL user stories. MUST complete before raw import or normalized entity work.

**⚠️ CRITICAL**: No user story implementation until this phase is complete.

- [x] T014 Implement `DomainValidationException` in `src/BillDrift.Domain/Common/DomainValidationException.cs` per `contracts/domain-assembly.md`
- [x] T015 [P] Implement identifier value objects (`MexId`, `TenantId`, `OfferId`, `SkuId`, Stripe IDs, supplier IDs) in `src/BillDrift.Domain/Common/Identifiers.cs` with factory validation
- [x] T016 [P] Implement `Money`, `CurrencyCode`, and `Money.Gbp` factory in `src/BillDrift.Domain/Common/Money.cs`
- [x] T017 [P] Implement `BillingPeriod` in `src/BillDrift.Domain/Common/BillingPeriod.cs` with `End >= Start` validation
- [x] T018 [P] Implement `Term`, `BillingFrequency` enums in `src/BillDrift.Domain/Common/Term.cs` and `BillingFrequency.cs`
- [x] T019 [P] Implement `CommercialKey`, `CommercialKeyRoot`, and `PriceTermKey` in `src/BillDrift.Domain/Common/CommercialKey.cs`
- [x] T020 [P] Implement `CustomerIdentity` in `src/BillDrift.Domain/Common/CustomerIdentity.cs`
- [x] T021 [P] Implement `ImportSourceKind`, `RawImportId`, and `SourceReference` in `src/BillDrift.Domain/Common/SourceReference.cs`
- [x] T022 [P] Implement billing enums (`ChargeType`, `SubscriptionStatus`, `PriceListStatus`, `PriceSource`) in `src/BillDrift.Domain/Common/BillingEnums.cs`
- [x] T023 [P] Implement mapping enums (`ProductClassification`, `MappingConfidence`, `MappingSource`, `MatchConfidence`) in `src/BillDrift.Domain/Common/MappingEnums.cs`
- [x] T024 [P] Implement reconciliation enums (`MismatchType`, `MismatchSeverity`, `ProposedActionType`) in `src/BillDrift.Domain/Common/ReconciliationEnums.cs`
- [x] T025 Add value object validation unit tests in `tests/BillDrift.Domain.Tests/Common/ValueObjectValidationTests.cs` (invalid `MexId`, invalid `BillingPeriod`, Stripe ID prefix rules)
- [x] T026 Verify `BillDrift.Domain.csproj` has zero package references; add `Directory.Build.props` or analyzer rules if repo convention requires

**Checkpoint**: Common layer compiles and tests pass; all shared enums and value objects available for import and billing types.

---

## Phase 3: User Story 1 — Represent Imported Billing Data Faithfully (Priority: P1) 🎯 MVP

**Goal**: Define distinct raw import structures for all four data sources (Giacom PDF, Subscription Management, price list CSV, Stripe) preserving source fidelity without premature normalization.

**Independent Test**: Given representative fixture data for each source, domain types hold every field needed for normalization; `RawImportId` equality enables idempotent re-import.

### Tests for User Story 1

- [x] T027 [P] [US1] Add raw import idempotency and field-preservation tests in `tests/BillDrift.Domain.Tests/Import/RawImportTests.cs`
- [x] T028 [P] [US1] Create fixture files under `tests/fixtures/` (`giacom-billing-sample.json`, `subscription-management-sample.json`, `reseller-pricing-sample.json`, `stripe-export-sample.json`) per `quickstart.md`

### Implementation for User Story 1

- [x] T029 [P] [US1] Implement `RawGiacomBillingLine` in `src/BillDrift.Domain/Import/RawGiacomBillingLine.cs`
- [x] T030 [P] [US1] Implement `RawSubscriptionManagementRow` in `src/BillDrift.Domain/Import/RawSubscriptionManagementRow.cs`
- [x] T031 [P] [US1] Implement `RawPriceListRow` in `src/BillDrift.Domain/Import/RawPriceListRow.cs`
- [x] T032 [P] [US1] Implement `RawManualPriceEntry` in `src/BillDrift.Domain/Import/RawManualPriceEntry.cs`
- [x] T033 [P] [US1] Implement `RawStripeCustomer` in `src/BillDrift.Domain/Import/Stripe/RawStripeCustomer.cs`
- [x] T034 [P] [US1] Implement `RawStripeSubscription` in `src/BillDrift.Domain/Import/Stripe/RawStripeSubscription.cs`
- [x] T035 [P] [US1] Implement `RawStripeSubscriptionItem` in `src/BillDrift.Domain/Import/Stripe/RawStripeSubscriptionItem.cs`
- [x] T036 [P] [US1] Implement `RawStripeProduct` in `src/BillDrift.Domain/Import/Stripe/RawStripeProduct.cs`
- [x] T037 [P] [US1] Implement `RawStripePrice` in `src/BillDrift.Domain/Import/Stripe/RawStripePrice.cs`
- [x] T038 [US1] Add fixture deserialization helper in `tests/BillDrift.Domain.Tests/Import/FixtureLoader.cs` to load sample JSON into raw types

**Checkpoint**: All five import source families represented; tests confirm idempotency keys and raw field preservation (FR-001–FR-003).

---

## Phase 4: User Story 2 — Normalize Cross-Domain Identifiers (Priority: P1)

**Goal**: Define normalized billing entities keyed by shared identifiers (`MexId`, `CommercialKey`, `CustomerIdentity`) enabling cross-domain comparison.

**Independent Test**: Given raw fixtures for one customer across all four domains, normalized entities share `CustomerIdentity` and `CommercialKey` where applicable; manual price override precedence is representable.

### Tests for User Story 2

- [x] T039 [P] [US2] Add normalized entity construction tests in `tests/BillDrift.Domain.Tests/Billing/NormalizedEntityTests.cs`
- [x] T040 [P] [US2] Add manual override precedence test (`ManualOverride` beats `Catalogue` for same `CommercialKey`) in `tests/BillDrift.Domain.Tests/Billing/IntendedPricePrecedenceTests.cs`

### Implementation for User Story 2

- [x] T041 [P] [US2] Implement entity ID structs (`SupplierCostLineId`, `MicrosoftSubscriptionLineId`, `IntendedPriceId`, `StripeBillingItemId`) in `src/BillDrift.Domain/Billing/EntityIds.cs`
- [x] T042 [P] [US2] Implement `SupplierCostLine` in `src/BillDrift.Domain/Billing/SupplierCostLine.cs` per `data-model.md` (FR-004, FR-005)
- [x] T043 [P] [US2] Implement `MicrosoftSubscriptionLine` in `src/BillDrift.Domain/Billing/MicrosoftSubscriptionLine.cs` (FR-006, FR-007)
- [x] T044 [P] [US2] Implement `IntendedPrice` in `src/BillDrift.Domain/Billing/IntendedPrice.cs` (FR-008–FR-010)
- [x] T045 [P] [US2] Implement `StripeMappingMetadata` in `src/BillDrift.Domain/Billing/StripeMappingMetadata.cs`
- [x] T046 [US2] Implement `StripeBillingItem` in `src/BillDrift.Domain/Billing/StripeBillingItem.cs` (FR-011, FR-012)
- [x] T047 [US2] Stub normalizer interfaces in `src/BillDrift.Application/Normalization/` per `contracts/normalization.md` (`IGiacomBillingNormalizer`, `ISubscriptionManagementNormalizer`, `IPriceListNormalizer`, `IStripeBillingNormalizer`)
- [x] T048 [US2] Stub `IIntendedPriceResolver` in `src/BillDrift.Application/Normalization/IIntendedPriceResolver.cs` with `ManualOverride` precedence logic (FR-010)

**Checkpoint**: All normalized billing entity types exist; immutability via `sealed record`; entities link to `SourceReference`; price precedence testable via resolver stub.

---

## Phase 5: User Story 3 — Maintain Canonical Product Mapping (Priority: P1)

**Goal**: First-class `ProductMapping` linking offer/SKU keys, supplier naming variants, Stripe catalogue IDs, and mapping confidence.

**Independent Test**: Two supplier name variants and one Stripe product resolve through a single `ProductMapping`; ambiguous mappings are representable without silent join.

### Tests for User Story 3

- [x] T049 [P] [US3] Add `ProductMapping` and variant resolution tests in `tests/BillDrift.Domain.Tests/Mapping/ProductMappingTests.cs`
- [x] T050 [P] [US3] Add fixture `tests/fixtures/product-mapping-sample.json` per `quickstart.md`

### Implementation for User Story 3

- [x] T051 [P] [US3] Implement `ProductMappingId` in `src/BillDrift.Domain/Mapping/ProductMappingId.cs`
- [x] T052 [P] [US3] Implement `SupplierNameVariant` in `src/BillDrift.Domain/Mapping/SupplierNameVariant.cs`
- [x] T053 [US3] Implement `ProductMapping` in `src/BillDrift.Domain/Mapping/ProductMapping.cs` (FR-016–FR-018)
- [x] T054 [US3] Stub `IProductMappingResolver` and `ProductMappingResolution` in `src/BillDrift.Application/Mapping/IProductMappingResolver.cs` per `contracts/normalization.md`

**Checkpoint**: Mapping types support zero-to-many supplier variants, term/frequency price dictionary, and confidence levels; resolver stub returns `Found`/`NotFound`/`Ambiguous`.

---

## Phase 6: User Story 4 — Execute and Record Reconciliation Runs (Priority: P2)

**Goal**: `ReconciliationRun` captures inputs, matched entity groups, mismatches, and proposed changes for reproducible, deterministic analysis.

**Independent Test**: Given fixed `ReconciliationInputs`, two runs with same inputs produce equivalent mismatch sets (types + entity references).

### Tests for User Story 4

- [x] T055 [P] [US4] Add `ReconciliationRun` and `EntityMatchGroup` construction tests in `tests/BillDrift.Domain.Tests/Reconciliation/ReconciliationRunTests.cs`
- [x] T056 [P] [US4] Add determinism placeholder test in `tests/BillDrift.Domain.Tests/Reconciliation/DeterminismTests.cs` (validates immutable inputs model; full engine determinism completed when stub engine exists)
- [x] T057 [P] [US4] Add fixture `tests/fixtures/reconciliation-determinism.json` per `quickstart.md`

### Implementation for User Story 4

- [x] T058 [P] [US4] Implement `RunId`, `MatchGroupId` in `src/BillDrift.Domain/Reconciliation/Identifiers.cs`
- [x] T059 [P] [US4] Implement `ReconciliationInputs` in `src/BillDrift.Domain/Reconciliation/ReconciliationInputs.cs` (FR-019 snapshot collections)
- [x] T060 [US4] Implement `EntityMatchGroup` in `src/BillDrift.Domain/Reconciliation/EntityMatchGroup.cs` (FR-020)
- [x] T061 [US4] Implement `ReconciliationRun` in `src/BillDrift.Domain/Reconciliation/ReconciliationRun.cs` (FR-019)
- [x] T062 [US4] Stub `IReconciliationEngine`, `ReconciliationRequest`, and `ReconciliationOptions` in `src/BillDrift.Application/Reconciliation/` per `contracts/reconciliation-engine.md`
- [x] T063 [US4] Implement `ReconciliationEngineStub` in `src/BillDrift.Application/Reconciliation/ReconciliationEngineStub.cs` returning empty run (implementation deferred; satisfies interface contract for build)

**Checkpoint**: Reconciliation run aggregate composes inputs, match groups, mismatches, and proposed changes; request/options types defined for future engine.

---

## Phase 7: User Story 5 — Classify Mismatches and Propose Actions (Priority: P2)

**Goal**: All seven mismatch types and four proposed action types are representable with idempotency keys and operator-facing context.

**Independent Test**: For each `MismatchType`, domain model holds at least one concrete example with a valid `ProposedChange` where applicable (per SC-003).

### Tests for User Story 5

- [x] T064 [P] [US5] Add mismatch type coverage tests (one example per `MismatchType`) in `tests/BillDrift.Domain.Tests/Reconciliation/MismatchTypeCoverageTests.cs`
- [x] T065 [P] [US5] Add proposed change and idempotency key tests in `tests/BillDrift.Domain.Tests/Reconciliation/ProposedChangeTests.cs`
- [x] T066 [P] [US5] Add fixture `tests/fixtures/reconciliation-quantity-mismatch.json` per `quickstart.md`

### Implementation for User Story 5

- [x] T067 [P] [US5] Implement `MismatchId`, `ProposedChangeId`, `IdempotencyKey` in `src/BillDrift.Domain/Reconciliation/Identifiers.cs`
- [x] T068 [P] [US5] Implement `MismatchEntityRefs` in `src/BillDrift.Domain/Reconciliation/MismatchEntityRefs.cs`
- [x] T069 [US5] Implement `Mismatch` in `src/BillDrift.Domain/Reconciliation/Mismatch.cs` (FR-021, FR-022)
- [x] T070 [P] [US5] Implement `ProposedChangeTarget` and `CatalogueEntryPayload` in `src/BillDrift.Domain/Reconciliation/ProposedChangeTarget.cs`
- [x] T071 [US5] Implement `ProposedChange` with idempotency key format `{RunId}:{MismatchId}:{ActionType}` in `src/BillDrift.Domain/Reconciliation/ProposedChange.cs` (FR-023, FR-024)
- [x] T072 [US5] Add `NormalizationException` in `src/BillDrift.Application/Normalization/NormalizationException.cs` per `contracts/normalization.md`

**Checkpoint**: All mismatch and action enums exercised in tests; `ProposedChange` includes execution order and catalogue payload; no UI/persistence types introduced (FR-026).

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Solution-wide validation, quickstart execution, and contract compliance.

- [x] T073 [P] Add `ReconciliationException` in `src/BillDrift.Application/Reconciliation/ReconciliationException.cs` per `contracts/reconciliation-engine.md`
- [x] T074 [P] Add namespace-level `GlobalUsings.cs` or folder `AssemblyInfo` only if needed; ensure all public types match namespaces in `contracts/domain-assembly.md`
- [x] T075 Run `dotnet build BillDrift.slnx --configuration Release` from repo root with zero warnings in `BillDrift.Domain`
- [x] T076 Run `dotnet test tests/BillDrift.Domain.Tests --configuration Release --verbosity normal` per `quickstart.md`
- [x] T077 Execute quickstart.md manual validation scenarios 1–5 (raw fidelity, commercial key, quantity mismatch, mapping missing, determinism)
- [x] T078 [P] Remove any remaining Aspire template sample artifacts (Weather pages, `WeatherApiClient`, unused `BillDrift.ApiService` references); retain `tests/BillDrift.AppHost.Tests` for orchestration smoke tests
- [x] T079 Verify `BillDrift.Infrastructure` remains empty placeholder with no persistence or Azure SDK references (FR-026, research R10)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately; blocks everything until solution aligns with plan
- **Phase 2 (Foundational)**: Depends on Phase 1 — BLOCKS all user stories
- **Phase 3 (US1)**: Depends on Phase 2 — raw types need common value objects
- **Phase 4 (US2)**: Depends on Phase 2; logically follows US1 (normalized entities reference raw via `SourceReference`, not raw types directly)
- **Phase 5 (US3)**: Depends on Phase 2; can parallel with US2 after foundational (uses `CommercialKeyRoot`, not billing entities)
- **Phase 6 (US4)**: Depends on US2 and US3 (reconciliation inputs reference billing + mapping types)
- **Phase 7 (US5)**: Depends on US4 (mismatches/proposed changes are part of reconciliation aggregate)
- **Phase 8 (Polish)**: Depends on all desired user stories

### User Story Dependencies

| Story | Priority | Depends On | Can Start After |
|-------|----------|------------|-----------------|
| US1 — Raw imports | P1 | Foundational | Phase 2 complete |
| US2 — Normalized entities | P1 | Foundational, US1 (conceptual) | Phase 2; US1 types for fixture linkage |
| US3 — Product mapping | P1 | Foundational | Phase 2 (parallel with US2) |
| US4 — Reconciliation runs | P2 | US2, US3 | Phases 4–5 complete |
| US5 — Mismatches & actions | P2 | US4 | Phase 6 complete |

### Within Each User Story

- Tests written first (red) where specified, then implementation (green)
- Parallel `[P]` tasks touch different files only
- Application stubs follow domain type completion for that story

### Parallel Opportunities

- **Phase 1**: T002–T005, T007, T011 in parallel after T001
- **Phase 2**: T015–T024 all parallel after T014
- **Phase 3**: T029–T037 all parallel; T038 after raw types exist
- **Phase 4**: T041–T045 parallel; T047–T048 after billing types
- **Phase 5**: T051–T052 parallel
- **US2 and US3** can proceed in parallel once Phase 2 completes (different folders)

---

## Parallel Example: User Story 1

```bash
# Launch all raw Stripe types together:
T033: RawStripeCustomer in src/BillDrift.Domain/Import/Stripe/RawStripeCustomer.cs
T034: RawStripeSubscription in src/BillDrift.Domain/Import/Stripe/RawStripeSubscription.cs
T035: RawStripeSubscriptionItem in src/BillDrift.Domain/Import/Stripe/RawStripeSubscriptionItem.cs
T036: RawStripeProduct in src/BillDrift.Domain/Import/Stripe/RawStripeProduct.cs
T037: RawStripePrice in src/BillDrift.Domain/Import/Stripe/RawStripePrice.cs
```

---

## Parallel Example: Foundational Value Objects

```bash
# After T014 (DomainValidationException), launch in parallel:
T015: Identifiers.cs
T016: Money.cs
T017: BillingPeriod.cs
T018: Term.cs / BillingFrequency.cs
T019: CommercialKey.cs
T020: CustomerIdentity.cs
T021: SourceReference.cs
T022–T024: Enum files
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Refactor Aspire template → planned solution structure
2. Complete Phase 2: Foundational value objects and validation
3. Complete Phase 3: User Story 1 (raw import types + fixtures + tests)
4. **STOP and VALIDATE**: `dotnet test tests/BillDrift.Domain.Tests` passes for Import tests; fixtures load correctly
5. Domain ingestion target is ready for future parser features

### Incremental Delivery

1. Setup + Foundational → shared types ready
2. US1 → raw fidelity validated (MVP for ingestion contract)
3. US2 + US3 (parallel) → normalized entities + mapping
4. US4 + US5 → reconciliation aggregates complete
5. Polish → quickstart.md full validation

### Suggested MVP Scope

**Phases 1–3 only** (through User Story 1): Delivers auditable raw import model and solution structure aligned with plan. This is the smallest independently testable increment that unblocks future ingestion features.

### Aspire Template Refactor Notes

| Template (current) | Plan (authoritative) | Task |
|--------------------|----------------------|------|
| `BillDrift.ApiService` | `BillDrift.Api` | T002, T009 |
| `src/BillDrift.Tests` (Aspire integration) | `tests/BillDrift.AppHost.Tests` (renamed, kept) + new `tests/BillDrift.Domain.Tests` | T006, T008 |
| Weather sample API/Web | Minimal scaffold only | T010, T011 |
| No Domain/Application/Infrastructure | Full modular layout | T003–T005, T012 |
| Solution in `src/` only | `tests/` at repo root | T006–T008 |

---

## Notes

- Do NOT implement PDF parsing, Stripe API clients, Azure persistence, or Blazor UI in this feature
- `BillDrift.Application` receives interface stubs only; full normalizer and engine implementations are later features
- Use `readonly record struct` for value objects and `sealed record` for entities per research R2
- Entity IDs may use `Guid.CreateVersion5` from `RawImportId` for stable normalization (per `contracts/normalization.md`) — decide during US2 implementation
- Commit after each phase checkpoint or logical task group
