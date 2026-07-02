# Reconciliation Pipeline Contract

**Consumer**: `BillDrift.Application.Tests`, future Blazor/API hosts  
**Provider**: `BillDrift.Application.Reconciliation.ReconciliationEngine`  
**Domain types**: `BillDrift.Domain.Reconciliation`

## Interface

Extends [001 reconciliation-engine contract](../../001-billing-domain-model/contracts/reconciliation-engine.md). This document specifies **implementation pipeline behaviour**.

```csharp
namespace BillDrift.Application.Reconciliation;

/// <summary>
/// Orchestrates staged reconciliation over normalized billing inputs.
/// </summary>
public sealed class ReconciliationEngine : IReconciliationEngine
{
    public ReconciliationRun Execute(ReconciliationRequest request);
}
```

## Pipeline Stages (ordered)

| # | Stage | Input | Output |
|---|-------|-------|--------|
| 1 | `InputValidationStage` | `ReconciliationRequest` | Validated request or `DomainValidationException` |
| 2 | `IndexBuildStage` | `ReconciliationInputs` | Populated indexes on context |
| 3 | `MatchGroupBuildStage` | Indexes + subscription truth lines | Initial `EntityMatchGroup` list |
| 4 | `SubscriptionTruthReconcileStage` | Match groups | Mismatches + proposed changes for truth ↔ Stripe |
| 5 | `SupplierCostReconcileStage` | Match groups + supplier lines | Attached cost lines + mapping/non-CSP mismatches |
| 6 | `CatalogueReconcileStage` | Required commercial keys + catalogue index | Catalogue missing/price mismatches |
| 7 | `OutputOrderingStage` | All collections | Sorted immutable output |

**Invariant**: Stages MUST NOT mutate `ReconciliationInputs`. Stages MAY append to context lists but MUST NOT remove prior mismatches (append-only).

## `InputValidationStage`

| Check | Failure |
|-------|---------|
| `Scope.End < Scope.Start` | `DomainValidationException` |
| `Inputs` is null | `DomainValidationException` |
| All input collections null | Treat as empty (not error) |

## `IndexBuildStage`

Builds:
- `IntendedPriceIndex` from `Inputs.IntendedPrices`
- `StripeCatalogueIndex` from `Inputs.StripeItems`
- `ProductMappingIndex` from `Inputs.ProductMappings`

**Comment requirement**: Each index builder MUST document collision and precedence rules inline.

## Stage Error Contract

| Exception | When |
|-----------|------|
| `DomainValidationException` | Invalid request (stage 1) |
| `ReconciliationException` | Internal invariant broken (e.g., duplicate `MatchGroupId`) |

Stages MUST NOT catch and swallow exceptions except to wrap unexpected errors in `ReconciliationException` with stage name context.

## Output Assembly

```csharp
return new ReconciliationRun(
    context.RunId,
    DateTimeOffset.UtcNow,
    request.Scope,
    request.Inputs,
    context.MatchGroups.AsReadOnly(),
    context.Mismatches.AsReadOnly(),
    context.ProposedChanges.AsReadOnly());
```

## Dependency Injection

```csharp
services.AddSingleton<IProductMappingResolver, ProductMappingResolver>();
services.AddSingleton<IReconciliationEngine, ReconciliationEngine>();
```

`ReconciliationEngine` MAY depend on `IProductMappingResolver` but MUST NOT depend on Infrastructure types.

## Side Effects

**None.** Pipeline MUST NOT:
- Call Stripe API
- Write files
- Mutate input entities
- Generate random IDs except via injected `RunId` or `RunId.New()` once at start

## Performance Contract

| Metric | Target |
|--------|--------|
| 1,000 match groups | < 5 seconds on developer hardware |
| Index build | O(n) over input sizes |
| Match group build | O(n log n) worst case for sorting keys |

## Test Contract

`BillDrift.Application.Tests` MUST include:
- Pipeline executes all stages (integration smoke test)
- Invalid scope throws `DomainValidationException`
- Empty inputs returns empty run with valid `RunId`
- Stage ordering preserved in audit log field (optional debug trace in tests)
