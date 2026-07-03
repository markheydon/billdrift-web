# Catalogue Reconciliation Pipeline

**Consumer**: `BillDrift.Application.CatalogueReconciliation`  
**Date**: 2026-07-03

## Purpose

Defines the staged pipeline for standalone Stripe catalogue reconciliation independent of customer subscription match groups.

---

## Stages (executed in order)

### 1. `ValidateInputsStage`

- Require non-empty `StripeProducts` or `StripePrices` (at least one catalogue record).
- Validate `ProductMappings` non-null (may be empty — limits scope).
- Validate `IntendedPrices` non-null.

**Failure**: Return run with validation error in summary; no exceptions fabricated.

### 2. `BuildIndexesStage`

- `StripeCatalogueSnapshotIndex.Build(products, prices)`
- `ProductMappingIndex.Build(mappings, resolver, fuzzyMatcher)` — reuse 004 index
- `IntendedPriceIndex.Build(intendedPrices)` — reuse 004 index with manual override precedence

### 3. `DetectDuplicateConflictsStage`

- Scan duplicate products by `(OfferId, SkuId)` resolution
- Scan duplicate active prices by `(ProductId, Frequency, Currency)`
- Emit `DuplicateProduct` / `DuplicatePrice` exceptions + `FlagManualCleanup` fixes

**Runs before presence checks** to avoid N duplicate missing-product noise.

### 4. `DetectUnmappedCatalogueStage`

- For each Stripe product without resolvable offer/SKU (metadata + mapping inverse lookup): `UnmappedCatalogueEntry`
- No destructive proposed fix

### 5. `ReconcileMappedProductsStage`

For each `ProductMapping` in scope (respect `IncludeNonCspProducts`):

1. If mapping confidence ambiguous → `MappingAmbiguous`, skip further checks for root
2. Resolve intended price keys for `(OfferId, SkuId)` from `IntendedPriceIndex`
3. If no intended prices → `PricingReferenceGap`
4. Resolve Stripe product (by `StripeProductId` or metadata match)
5. If missing → `MissingProduct` + `CreateProduct` fix
6. For each required `CommercialKey` (term + frequency):
   - Find active Stripe price
   - If missing → `MissingPrice` + `CreatePrice` fix
   - If present, compare RRP → `IncorrectPrice` + `CreateReplacementPrice` when mismatch

### 6. `AttachProposedFixesStage`

- Factory creates `CatalogueProposedFix` per exception type (see `catalogue-check-rules.md`)
- Duplicate/conflict: `IsActionable = false`

### 7. `OrderOutputStage`

Deterministic ordering:
1. Exception type ordinal (`Duplicate*` before `Missing*` before `Incorrect*`)
2. `OfferId`, `SkuId`, `Term`, `Frequency` string sort
3. `ExceptionId` GUID sort tie-breaker

---

## Determinism Contract

Identical `CatalogueReconciliationInputs` + `CatalogueReconciliationOptions` → byte-identical JSON serialization of exceptions and proposed fixes (stable property order via source-gen serializer).

---

## Side Effects

**None** in engine. Persistence and approval ingestion occur in `CatalogueReconciliationService` after engine returns.
