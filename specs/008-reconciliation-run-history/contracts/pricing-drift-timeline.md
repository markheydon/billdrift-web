# Contract: Pricing Drift Timeline

**Feature**: `008-reconciliation-run-history`  
**Project**: `BillDrift.Application.History/PricingDriftAnalyzer.cs`  
**Date**: 2026-07-02

## Purpose

Track how intended retail pricing (RRP + manual overrides) and Stripe catalogue prices evolve across stored runs for a given `CommercialKey` (FR-013, FR-014, SC-005).

---

## Inputs

For each run in the requested window (ordered by `CompletedAt` asc):

1. `intended-pricing.json` blob — `IntendedPrice[]`
2. `stripe-billing.json` blob — `StripeBillingItem[]` (catalogue prices extracted)
3. `results/mismatches.json` — catalogue-related mismatches (optional validation)

---

## Event Detection Rules

For each run and target `CommercialKey`:

### 1. RRP Changed (`RrpChanged`)

Compare catalogue-sourced intended price (non-override) amount to previous run.

```
if (currentCatalogueAmount != previousCatalogueAmount) → emit RrpChanged
```

### 2. Override Added / Removed

| Transition | Event |
|------------|-------|
| No override → override present | `OverrideAdded` |
| Override present → no override | `OverrideRemoved` |
| Override amount changed | `OverrideAdded` (with prior noted in payload) |

Effective price for comparison = override wins over catalogue (same rule as reconciliation engine).

### 3. Stripe Price Changed (`StripePriceChanged`)

Compare Stripe catalogue unit amount for matching `CommercialKey` + interval to previous run.

### 4. Catalogue Missing (`CatalogueMissing`)

No Stripe price found for key in current run but intended pricing exists.

Persist from mismatch snapshot OR computed absence — prefer mismatch when present for consistency.

### 5. Catalogue Aligned (`CatalogueAligned`)

Previous run had `CatalogueMissing` or amount mismatch; current run has matching Stripe price within tolerance.

**Tolerance**: Same as reconciliation engine default (documented in 004 options; typically 0 for exact match).

---

## Lag Persistence Calculation

When `CatalogueMissing` or amount mismatch occurs:

```
lagRunsPersisted = count of consecutive runs from first lag run through current
```

Reset to 0 on `CatalogueAligned` or `StripePriceChanged` aligning amounts.

SC-005: lag persisting ≥ 2 runs MUST appear in timeline output.

---

## Timeline Entry Shape

Each entry (see data-model `PricingDriftTimelineEntry`):

```json
{
  "commercialKey": { "offerId": "...", "skuId": "...", "term": "P1Y", "frequency": "Annual" },
  "runId": "...",
  "runDate": "2026-07-02T10:00:00Z",
  "eventType": "RrpChanged",
  "intendedAmount": 12.50,
  "overrideAmount": null,
  "stripeCatalogueAmount": 12.50,
  "currency": "GBP",
  "lagRunsPersisted": null
}
```

---

## Query API

See [run-history-api-endpoints.md](./run-history-api-endpoints.md) `GET /trends/pricing`.

---

## Performance

- Load only `intended-pricing.json` and `stripe-billing.json` blobs per run (not full results)
- Window default max 24 runs (2 years monthly cadence)
- Target < 5s for 24-run window (SC-005 validation)

---

## Edge Cases

| Case | Behaviour |
|------|-----------|
| Intended pricing absent in run | Skip run for RRP events; may still detect Stripe-only changes |
| Multiple Stripe prices for same key | Use same selection rules as `StripeCatalogueIndex` (004) |
| End-of-sale price list entry | Include but flag in entry metadata |
| Manual override and RRP change same run | Emit both events ordered: RrpChanged then OverrideAdded |

---

## Test Fixtures Required

| Fixture | Validates |
|---------|-----------|
| RRP change run 2, Stripe lag runs 2-3, aligned run 4 | Lag persistence + alignment |
| Override added mid-window | OverrideAdded event |
| Recurring catalogue-missing 3 runs | Distinct from amount mismatch |

---

## Invariants

- Events MUST be replayable from stored blobs alone
- Analyzer MUST NOT call live Stripe API
- Currency mismatches excluded from amount comparison (separate warning)
