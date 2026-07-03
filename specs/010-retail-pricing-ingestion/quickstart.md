# Quickstart: Retail Pricing Ingestion Validation

**Feature**: `010-retail-pricing-ingestion`  
**Prerequisites**: [.NET 10 SDK](https://dotnet.microsoft.com/download), Git, Azurite (via Aspire AppHost emulator), sanitized `ResellerPricingVsRRP.csv` fixtures

## Prerequisites

```powershell
dotnet --version   # Expect 10.x
```

Place sanitized CSV fixtures under `tests/fixtures/reseller-pricing/` (obtain from production export before header map lock-in — see [research.md](./research.md) R15).

Start Aspire AppHost (Azurite tables + blobs):

```powershell
cd D:\repos\markheydon\billdrift-web
dotnet run --project src/BillDrift.AppHost
```

## Solution Layout (after implementation)

```text
BillDrift.sln
src/
  BillDrift.Application/Import/
  BillDrift.Application/Import/RetailPricing/
  BillDrift.Application/Normalization/PriceListNormalizer.cs
  BillDrift.Infrastructure/Import/Giacom/RetailPricing/
  BillDrift.Infrastructure/Ingestion/          # extended blob + table stores
  BillDrift.Api/Imports/RetailPricingImportEndpoints.cs
tests/
  BillDrift.Infrastructure.Tests/Import/Giacom/RetailPricing/
  BillDrift.Application.Tests/Normalization/PriceListNormalizerTests.cs
  fixtures/reseller-pricing/
```

## Build

```powershell
dotnet build BillDrift.sln --configuration Release
```

**Expected**: Build succeeds; CsvHelper reused from existing Infrastructure reference.

## Run Parser Tests (no Azure)

```powershell
dotnet test tests/BillDrift.Infrastructure.Tests --configuration Release --filter "FullyQualifiedName~RetailPricing" --verbosity normal
```

**Expected**: All tests pass, including:
- Full catalogue golden-file extraction
- Deterministic re-parse (identical output twice)
- Column-order variant fixture
- Partial-success fixture (5% bad rows — SC-006)
- Duplicate commercial key last-wins warning
- End-of-sale status preserved with RRP
- Platform NCE/Legacy mapping
- Missing commercial key skip

## Run Normalizer + Resolver Tests

```powershell
dotnet test tests/BillDrift.Application.Tests --configuration Release --filter "FullyQualifiedName~PriceList" --verbosity normal
```

**Expected**:
- Catalogue row → `PriceSource.Catalogue`, `ProductClassification.Csp`
- Manual override → `PriceSource.ManualOverride`, `ProductClassification.NonCsp`
- Override beats catalogue for same key (SC-003)

## Run Storage Integration Tests (Azurite)

```powershell
dotnet test tests/BillDrift.Infrastructure.Tests --configuration Release --filter "Category=Integration&FullyQualifiedName~Ingestion" --verbosity normal
```

**Expected**: Retail pricing blob upload + table index round-trip when Azurite available.

## Manual Validation Scenarios

### Scenario 1: Full Catalogue Import (US1)

1. POST `tests/fixtures/reseller-pricing/reseller-pricing-sample-a.csv` to `/api/imports/retail-pricing`.
2. GET `/api/imports/retail-pricing/{ingestionId}/resolved-prices`.

**Pass**:
- `Status = Success`
- Row count matches valid CSV rows
- Each record has `CommercialKey`, wholesale, RRP, status
- Platform present when column populated

### Scenario 2: Default RRP Strategy (US2)

1. Ingest catalogue-only fixture (no manual overrides).
2. Inspect `resolved-prices.json` via API.

**Pass**:
- Every resolved record `Source = Catalogue`
- `Rrp` equals catalogue RRP from CSV
- End-of-sale rows still present with RRP

### Scenario 3: Manual Override for Missing Product (US3)

1. POST catalogue fixture **without** offer `BESPOKE-001`.
2. Include `manualOverrides` JSON with RRP for `BESPOKE-001`.

**Pass**:
- Resolved prices include bespoke key
- `Source = ManualOverride`, `Classification = NonCsp`
- `OverrideWinsCount = 0` (no catalogue conflict)

### Scenario 4: Override Wins Over Catalogue (US3)

1. POST catalogue containing offer `OFFER-MS365-BB`.
2. Include manual override for same offer/SKU/term/frequency with different RRP.

**Pass**:
- Resolved RRP = manual value
- `OverrideWinsCount >= 1`
- `PricingResolutionDetail` shows `WinningSource = ManualOverride`

### Scenario 5: Commercial Key Normalisation (US4)

1. Ingest fixture with mixed-case offer/SKU and whitespace.

**Pass**:
- Normalised keys match trimmed uppercase correlation form
- Raw values traceable in source reference

### Scenario 6: Margin Fields for Reconciliation (US5)

1. Ingest fixture with margin columns populated.

**Pass**:
- Wholesale, RRP, margin, margin percent on output records
- Blank margin columns → absent (not invented)

### Scenario 7: Partial Import (US6)

1. Ingest `partial-bad-rows.csv` (valid + invalid monetary values).

**Pass**:
- `Status = PartialSuccess`
- Valid rows in `resolved-prices`
- Skip log entries with row numbers and reasons
- Import not aborted

### Scenario 8: Unrecognisable File

1. POST a CSV missing mandatory headers.

**Pass**:
- `Status = Failed`
- No resolved prices
- Table index shows `Failed` with reason

### Scenario 9: Reconciliation Consumer Smoke Test

1. Load resolved prices from ingestion into reconciliation test harness fixture.
2. Run price mismatch scenario (`tests/fixtures/reconciliation/price-mismatch.json`).

**Pass**:
- `StripePriceRrpMismatch` detected when Stripe amount ≠ intended RRP (SC-004)

### Scenario 10: Run History Snapshot (008 integration)

1. Complete retail pricing ingestion.
2. Start reconciliation run referencing ingestion fingerprint.

**Pass**:
- `InputDomainType.IntendedPricing` snapshot references `resolved-prices.json` blob path

## Performance Check (SC-001)

```powershell
# After implementation: ingest 500+ row fixture; measure wall time < 30s parser+persist
```

## Contract References

| Topic | Document |
|-------|----------|
| Pipeline stages | [contracts/csv-ingestion-pipeline.md](./contracts/csv-ingestion-pipeline.md) |
| Header aliases | [contracts/reseller-pricing-header-map.md](./contracts/reseller-pricing-header-map.md) |
| Pricing strategy | [contracts/pricing-strategy-rules.md](./contracts/pricing-strategy-rules.md) |
| Blob layout | [contracts/azure-blob-ingestion-archive.md](./contracts/azure-blob-ingestion-archive.md) |
| Table index | [contracts/azure-table-ingestion-index.md](./contracts/azure-table-ingestion-index.md) |
| Data model | [data-model.md](./data-model.md) |

## Out of Scope for This Quickstart

- Blazor upload UI
- Stripe catalogue write actions
- SQL database verification (must not exist)
