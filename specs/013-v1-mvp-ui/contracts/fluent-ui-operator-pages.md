# Contract: Fluent UI Operator Pages & Routes

**Feature**: `013-v1-mvp-ui`  
**Project**: `BillDrift.Web`  
**Skill reference**: `.cursor/skills/fluentui-blazor-usage/SKILL.md`  
**Date**: 2026-07-03

## Purpose

Define all operator-facing Blazor pages, routes, navigation, and API client wiring for V1 MVP workflows. Extends 007 and 008 partial implementations to full coverage.

---

## Navigation (MainLayout)

| Label | Route | Status |
|-------|-------|--------|
| Home | `/` | **New** — workflow dashboard |
| Ingestion | `/ingestion` | **New** |
| Reconciliation | `/reconciliation` | **New** (replaces dead link) |
| Approvals | `/approvals` | **Extend** — add run picker |
| Catalogue | `/catalogue` | **New** |
| Mapping | `/mapping` | **New** (session/inline, deferred persistence) |
| Run History | `/history` | **Extend** polish |
| *(sub)* Compare | `/history/compare` | **Extend** — run dropdowns |
| *(sub)* Trends | `/history/trends` | **Extend** — add to nav |

Use `FluentNav`, `FluentNavItem`, `FluentNavGroup` (v5 — no `FluentNavMenu`).

---

## Pages

### Home — `Pages/Home/WorkflowHomePage.razor`

Route: `/`

- Workflow step cards: Upload → Reconcile → Review → Approve → Export
- Latest import status chips per source type
- Link to latest run / pending approvals count
- Empty state for first-time operators

### Ingestion — `Pages/Ingestion/IngestionHubPage.razor`

Route: `/ingestion`

**Tabs**: Subscription CSV | Retail Pricing | Giacom PDF | Stripe CSV

Each tab:
- `FluentInputFile` upload
- Progress during upload
- Result summary (`FluentMessageBar` success/error)
- Recent runs `FluentDataGrid` for that source type

**API clients**: `IIngestionApiClient`

### Reconciliation — `Pages/Reconciliation/ReconciliationPage.razor`

Route: `/reconciliation`

**Sections**:
1. **Input selection** — pick latest or specific ingestion run ID per source (dropdowns populated from import history)
2. **Product mappings** — inline edit grid (session state; JSON paste fallback)
3. **Billing period** — date pickers
4. **Start run** — `FluentButton Appearance=Primary`
5. **Results** — summary badges + link to exceptions

Sub-routes (optional tabs on same page):
- `/reconciliation/{runId}/exceptions` — exception dashboard
- `/reconciliation/{runId}/margin` — margin view

**Exception dashboard**:
- `FluentSelect` category filter (FR-014 categories)
- `FluentDataGrid` with customer, product, category, description
- Detail panel: expected vs actual, rule reference

**Margin view**:
- `FluentDataGrid<MarginLineViewModel>`
- `FluentBadge` severity (Healthy/Low/Negative/Unknown)
- Sort by margin percent ascending (worst first)

**API clients**: `IReconciliationApiClient`, `IIngestionApiClient`

### Mapping — `Pages/Mapping/MappingPage.razor`

Route: `/mapping`

- View/edit `ProductMapping[]` in session
- Load from run-history input blob when `runId` query param present
- Export/import JSON for operator portability
- **No persistent save** — banner explains deferred persistence

### Classification — `Pages/Classification/ClassificationPage.razor`

Route: `/classification` (linked from Mapping nav group)

- Override list + apply/clear forms
- Mex ID config + category rules (existing classification API)

**API client**: `IClassificationApiClient`

### Catalogue — `Pages/Catalogue/CatalogueReconciliationPage.razor`

Route: `/catalogue`

- Start catalogue run form (ingestion IDs + inline mappings)
- Results grid: missing product/price, RRP mismatch
- "Ingest fixes to approval queue" button

**API client**: `ICatalogueReconciliationApiClient`

### Approvals — extend existing

Routes:
- `/approvals` — **New** run picker page listing runs with pending proposals
- `/approvals/{runId}` — existing `ApprovalQueuePage` (extend)

**Extensions to existing**:
- Wire `BulkApproveDialog` (component exists, unused)
- Add "Ingest proposals" when queue empty but run has results
- Extend `ApprovalApiClient` with bulk approve + ingest-from-run

### Run History — extend existing

Polish per 008 contract:
- Billing period filter, date range, input presence badges
- Compare page: replace GUID text fields with run dropdowns
- Add Compare + Trends to nav

Extend `RunHistoryApiClient`: persist, input download, compare export.

---

## API Clients (Program.cs)

```csharp
builder.Services.AddHttpClient<IIngestionApiClient, IngestionApiClient>(...);
builder.Services.AddHttpClient<IReconciliationApiClient, ReconciliationApiClient>(...);
builder.Services.AddHttpClient<IClassificationApiClient, ClassificationApiClient>(...);
builder.Services.AddHttpClient<ICatalogueReconciliationApiClient, CatalogueReconciliationApiClient>(...);
// existing: IApprovalApiClient, IRunHistoryApiClient (extend)
```

Base address: `https+http://api` (Aspire service discovery).

---

## Shared Components

| Component | Purpose |
|-----------|---------|
| `ImportResultBanner.razor` | Success/partial/error after upload |
| `IngestionRunPicker.razor` | Dropdown of recent runs by source type |
| `ExceptionCategoryBadge.razor` | Consistent category colours |
| `MarginSeverityBadge.razor` | Margin health indicator |
| `WorkflowStepIndicator.razor` | Home page progress |
| `EmptyStatePanel.razor` | Consistent empty states (constitution III) |

---

## Error & Loading Patterns (Constitution III)

| State | Component |
|-------|-----------|
| Loading | `FluentProgressRing` / `FluentProgressBar` |
| Empty | `FluentMessageBar Intent=Info` + action link |
| Error | `FluentMessageBar Intent=Error` + retry button |
| Permission | Disabled actions + explanatory message |

Terminology: **exception** (not "error"), **proposal** (not "action"), **approval** (not "confirm apply").

---

## Out of Scope (UI)

- Stripe write/apply UI
- Authentication login UI
- Persistent product mapping catalogue
- Automated scheduled reconciliation

---

## Verification

1. All nav links resolve (FR-036)
2. Upload each source type via Ingestion hub
3. Complete reconciliation → exceptions → ingest → approve → export without CLI
4. Bulk approve dialog functions
5. Compare runs via dropdowns (SC-008)
