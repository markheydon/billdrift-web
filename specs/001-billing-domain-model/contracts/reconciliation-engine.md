# Reconciliation Engine Contract

**Consumer**: `BillDrift.Application`  
**Provider**: `BillDrift.Application.Reconciliation` (implementation deferred to tasks phase)  
**Domain types**: `BillDrift.Domain.Reconciliation`

## Interface

```csharp
namespace BillDrift.Application.Reconciliation;

/// <summary>
/// Executes deterministic billing drift reconciliation over normalized inputs.
/// </summary>
public interface IReconciliationEngine
{
  /// <summary>
  /// Produces a ReconciliationRun from immutable inputs.
  /// Same inputs MUST yield equivalent mismatches and proposed changes (FR-025).
  /// </summary>
  ReconciliationRun Execute(ReconciliationRequest request);
}
```

## `ReconciliationRequest`

| Member | Type | Required |
|--------|------|----------|
| `RunId` | `RunId?` | No — generated if null |
| `Scope` | `BillingPeriod` | Yes |
| `Inputs` | `ReconciliationInputs` | Yes |
| `Options` | `ReconciliationOptions` | No |

## `ReconciliationOptions`

| Member | Type | Default | Description |
|--------|------|---------|-------------|
| `IncludeNonCspProducts` | `bool` | `false` | When false, skip `ProductClassification.NonCsp` lines |
| `IncludeInactiveSubscriptions` | `bool` | `false` | When false, only `SubscriptionStatus.Active` for quantity checks |
| `PriceTolerance` | `Money` | `0` | Absolute tolerance for price mismatch detection |
| `ProposeCatalogueChanges` | `bool` | `true` | Emit `CreateOrUpdateCatalogueEntry` for catalogue gaps |

## Output Guarantees

1. **Determinism**: Identical `ReconciliationInputs` + `ReconciliationOptions` → same `MismatchType` set and entity references.
2. **Ordering**: Mismatches ordered by `(Customer.MexId, CommercialKey, MismatchType)`.
3. **Proposed changes**: At most one primary `ProposedChange` per `Mismatch` unless catalogue action is supplementary.
4. **Idempotency keys**: Every `ProposedChange` includes `IdempotencyKey` per data model spec.
5. **No side effects**: `Execute` does not mutate inputs or call external services.

## Mismatch Detection Rules

| MismatchType | Condition |
|--------------|-----------|
| `MappingMissing` | Cannot resolve `CommercialKey` from supplier/subscription line and no mapping for product name |
| `MappingAmbiguous` | Multiple `ProductMapping` candidates for same supplier name variant |
| `MissingInStripe` | Active subscription truth line with no matched `StripeBillingItem` |
| `QuantityMismatch` | Matched group: `LicenceCount` ≠ Stripe `Quantity` |
| `BillingFrequencyMismatch` | Matched group: expected `PriceTermKey` from mapping ≠ Stripe price interval |
| `PriceMismatch` | Matched group: Stripe `UnitAmount` ≠ `IntendedPrice.Rrp` beyond tolerance |
| `CatalogueMissing` | `ProductMapping` exists but expected `StripePriceId` missing from mapping dictionary |

## Proposed Action Mapping

| MismatchType | Default ProposedActionType |
|--------------|---------------------------|
| `MissingInStripe` | `CreateMissingItem` |
| `QuantityMismatch` | `UpdateQuantity` |
| `BillingFrequencyMismatch` | `SwitchPrice` |
| `PriceMismatch` | `SwitchPrice` (if alternate price exists) or none (operator review) |
| `CatalogueMissing` | `CreateOrUpdateCatalogueEntry` (when `ProposeCatalogueChanges`) |
| `MappingMissing` / `MappingAmbiguous` | None — requires manual mapping first |

## Error Contract

| Exception | When |
|-----------|------|
| `DomainValidationException` | Invalid request (empty inputs where required, invalid period) |
| `ReconciliationException` | Internal invariant violation (should not occur with valid inputs) |

## Test Contract

Contract tests in `BillDrift.Domain.Tests` / `BillDrift.Application.Tests` MUST include:
- One fixture per `MismatchType`
- Determinism test: double execution, assert equality
- Pro-rata + recurring supplier lines do not double-count quantity
