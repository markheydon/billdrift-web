# Exception Surfacing Pipeline

**Consumer**: `BillDrift.Application.Reconciliation.ExceptionSurfacing`  
**Input**: `ReconciliationRun` from `IReconciliationEngine` (feature 004)  
**Extends**: [001 reconciliation-engine contract](../../001-billing-domain-model/contracts/reconciliation-engine.md)

## Purpose

Defines the four-phase pipeline that transforms engine output into a `ReconciliationExceptionViewModel` suitable for UI, API, and export consumers.

---

## Entry Point

```text
ExceptionSurfacingService.Surface(ReconciliationRun run, ReconciliationOptions? options = null)
  → ReconciliationExceptionViewModel
```

**Preconditions**:
- `run` is non-null and fully constructed (immutable)
- `run.Inputs` snapshot available for derived detectors

**Postconditions**:
- Output is immutable
- Deterministic except `GeneratedAt` timestamp
- No side effects; no Stripe API calls
- No mutation of input `ReconciliationRun`

---

## Pipeline Phases

### Phase 1: Collect

**Responsibility**: Build raw `SurfacedException` candidates.

1. Iterate `run.Mismatches` → map each via `MismatchToExceptionMapper` (see [mismatch-to-exception-mapping.md](./mismatch-to-exception-mapping.md))
2. Run derived detectors:
   - `OrphanedStripeDetector`
   - `MexIdMismatchDetector`
   - `ProductMismatchDetector`
3. Attach `SourceMismatchIds`, initial `Severity`, `Explanation` from mismatch description or detector template
4. Build preliminary `Evidence` via `EvidenceBuilder`

**Output**: `List<SurfacedException>` candidates (may contain duplicates and dependents)

---

### Phase 2: Suppress

**Responsibility**: Remove or trim candidates made redundant by root-cause rules.

Apply rules in order (see [suppression-and-ordering-rules.md](./suppression-and-ordering-rules.md)):

1. Root-cause mapping suppression (per match group)
2. Root-cause MexId suppression
3. Low-confidence proposed action stripping (not removal of exception)
4. Catalogue-subsumed-by-subscription suppression
5. Out-of-scope inactive exclusion (orphaned detector)

Record each suppression in `SurfacingContext.Suppressed` for audit (`SuppressedCount` in summary).

**Output**: Filtered candidate list; some candidates may have `ProposedChangeId` cleared

---

### Phase 3: Consolidate

**Responsibility**: Merge catalogue-related exceptions sharing the same `CommercialKey`.

- Group candidates where `Category ∈ {StripeProductMissing, StripePriceMissing, StripePriceRrpMismatch}` AND same `CommercialKey`
- Merge evidence arrays; sum `SuppressedSiblingCount`
- Keep highest severity among merged items
- Single surviving `SurfacedExceptionId` using lexicographically first source ID for stability

**Output**: Deduplicated candidate list

---

### Phase 4: Finalize

**Responsibility**: Compute triage flags, summaries, grouping, ordering.

1. Set `RequiresActionNow` per [suppression-and-ordering-rules.md](./suppression-and-ordering-rules.md)
2. Build `ExceptionRunSummary` counts
3. Group by `Customer.MexId` → `CustomerExceptionGroup`
4. Apply customer group ordering
5. Apply within-group exception ordering
6. Construct `ReconciliationExceptionViewModel`

---

## Error Handling

| Condition | Behaviour |
|-----------|-----------|
| `run` is null | `ArgumentNullException` |
| `run.Mismatches` contains orphan `MismatchId` in `ProposedChange` | Skip invalid link; log via `Suppressed` audit (internal invariant) |
| Match group referenced by mismatch not found | Treat as ungrouped exception; evidence from mismatch fields only |

No exceptions thrown for empty mismatch sets — returns zero-count view model with `HasExceptions == false`.

---

## Determinism Contract

Given identical `ReconciliationRun` content and `ReconciliationOptions`:

- `CustomerGroups` order identical
- Each group's `Exceptions` order identical
- `SurfacedExceptionId` values identical
- Summary counts identical
- `GeneratedAt` MAY differ

Comparison helper for tests: exclude `GeneratedAt`; compare by `(Id, Category, Severity, RequiresActionNow, Explanation)`.

---

## DI Registration

```csharp
services.AddSingleton<ExceptionSurfacingService>();
```

Registered alongside `IReconciliationEngine` in Application DI extension. Web/API layers inject both services sequentially: `engine.Execute(request)` → `surfacing.Surface(run, request.Options)`.
