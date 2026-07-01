# Quickstart: Billing Domain Model Validation

**Feature**: `001-billing-domain-model`  
**Prerequisites**: [.NET 10 SDK](https://dotnet.microsoft.com/download), Git

This guide validates the domain model implementation once tasks are executed. It does not include full application or Aspire host setup.

## Prerequisites

```powershell
dotnet --version   # Expect 9.x
```

## Solution Layout (after implementation)

```text
BillDrift.sln
src/
  BillDrift.Domain/
  BillDrift.Application/
tests/
  BillDrift.Domain.Tests/
```

## Build

```powershell
cd D:\repos\markheydon\billdrift-web
dotnet build BillDrift.sln --configuration Release
```

**Expected**: Build succeeds with zero warnings in `BillDrift.Domain`.

## Run Unit Tests

```powershell
dotnet test tests/BillDrift.Domain.Tests --configuration Release --verbosity normal
```

**Expected**: All tests pass, including:
- Value object validation (invalid `MexId`, `BillingPeriod`)
- Raw import idempotency key equality
- Intended price manual override precedence
- Reconciliation determinism (same inputs → same mismatches)

## Manual Validation Scenarios

### Scenario 1: Raw → Normalized Fidelity

1. Load fixture `tests/fixtures/giacom-billing-sample.json` (created during implementation).
2. Normalize to `SupplierCostLine`.
3. Assert `Source.SourceLineKey` matches supplier reference from PDF.

**Pass**: Normalized entity links to raw import; product name preserved separately from mapping.

### Scenario 2: Commercial Key Matching

1. Create `IntendedPrice` and `StripeBillingItem` with same `CommercialKey`.
2. Build `ReconciliationInputs` with matching subscription line.
3. Execute `IReconciliationEngine.Execute`.

**Pass**: Single `EntityMatchGroup` with all four domain entities; no mismatches.

### Scenario 3: Quantity Mismatch

1. Set subscription `LicenceCount = 10`, Stripe `Quantity = 5`.
2. Execute reconciliation.

**Pass**:
- One `Mismatch` with `Type = QuantityMismatch`
- One `ProposedChange` with `ActionType = UpdateQuantity`, proposed quantity `10`

### Scenario 4: Mapping Missing

1. Supplier line with unknown product name, no `ProductMapping`.
2. Execute reconciliation.

**Pass**: `MappingMissing` mismatch; no `ProposedChange` that mutates Stripe.

### Scenario 5: Determinism

1. Execute reconciliation twice with identical frozen `ReconciliationInputs`.
2. Compare mismatch sets (type + entity refs + description).

**Pass**: Sets are equivalent.

## Fixture References

| Fixture | Validates |
|---------|-----------|
| `giacom-billing-sample` | FR-004, FR-005 |
| `subscription-management-sample` | FR-006 |
| `reseller-pricing-sample` | FR-008 |
| `stripe-export-sample` | FR-011 |
| `product-mapping-sample` | FR-016 |
| `reconciliation-quantity-mismatch` | FR-021, FR-023 |
| `reconciliation-determinism` | FR-025, SC-006 |

## Related Artifacts

- [data-model.md](./data-model.md) — full entity reference
- [contracts/reconciliation-engine.md](./contracts/reconciliation-engine.md) — engine guarantees
- [contracts/normalization.md](./contracts/normalization.md) — parser → domain rules
- [spec.md](./spec.md) — business requirements

## Aspire Host (out of scope for this feature)

Full-stack validation with Blazor + Azure Storage requires a later feature. Domain validation is complete when `BillDrift.Domain.Tests` passes independently.
