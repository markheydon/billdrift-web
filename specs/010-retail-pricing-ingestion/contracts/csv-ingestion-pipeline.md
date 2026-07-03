# Contract: Reseller Pricing CSV Ingestion Pipeline

**Feature**: `010-retail-pricing-ingestion`  
**Implementation**: `BillDrift.Infrastructure.Import.Giacom.RetailPricing.ResellerPricingCsvIngester`

## Purpose

Defines the ordered stages from `ResellerPricingVsRRP.csv` bytes (plus optional manual overrides) to `RetailPricingCsvIngestionResult` with resolved `IntendedPrice` records.

## Public Entry Points

| Type | Location |
|------|----------|
| `IResellerPricingCsvIngester` | `BillDrift.Application.Import` |
| `IRetailPricingIngestionService` | `BillDrift.Application.Import.RetailPricing` |

Parser has **no storage dependency**. Orchestration service composes parser + normalizer + resolver + Azure stores.

## Pipeline Stages

| # | Stage | Input → Output | Failure mode |
|---|-------|----------------|--------------|
| 1 | **Bounded read** | Stream → `byte[]` | `UploadTooLarge` if exceeds `MaxFileSizeBytes` |
| 2 | **Header detection** | bytes → column map | Fail entire import if mandatory headers missing |
| 3 | **Row iteration** | CSV rows → `ParsedResellerPricingRow` | Continue |
| 4 | **Catalogue validation** | parsed row | Skip row + log if mandatory fields missing/unparseable |
| 5 | **Raw map** | parsed row → `RawPriceListRow` | Continue |
| 6 | **Manual override validation** | `ManualPriceOverrideRequest[]` | Reject individual invalid entries; continue |
| 7 | **Manual raw map** | accepted requests → `RawManualPriceEntry` | Continue |
| 8 | **Normalize catalogue** | `RawPriceListRow` → `IntendedPrice` | Skip row + `NormalizationSkipped` log |
| 9 | **Normalize manual** | `RawManualPriceEntry` → `IntendedPrice` | Skip entry + log |
| 10 | **Duplicate detection** | catalogue prices | Last wins + `DuplicateCommercialKey` warning |
| 11 | **Resolve strategy** | catalogue + manual lists | `IntendedPriceResolver` per key; build `ResolvedPrices` |
| 12 | **Summary** | counts + logs | Set `Success` / `PartialSuccess` / `Failed` |

## Status Mapping

| Condition | `IngestionOutcomeStatus` |
|-----------|--------------------------|
| All catalogue rows emitted; no skips; overrides valid | `Success` |
| At least one row/override succeeded; at least one skipped/rejected | `PartialSuccess` |
| File unrecognisable or zero successes | `Failed` |

## Determinism

Identical CSV bytes + identical manual override set → identical:
- `SourceDocumentId` (content hash)
- `RawImportId` per row
- `ResolvedPrices` ordering (stable sort by `CommercialKey`)

## Normalization Boundary

| Concern | Ingestion (faithful) | Normalization (strict) |
|---------|---------------------|----------------------|
| Missing offer/SKU | Skip row at stage 4 | N/A |
| Mixed-case IDs | Preserve in raw; trim in normalized key | `OfferId`/`SkuId` value objects |
| End of sale | Preserve status text | `PriceListStatus.EndOfSale` |
| Platform blank | Emit raw null | `PricingPlatform.Unknown` |

## Pricing Strategy (Stage 11)

For each distinct `CommercialKey` across catalogue and manual lists:

1. Collect all `IntendedPrice` matches.
2. If any `ManualOverride` exists → winner is manual RRP (`ProductClassification.NonCsp`).
3. Else if any `Catalogue` exists → winner is catalogue RRP (`ProductClassification.Csp`).
4. Else → key absent from resolved output (no invented price).

Record `PricingResolutionDetail` for keys where both sources existed (`OverrideWinsCount`).

## Dependencies

| Component | Role |
|-----------|------|
| `PriceListNormalizer` | Stages 8–9 |
| `IntendedPriceResolver` | Stage 11 |
| `ResellerPricingCsvHeaderMap` | Stage 2 |
| `PlatformClassifier` | Stage 5 (optional column) |
| `TermFrequencyParser` | Stages 4, 8 |

## Testing Hooks

| Hook | Purpose |
|------|---------|
| `NormalizeOutput = false` | Return raw rows only |
| Direct `IResellerPricingCsvIngester.Ingest` | Parser tests without Azure |

## Out of Scope

- Stripe catalogue mutation
- Product mapping (`ProductMapping`) creation
- Reconciliation run execution
- SQL persistence
