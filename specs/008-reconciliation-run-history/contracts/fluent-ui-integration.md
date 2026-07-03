# Contract: Fluent UI Integration — Run History

**Feature**: `008-reconciliation-run-history`  
**Project**: `BillDrift.Web`  
**Date**: 2026-07-02

## Overview

Operator-facing run history pages using **Fluent UI Blazor v5** per `.cursor/skills/fluentui-blazor-usage/SKILL.md`. Web consumes run history API via `RunHistoryApiClient` — **no direct Azure storage access**.

---

## Pages

| Route | Component | Purpose |
|-------|-----------|---------|
| `/history` | `RunHistoryListPage.razor` | Browse/filter runs |
| `/history/{runId:guid}` | `RunDetailPage.razor` | Run detail + inputs + results |
| `/history/compare` | `RunComparisonPage.razor` | Two-run comparison |
| `/history/trends` | `DriftTrendsPage.razor` | Drift + pricing trends |

Add nav item under existing Fluent layout main nav: **Run History** (icon: History/Calendar).

---

## Run History List

**Components**: `FluentDataGrid`, `FluentSelect` (billing period filter), `FluentBadge` (status/clean run)

**Columns**:
- Completed date
- Billing period
- Mismatch count (badge — red if > 0)
- Proposal count
- Input presence indicators (5 icon badges)
- Archived indicator

**Filters**: Billing period, date range, clean runs only, include archived.

**Empty state**: "No reconciliation runs archived yet. Complete a reconciliation to build history."

---

## Run Detail

**Layout**: `FluentTabs`

| Tab | Content |
|-----|---------|
| Summary | Metrics, mapping version, status, initiator |
| Inputs | Grid of input snapshots with filename, fingerprint, record count; link to view JSON |
| Exceptions | Mismatch list from blob (category, customer, description) |
| Proposals | Proposed changes with **approval status badge** joined from API |
| Audit | Run audit events |

**Proposal status badges** (consistent with 007):
- Pending → `Neutral`
- Approved → `Success`
- Rejected → `Danger`
- Stale / Historical → `Warning`

**Link to approval queue**: `Navigate to /approvals/{runId}` when proposals exist.

---

## Run Comparison

**Flow**:
1. Select earlier and later run from dropdowns (sorted by date desc)
2. Click Compare
3. Display three sections: Input changes, Exception deltas (New / Resolved / Persisting), Mapping version warning if changed

**Components**: `FluentAccordion` for delta sections; `FluentDataGrid` for mismatch lists.

**Persisting section**: Highlight `ValuesChanged` rows; show approval status summary column.

---

## Drift Trends

**Sub-tabs**: Recurring Mismatches | Pricing Drift

### Recurring Mismatches
- Date range picker
- Grid: customer, product, type, occurrence count, first/last seen, decision summary
- Row click → detail panel with run-by-run occurrence list

### Pricing Drift
- Commercial key selector (offer/SKU/term/frequency)
- Timeline vertical list or `FluentTimeline`-style stacked entries
- Lag persistence badge when ≥ 2 runs

---

## API Client

`RunHistoryApiClient` methods mirror endpoints in [run-history-api-endpoints.md](./run-history-api-endpoints.md).

Registered in `BillDrift.Web/Program.cs`:

```csharp
builder.Services.AddHttpClient<RunHistoryApiClient>(client =>
{
    client.BaseAddress = new Uri("https+http://apiservice/");
});
```

---

## Terminology Alignment

Use consistent terms from 004/005/007:

| UI Label | Domain term |
|----------|-------------|
| Exception | `Mismatch` |
| Proposal | `ProposedChange` |
| Clean run | Zero exceptions |
| Billing period | `BillingPeriod` scope |

---

## Loading & Error States

- Skeleton loaders on grid fetch
- Toast on comparison failure (`FluentToast`)
- Integrity error → "Run archive corrupted — contact administrator" (no stack trace)

---

## Accessibility

- Data grids keyboard navigable
- Status badges include `title` tooltip with full state name
- Date filters labeled for screen readers

---

## Out of Scope (UI)

- Raw PDF/CSV download from history (future ingestion feature)
- Inline approval actions (link to 007 queue instead)
- Stripe apply execution UI
