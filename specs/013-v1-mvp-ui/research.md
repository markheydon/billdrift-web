# Research: V1 MVP Operator UI

**Feature**: `013-v1-mvp-ui`  
**Date**: 2026-07-03

## R1 — Scope boundary: Application layer vs API enablement

**Decision**: Treat the **Application layer domain/business logic as frozen**. In-scope work is (a) **API endpoints** that expose existing Application services and (b) **Blazor UI** that consumes those endpoints. Thin orchestration glue that mirrors established ingestion patterns (009/010) is classified as **API enablement**, not new domain logic.

**Rationale**: User clarified that "no backend changes" meant no new Application-layer features/functionality — not that HTTP endpoints are forbidden. PDF ingester, Stripe CSV ingester, reconciliation engine, and exception surfacing already exist but lack HTTP surfaces.

**Alternatives considered**:
- *UI-only, no new endpoints*: Rejected — operators cannot upload PDF/Stripe CSV or trigger reconciliation without external tooling.
- *New Application domain services for mapping persistence*: Rejected — deferred per spec Application-Layer Capability Notes.

---

## R2 — PDF ingestion API pattern

**Decision**: Add `GiacomPdfImportEndpoints` following `SubscriptionManagementImportEndpoints` pattern. Introduce a thin `GiacomPdfIngestionService` in `BillDrift.Application.Import.Giacom` that orchestrates `IGiacomBillingPdfIngester` → `IGiacomBillingNormalizer` → `IIngestionBlobStore` + `IIngestionRunIndexStore`. Extend `IIngestionBlobStore` with supplier-cost persistence methods if not already present (Infrastructure wiring only).

**Rationale**: Parser (002) explicitly deferred upload API; normalizer exists; subscription CSV service (009) is the canonical orchestration template. Reconciliation orchestration needs persisted ingestion run IDs, not ephemeral parse results.

**Alternatives considered**:
- *API calls ingester directly without persistence*: Rejected — breaks reconciliation snapshot loading and run history input domains.
- *Defer PDF to manual API scripts*: Rejected — core MVP input source.

---

## R3 — Stripe CSV ingestion API pattern

**Decision**: Add `StripeCsvImportEndpoints` accepting multipart upload of `subscriptions.csv` (required) plus optional `products.csv` and `prices.csv`. Introduce `StripeCsvIngestionService` orchestrating `IStripeBillingCsvIngester` → `IStripeBillingNormalizer` → blob persistence including `PersistStripeCatalogueAsync` for catalogue reconciliation reuse.

**Rationale**: Stripe ingester (003) and `PersistStripeCatalogueAsync` exist; catalogue reconciliation (012) already loads Stripe blobs by ingestion run ID. Missing piece is upload + normalize + persist orchestration.

**Alternatives considered**:
- *Separate endpoints per CSV file type*: Rejected — ingester contract expects bundled request; single multipart upload matches operator workflow.

---

## R4 — Reconciliation orchestration endpoint

**Decision**: Add `POST /api/reconciliation/runs` via `ReconciliationEndpoints` and a `ReconciliationOrchestrationService` that:
1. Accepts ingestion run ID references + billing period + inline `ProductMapping[]`
2. Loads normalized snapshots from ingestion blob stores
3. Builds `ReconciliationRequest` and calls `IReconciliationEngine.Execute`
4. Calls `ExceptionSurfacingService` for operator view model
5. Optionally calls `RunArchiveService.PersistAsync`
6. Returns run summary + exceptions + raw run payload for approval ingest

**Rationale**: Matches 008 `run-history-pipeline.md` contract. Engine and archive services exist; only HTTP orchestration is missing. Tests already demonstrate this sequence in Application.Tests.

**Alternatives considered**:
- *UI calls engine via multiple round-trips*: Rejected — violates SC-002 (no CLI/external tooling); exposes assembly complexity to Blazor.
- *Auto-ingest approvals on run complete*: Rejected — 008 explicitly defers; operator triggers ingest from UI (FR-024).

---

## R5 — Approval ingest convenience (FR-011)

**Decision**: Add `POST /api/reconciliation/{runId}/approvals/ingest-from-run` that loads a persisted reconciliation run from run history (or accepts run ID from just-completed orchestration), runs exception surfacing if needed, and assembles `ApprovalIngestionRequest` server-side. Keep existing full-payload `POST .../ingest` unchanged.

**Rationale**: Current ingest requires embedded `ReconciliationRun` + `ReconciliationExceptionViewModel` — operators cannot assemble this manually. Server-side assembly from archived run is pure API orchestration over existing services.

**Alternatives considered**:
- *UI assembles full payload client-side*: Rejected — large payloads, duplicated surfacing logic, error-prone.
- *Change Application ingest contract*: Rejected — Application layer frozen.

---

