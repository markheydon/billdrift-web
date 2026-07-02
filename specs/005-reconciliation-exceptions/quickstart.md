# Quickstart: Reconciliation Exception Surfacing Validation

**Feature**: `005-reconciliation-exceptions`  
**Prerequisites**: [.NET 10 SDK](https://dotnet.microsoft.com/download), feature 004 reconciliation engine implemented

This guide validates the exception surfacing layer once implementation tasks are complete. It chains `IReconciliationEngine.Execute` → `ExceptionSurfacingService.Surface`.

## Prerequisites

```powershell
dotnet --version   # Expect 10.x
```

## Build

```powershell
cd D:\repos\markheydon\billdrift-web
dotnet build BillDrift.sln --configuration Release
```

**Expected**: Build succeeds; `ExceptionSurfacingService` registered in DI.

## Run Tests

```powershell
dotnet test tests/BillDrift.Application.Tests --configuration Release --filter "FullyQualifiedName~ExceptionSurfacing" --verbosity normal
```

**Expected**: All exception surfacing tests pass, including:
- One test per `ExceptionCategory` mapping
- Suppression rules SR-1 through SR-5
- Catalogue consolidation CR-1
- Customer and within-group ordering
- Determinism (double `Surface` → identical exception IDs and order)
- Low-confidence proposed action stripping
- Empty run produces zero-count summary with `HasExceptions == false`

## Manual Validation Scenarios

### Scenario 1: Prioritised Customer Groups

1. Load reconciliation fixture `tests/fixtures/exception-surfacing/mixed-three-customers.json` (produces `ReconciliationRun` via engine).
2. Execute `ExceptionSurfacingService.Surface(run)`.

**Pass**:
- Customer with `Error` severity appears before `Warning`-only customers
- `Summary.BySeverity` counts match surfaced exceptions
- `Summary.RequiresActionNowCount` > 0 when bill-impacting errors exist

---

### Scenario 2: Evidence on Quantity Mismatch

1. Run engine on `tests/fixtures/reconciliation/quantity-mismatch.json`.
2. Surface exceptions.

**Pass**:
- One `QuantityLicenceMismatch` exception
- `Evidence` includes `SubscriptionTruth` and `StripeSubscriptionItem` sources
- `Explanation` states expected vs actual licence counts

---

### Scenario 3: Mapping Root-Cause Suppression

1. Use fixture `tests/fixtures/exception-surfacing/suppression-mapping-root-cause.json`.
2. Surface exceptions.

**Pass**:
- Raw mismatch count > surfaced exception count
- Only mapping exception visible (quantity/price suppressed per SR-1)
- `Summary.SuppressedCount` > 0

---

### Scenario 4: Orphaned Stripe Item

1. Use fixture `tests/fixtures/exception-surfacing/orphaned-stripe-item.json`.
2. Surface exceptions.

**Pass**:
- `OrphanedBillingItem` category present
- Domain `TruthVsStripe`
- Evidence includes Stripe subscription item details

---

### Scenario 5: Catalogue Consolidation

1. Use fixture `tests/fixtures/exception-surfacing/catalogue-consolidation.json`.
2. Surface exceptions.

**Pass**:
- Single catalogue exception per `CommercialKey` (CR-1)
- Merged evidence from multiple engine catalogue mismatches

---

### Scenario 6: Low Confidence — No Action Flag

1. Use fixture `tests/fixtures/exception-surfacing/low-confidence-no-action.json`.
2. Surface exceptions.

**Pass**:
- `ProposedChangeId` is null on bill-impacting categories
- `RequiresActionNow` is false
- Mapping exception may still be present

---

### Scenario 7: Clean Run — Empty State

1. Run engine on `tests/fixtures/reconciliation/clean-match-all-domains.json`.
2. Surface exceptions.

**Pass**:
- `HasExceptions == false`
- `Summary.TotalCount == 0`
- `CustomerGroups` is empty

---

### Scenario 8: Determinism

1. Execute `Surface(run)` twice on the same `ReconciliationRun`.

**Pass**:
- Identical ordered `SurfacedExceptionId` sequences
- Identical summary counts
- `GeneratedAt` may differ

## Fixture Layout

```text
tests/fixtures/exception-surfacing/
├── mixed-three-customers.json
├── suppression-mapping-root-cause.json
├── catalogue-consolidation.json
├── orphaned-stripe-item.json
├── mex-id-mismatch.json
├── low-confidence-no-action.json
└── clean-run-empty.json
```

Fixtures may be expressed as `ReconciliationRequest` JSON (engine input) or pre-built `ReconciliationRun` snapshots — implementation chooses; tests must document which.

## Contract References

- Pipeline phases: [exception-surfacing-pipeline.md](./contracts/exception-surfacing-pipeline.md)
- Mapping table: [mismatch-to-exception-mapping.md](./contracts/mismatch-to-exception-mapping.md)
- Suppression/ordering: [suppression-and-ordering-rules.md](./contracts/suppression-and-ordering-rules.md)
- Type definitions: [data-model.md](./data-model.md)

## Next Step

After all scenarios pass, run `/speckit-tasks` to generate implementation tasks.
