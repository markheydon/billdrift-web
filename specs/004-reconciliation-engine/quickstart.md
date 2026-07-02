# Quickstart: Billing Reconciliation Engine Validation

**Feature**: `004-reconciliation-engine`  
**Prerequisites**: [.NET 10 SDK](https://dotnet.microsoft.com/download), Git

This guide validates the reconciliation engine once implementation tasks are complete. It assumes normalized `ReconciliationInputs` fixtures exist (domain entities from feature 001).

## Prerequisites

```powershell
dotnet --version   # Expect 10.x
```

## Build

```powershell
cd D:\repos\markheydon\billdrift-web
dotnet build BillDrift.sln --configuration Release
```

**Expected**: Build succeeds; `ReconciliationEngine` registered in DI.

## Run Tests

```powershell
dotnet test tests/BillDrift.Application.Tests --configuration Release --filter "FullyQualifiedName~Reconciliation" --verbosity normal
```

**Expected**: All reconciliation tests pass, including:
- One test per `MismatchType`
- Determinism (double execution → equivalent output)
- Pro-rata supplier lines excluded from quantity totals
- Non-CSP lines flagged without bill-impacting proposals
- Low-confidence fuzzy match suppresses proposed actions

## Manual Validation Scenarios

### Scenario 1: Clean Match (all domains aligned)

1. Load fixture `tests/fixtures/reconciliation/clean-match-all-domains.json` as `ReconciliationInputs`.
2. Execute `IReconciliationEngine.Execute` with scope covering the billing period.

**Pass**:
- One `EntityMatchGroup` with truth, Stripe, supplier, and intended price attached
- `Confidence == High`
- Zero mismatches

---

### Scenario 2: Missing in Stripe

1. Load `tests/fixtures/reconciliation/missing-in-stripe.json`.
2. Execute reconciliation.

**Pass**:
- `MismatchType == MissingInStripe`
- `ProposedActionType == CreateMissingItem`
- Description includes expected licence count

---

### Scenario 3: Quantity Mismatch

1. Load `tests/fixtures/reconciliation/quantity-mismatch.json` (extends 001 fixture).
2. Execute reconciliation.

**Pass**:
- `MismatchType == QuantityMismatch`
- `ProposedActionType == UpdateQuantity`
- Proposed quantity equals subscription truth licence count (not supplier pro-rata sum)

---

### Scenario 4: Billing Frequency Mismatch

1. Load `tests/fixtures/reconciliation/billing-frequency-mismatch.json`.
2. Execute reconciliation.

**Pass**:
- `MismatchType == BillingFrequencyMismatch`
- `SwitchPrice` proposed when alternate monthly/annual price exists in catalogue index

---

### Scenario 5: Price Mismatch

1. Load `tests/fixtures/reconciliation/price-mismatch.json`.
2. Execute with `PriceTolerance = Money.Gbp(0)`.

**Pass**:
- `MismatchType == PriceMismatch`
- Expected value shows intended RRP; actual shows Stripe unit amount

---

### Scenario 6: Catalogue Missing

1. Load `tests/fixtures/reconciliation/catalogue-missing.json`.
2. Execute with `ProposeCatalogueChanges = true`.

**Pass**:
- `MismatchType == CatalogueMissing`
- `CreateOrUpdateCatalogueEntry` proposed with `CatalogueEntryPayload`

---

### Scenario 7: Mapping Missing

1. Load `tests/fixtures/reconciliation/mapping-missing.json` (unknown supplier product name).
2. Execute reconciliation.

**Pass**:
- `MismatchType == MappingMissing`
- No bill-impacting proposed actions

---

### Scenario 8: Mapping Ambiguous

1. Load `tests/fixtures/reconciliation/mapping-ambiguous.json` (duplicate Stripe items or duplicate name variants).
2. Execute reconciliation.

**Pass**:
- `MismatchType == MappingAmbiguous`
- Description lists candidate IDs

---

### Scenario 9: Non-CSP Supplier Line

1. Load `tests/fixtures/reconciliation/non-csp-supplier-line.json`.
2. Execute with default options (`IncludeNonCspProducts = false`).

**Pass**:
- `MappingMissing` with description starting `"Non-CSP line requires manual mapping:"`
- No bill-impacting proposed actions (SC-008)

---

### Scenario 10: Determinism

1. Load any fixture.
2. Execute `Execute` twice with identical request (same `RunId` or both null).
3. Compare mismatch sets by `(Type, Customer.MexId, CommercialKey, Description)`.

**Pass**: Sets are equivalent (SC-002).

---

### Scenario 11: Manual Price Override Precedence

1. Build inputs with catalogue price and manual override for same `CommercialKey`.
2. Execute reconciliation with price mismatch on Stripe vs override amount.

**Pass**: Comparison uses manual override RRP, not catalogue RRP.

## Golden Run Comparison

```powershell
dotnet test tests/BillDrift.Application.Tests --filter "GoldenRun"
```

Compares serialized mismatch output to `tests/fixtures/reconciliation/expected/quantity-mismatch-run.json`.

## Fixture References

| Fixture | Validates |
|---------|-----------|
| `clean-match-all-domains.json` | FR-011, SC-003 |
| `missing-in-stripe.json` | FR-008, FR-012 |
| `quantity-mismatch.json` | FR-008, FR-016 |
| `billing-frequency-mismatch.json` | FR-008, FR-012 |
| `price-mismatch.json` | FR-008, FR-017 |
| `catalogue-missing.json` | FR-010, FR-012 |
| `mapping-missing.json` | FR-006, FR-012 |
| `mapping-ambiguous.json` | FR-006, edge duplicate Stripe |
| `non-csp-supplier-line.json` | FR-009, SC-006, SC-008 |
| `duplicate-stripe-items.json` | Edge case — ambiguous |

## Related Artifacts

- [data-model.md](./data-model.md) — Application layer types and indexes
- [contracts/reconciliation-pipeline.md](./contracts/reconciliation-pipeline.md) — Stage orchestration
- [contracts/matching-phases.md](./contracts/matching-phases.md) — Product/customer matching
- [contracts/mismatch-rules.md](./contracts/mismatch-rules.md) — Detection and proposed actions
- [001 reconciliation contract](../001-billing-domain-model/contracts/reconciliation-engine.md) — Domain guarantees
- [spec.md](./spec.md) — Business requirements

## Out of Scope

- End-to-end ingest → normalize → reconcile pipeline (requires normalizer implementations)
- Stripe write/apply after operator approval
- Blazor reconciliation UI

Full-stack validation deferred to a future operator workflow feature.
