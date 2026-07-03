# Quickstart: V1 MVP Operator UI

**Feature**: `013-v1-mvp-ui`  
**Date**: 2026-07-03

Validation guide for the operator UI and new API endpoints. Assumes Aspire AppHost running with Azurite storage emulator.

**Prerequisites**: See [plan.md](./plan.md) and fixture paths from features 002–012.

**Start app**:

```powershell
cd D:\repos\markheydon\billdrift-web
dotnet run --project src/BillDrift.AppHost
```

Open Web URL from Aspire dashboard (typically `https://localhost:7xxx`).

---

## Phase A — API Enablement (can test via HTTP before UI)

### A1 — Upload Giacom PDF

```powershell
curl -X POST "https://localhost:7yyy/api/imports/giacom-pdf" `
  -F "file=@tests/fixtures/giacom/sample-pre-billing.pdf"
```

**Expected**: `200 OK` with `ingestionId`, `status: Completed`, line count > 0.

**Maps to**: FR-007, User Story 1 scenario 3

### A2 — Upload Stripe CSV bundle

```powershell
curl -X POST "https://localhost:7yyy/api/imports/stripe-csv" `
  -F "subscriptions=@tests/fixtures/stripe/subscriptions.csv" `
  -F "products=@tests/fixtures/stripe/products.csv" `
  -F "prices=@tests/fixtures/stripe/prices.csv"
```

**Expected**: `200 OK` with subscription + catalogue counts.

**Maps to**: FR-008, User Story 1 scenario 4

### A3 — Upload Subscription Management CSV (existing API)

```powershell
curl -X POST "https://localhost:7yyy/api/imports/subscription-management" `
  -F "file=@tests/fixtures/giacom/subscription-management.csv"
```

**Expected**: `200 OK` — confirms existing API still works.

### A4 — Upload retail pricing CSV (existing API)

```powershell
curl -X POST "https://localhost:7yyy/api/imports/retail-pricing" `
  -F "file=@tests/fixtures/giacom/ResellerPricingVsRRP.csv"
```

**Expected**: `200 OK` with resolved price count.

### A5 — Start reconciliation run

Use ingestion IDs from A1–A4:

```powershell
curl -X POST "https://localhost:7yyy/api/reconciliation/runs" `
  -H "Content-Type: application/json" `
  -d '{
    "billingPeriod": { "start": "2026-06-01", "end": "2026-06-30" },
    "supplierCostIngestionId": "<pdf-id>",
    "subscriptionTruthIngestionId": "<sub-id>",
    "intendedPricingIngestionId": "<pricing-id>",
    "stripeBillingIngestionId": "<stripe-id>",
    "productMappings": [],
    "persistRun": true
  }'
```

**Expected**: `200 OK` with `runId`, `summary.mismatchCount`, `exceptions` populated.

**Maps to**: FR-009, FR-013, User Story 2

### A6 — Ingest proposals from run

```powershell
curl -X POST "https://localhost:7yyy/api/reconciliation/<runId>/approvals/ingest-from-run" `
  -H "Content-Type: application/json" `
  -d '{ "includeInvestigationItems": true }'
```

**Expected**: `200 OK` with `ingestedCount` > 0 when run has proposals.

**Maps to**: FR-011, FR-024

---

## Phase B — Ingestion UI

### B1 — Upload all source types via `/ingestion`

1. Navigate to **Ingestion**
2. Upload each file type on respective tab
3. Verify success banner with record count
4. Verify import appears in recent runs grid

**Expected**: All four tabs functional; errors shown for invalid files.

**Maps to**: FR-001–FR-006, SC-001

### B2 — Invalid file handling

Upload a non-CSV file to Subscription Management tab.

**Expected**: Clear error message; no crash; no stack trace visible.

**Maps to**: FR-006

---

## Phase C — Reconciliation UI

### C1 — Start reconciliation from UI

1. Navigate to **Reconciliation**
2. Select ingestion runs from dropdowns (or use latest)
3. Set billing period
4. Add inline product mappings (or paste JSON)
5. Click **Start reconciliation**

**Expected**: Summary with mismatch counts; link to exceptions.

**Maps to**: FR-013, User Story 2 scenario 1

### C2 — Exception dashboard filtering

1. Open exceptions for a run with multiple categories
2. Filter to "Quantity mismatch" only

**Expected**: Only quantity mismatches visible.