## R6 — Product mapping UI (deferred persistence)

**Decision**: Mapping UI supports **view + inline edit for current session/reconciliation run** only. Mappings supplied on reconciliation/catalogue API requests and stored in run-history input blobs. No persistent mapping CRUD store in this feature.

**Rationale**: Spec Application-Layer Capability Notes — no Application-layer mapping store exists. Full managed mapping catalogue requires new domain functionality.

**Alternatives considered**:
- *JSON file upload for mappings*: Acceptable v1 workaround documented in quickstart; not primary UI path.
- *New mapping store in this feature*: Rejected — violates Application freeze.

---

## R7 — Fluent UI Blazor v5 patterns

**Decision**: Extend existing Fluent UI layout from 007/008. Add typed `HttpClient` API clients per domain area (Ingestion, Reconciliation, Classification, Catalogue). Use Interactive Server render mode. Reuse partial implementations (ApprovalQueuePage, RunHistoryListPage) rather than rewrite.

**Rationale**: Constitution III — reuse established components. Skill `.cursor/skills/fluentui-blazor-usage/SKILL.md` governs v5 patterns (no `FluentDesignTheme`, use `FluentProviders`, `FluentNav` not v4 `FluentNavMenu`).

**Alternatives considered**:
- *Blazor WebAssembly*: Rejected — existing app is Interactive Server; Aspire service discovery uses `https+http://api`.
- *Bootstrap for new pages*: Rejected — inconsistent with 007 refactor direction.

---

## R8 — API client architecture (Web)

**Decision**: One interface + implementation per API domain, registered in `Program.cs` with `https+http://api` base address:
- `IIngestionApiClient` — all import endpoints (subscription, retail, PDF, Stripe)
- `IReconciliationApiClient` — run orchestration + exceptions
- `IClassificationApiClient` — classification endpoints (already exist, no client yet)
- `ICatalogueReconciliationApiClient` — catalogue runs
- Extend existing `IApprovalApiClient` — add bulk approve + ingest-from-run
- Extend existing `IRunHistoryApiClient` — add persist, input download, compare export

**Rationale**: Matches existing two-client pattern; keeps pages thin; enables contract tests against OpenAPI shapes.

**Alternatives considered**:
- *Single mega client*: Rejected — violates single-responsibility and complicates testing.

---

## R9 — Margin view data source

**Decision**: Margin view rendered from reconciliation run results / exception view model where cost and RRP evidence is already attached by exception surfacing. No new margin calculation logic — display only.

**Rationale**: Margin = RRP − Cost is computed during reconciliation/surfacing. UI filters and highlights rows with margin evidence fields populated.

**Alternatives considered**:
- *Dedicated margin API endpoint*: Rejected unless run payload too large — prefer embedding in reconciliation run response first.

---

## R10 — Implementation phasing

**Decision**: Implement in dependency order:
1. **Phase A — API enablement**: PDF import, Stripe import, reconciliation orchestration, approval ingest-from-run
2. **Phase B — Ingestion UI**: Upload pages + import history
3. **Phase C — Reconciliation UI**: `/reconciliation` page, exception dashboard, margin tab
4. **Phase D — Complete partial UI**: Approvals (bulk approve, ingest button, run picker), run history polish, compare/trends nav
5. **Phase E — Mapping/classification UI**, catalogue UI, home/workflow page

**Rationale**: API endpoints unblock UI work; reconciliation page is P1 critical path; partial UI completion is lower risk when APIs stable.

**Alternatives considered**:
- *UI-first with mocked APIs*: Rejected — constitution II requires contract tests at API boundaries; mocks hide orchestration gaps.

---

## R11 — Testing strategy

**Decision**:
- **API contract tests**: New endpoint integration tests in `BillDrift.Api.Tests` (or existing test project) using WebApplicationFactory + in-memory stores
- **UI component tests**: Deferred — manual quickstart validation for v1; focus automated tests on API clients and critical approval flows
- **Existing Application tests**: Unchanged — Application layer frozen

**Rationale**: Constitution II — contract tests at changed boundaries. Application reconciliation logic already tested; this feature adds HTTP adapters and UI.

**Alternatives considered**:
- *bUnit for every Blazor page*: Rejected — high cost for v1; quickstart manual scenarios sufficient initially.

---

## Resolved unknowns

| Unknown | Resolution |
|---------|------------|
| Can PDF upload work without Application changes? | Thin ingestion service + blob store methods (Infrastructure) — enablement glue, not domain logic |
| Reconciliation trigger mechanism | `POST /api/reconciliation/runs` orchestration service |
| Approval ingest payload complexity | New `ingest-from-run` convenience endpoint |
| Mapping persistence | Deferred — session/inline only |
| Stripe catalogue blob wiring | In scope via Stripe ingestion service calling existing `PersistStripeCatalogueAsync` |
