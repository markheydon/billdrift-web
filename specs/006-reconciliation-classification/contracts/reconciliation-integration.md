# Contract: Reconciliation Integration

**Feature**: `006-reconciliation-classification`  
**Integrates with**: Features 004 (engine), 005 (exception surfacing)  
**Date**: 2026-07-02

## Overview

Classification replaces ad-hoc `ProductMapping.Classification` checks as the **authoritative** source for per-item reconciliation behaviour. Mapping classification remains a signal input only.

## Request Changes

### `ReconciliationRequest`

Add optional property:

```csharp
ClassificationContext? Classifications { get; init; }
```

When null, engine uses legacy `ProductMapping.Classification` path (backward compatible).

### `ReconciliationContext`

Add:

```csharp
ClassificationContext? Classifications { get; set; }
```

Set by orchestrator before pipeline executes.

## New Pipeline Stage

**`ClassificationEnrichmentStage`** — optional pre-stage when `Classifications` is null and `IClassificationService` is available:

1. Call `ClassificationService.ClassifyAsync(inputs, scope)`
2. Assign result to `context.Classifications`

**Order** (updated reconciliation pipeline):

```
ClassificationEnrichmentStage (new, when needed)
→ MatchGroupBuildStage
→ SubscriptionTruthReconcileStage
→ SupplierCostReconcileStage
→ CatalogueReconcileStage
→ OutputOrderingStage
```

## Engine Modifications

### `MatchGroupBuildStage`

Replace:

```csharp
productMapping!.Classification == ProductClassification.NonCsp
```

With helper:

```csharp
IsNonCspForReconciliation(subscriptionLine, productMapping, context.Classifications)
```

Logic:
1. If classification context has entry for subscription truth ref → use `ReconciliationItemClassification.NonCspSupplier`
2. Else if `Internal` → set `mappingBlocksBilling = false` but flag `internalItem = true` for missing-in-stripe skip
3. Else fall back to `productMapping.Classification == NonCsp`

### `SupplierCostReconcileStage`

Classify supplier lines before attach; Non-CSP classification triggers existing `EmitNonCspMapping` path using item classification ref.

### `MismatchDetector` / `ProposedChangeFactory`

Before emitting `MissingInStripe`:

```csharp
if (IsInternalOrCustomService(subscriptionLine, context.Classifications))
    return; // RI-1a, RI-3a
```

### `Mismatch` description enrichment (optional)

Append `[Classification: Internal]` to mismatch descriptions for audit — not required for v1.

## Exception Surfacing Modifications

### `SurfacingContext`

Add:

```csharp
IReadOnlyDictionary<string, ItemClassification>? ClassificationsByStableKey { get; init; }
```

### `SuppressPhase`

Add `ApplySr6ClassificationSuppression` after SR-2:

**SR-6**: Suppress `MissingBillingItem` when:
- Group subscription line ref maps to `Internal`, OR
- `CustomService` with no truth line in group

New `SuppressionRule` values: `ClassificationInternal`, `ClassificationCustomService`

### `SurfacedException` evidence (optional enhancement)

Add evidence field `Classification` with value + rule basis for operator visibility (FR-019).

## `ProductClassification` vs `ReconciliationItemClassification`

| `ProductClassification` | Typical `ReconciliationItemClassification` |
|---------------------------|------------------------------------------|
| `Csp` | `MicrosoftCsp` (when truth/price signals confirm) |
| `NonCsp` | `NonCspSupplier` |
| `Csp` + internal customer | `Internal` (CR-1 wins) |
| `Csp` + Stripe-only service | `CustomService` |

## Backward Compatibility

| Scenario | Behaviour |
|----------|-----------|
| Tests without `ClassificationContext` | Unchanged 004 behaviour |
| `IncludeNonCspProducts` option | Still honoured; classification provides finer control |
| 005 exception mapping | `NonCspManualReview` still maps from non-CSP classification or description |

## API Orchestration (typical)

```
classifications = await classificationService.ClassifyAsync(inputs, scope, ct);
run = reconciliationEngine.Execute(request with { Inputs = inputs, Classifications = classifications });
viewModel = exceptionSurfacing.Surface(run, options, classifications);
```

## Future UI (out of scope)

Blazor classification review will consume:
- `ItemClassification` from API
- Override endpoints per [classification-pipeline.md](./classification-pipeline.md)
- Fluent UI Blazor components per project skill when UI feature is scheduled