**Maps to**: FR-014, FR-015, SC-004, SC-005

### C3 — Clean run indication

Run reconciliation with aligned fixture data.

**Expected**: Distinct "clean run" message; option to view in history.

**Maps to**: FR-016

### C4 — Margin view

1. Open margin tab for a run with cost + RRP data
2. Verify negative margins highlighted

**Expected**: Margin % visible; negative/low visually distinct; missing cost/RRP shows "Unknown".

**Maps to**: FR-017, SC-009

---

## Phase D — Approval & History

### D1 — Full approval cycle

1. From reconciliation results, click **Send to approval queue**
2. Navigate to `/approvals/{runId}`
3. Approve one proposal; reject one with reason
4. Export changeset

**Expected**: Queue updates; audit trail records actions; export contains approved only.

**Maps to**: FR-021–FR-028, SC-002, SC-007

### D2 — Bulk approve with preview

1. Select multiple pending proposals
2. Click bulk approve
3. Review preview; confirm

**Expected**: Preview lists all items; all approved on confirm.

**Maps to**: FR-023, SC-006

### D3 — Run history polish

1. Navigate to **Run History**
2. Filter by billing period
3. Open run detail — verify all tabs
4. Compare two runs via dropdowns (not GUID entry)

**Expected**: Input presence badges; compare shows deltas.

**Maps to**: FR-031–FR-035, SC-008

### D4 — Drift trends

Navigate to **Run History → Trends**.

**Expected**: Recurring drift patterns visible when multiple runs archived.

**Maps to**: FR-034

---

## Phase E — Remaining Pages

### E1 — Navigation completeness

Click every main nav item.

**Expected**: No dead routes; all pages load.

**Maps to**: FR-036, SC-003

### E2 — Home workflow orientation

Open `/`.

**Expected**: Workflow steps visible; links to each area; pending approval count when applicable.

**Maps to**: FR-037, SC-010

### E3 — Catalogue reconciliation UI

1. Navigate to **Catalogue**
2. Start run with Stripe + pricing ingestion IDs
3. Review missing/misaligned items
4. Ingest fixes to approval queue

**Expected**: Catalogue tab in approvals populated.

**Maps to**: FR-029, FR-030, User Story 6

### E4 — Classification overrides

1. Navigate to **Mapping / Classification**
2. Apply override for a non-CSP item
3. Re-run reconciliation

**Expected**: Override reflected in exception routing.

**Maps to**: FR-019, FR-020

### E5 — Session mapping (deferred persistence)

1. Edit mappings on Mapping page
2. Start reconciliation using session mappings
3. Close browser — mappings not persisted globally

**Expected**: Mappings work for current session; banner explains no persistent store.

**Maps to**: FR-018, Application-Layer Capability Notes

---

## Regression Checks

| Check | Command / Action | Expected |
|-------|------------------|----------|
| Build clean | `dotnet build --no-incremental` | 0 errors, 0 warnings |
| Tests pass | `dotnet test` | All pass |
| Existing approval API | Approve via existing endpoint | Unchanged behaviour |
| Existing run history API | List runs | Unchanged behaviour |
| No Stripe writes | Grep UI for apply/push to Stripe | Export only |

---

## Scenario → Requirement Map

| Scenario | FR | SC |
|----------|----|----|
| A1–A4 | FR-007, FR-008 | SC-001 |
| A5 | FR-009, FR-010 | SC-002 |
| A6 | FR-011 | SC-002 |
| B1 | FR-001–FR-005 | SC-001 |
| C1–C3 | FR-013–FR-016 | SC-004, SC-005 |
| C4 | FR-017 | SC-009 |
| D1 | FR-021–FR-028 | SC-002, SC-007 |
| D2 | FR-023 | SC-006 |
| D3–D4 | FR-031–FR-034 | SC-008 |
| E1–E2 | FR-036–FR-037 | SC-003, SC-010 |

---

## Troubleshooting

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| 404 on new endpoints | Endpoints not registered in Program.cs | Verify `Map*Endpoints()` calls |
| Reconciliation empty | Missing ingestion IDs or mappings | Check pre-check warnings |
| Approval queue empty | Proposals not ingested | Use ingest-from-run button |
| Compare page GUID fields | Phase D not complete | Use run dropdowns per contract |
| Azurite errors | Emulator not running | Start via Aspire AppHost |
