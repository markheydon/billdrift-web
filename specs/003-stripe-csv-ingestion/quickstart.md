# Quickstart: Stripe CSV Ingestion Validation

**Feature**: `003-stripe-csv-ingestion`  
**Prerequisites**: [.NET 10 SDK](https://dotnet.microsoft.com/download), Git, sanitized Stripe CSV fixtures

This guide validates the Stripe CSV ingestion pipeline once implementation tasks are complete.

## Prerequisites

```powershell
dotnet --version   # Expect 10.x
```

Place sanitized CSV fixtures under `tests/fixtures/stripe-csv/` (not committed until obtained — see [plan.md](./plan.md)).

## Solution Layout (after implementation)

```text
BillDrift.sln
src/
  BillDrift.Application/Import/
  BillDrift.Infrastructure/Import/Stripe/
tests/
  BillDrift.Infrastructure.Tests/Import/Stripe/
  fixtures/stripe-csv/
```

## Build

```powershell
cd D:\repos\markheydon\billdrift-web
dotnet build BillDrift.sln --configuration Release
```

**Expected**: Build succeeds; `BillDrift.Infrastructure` references CsvHelper.

## Run Parser Tests

```powershell
dotnet test tests/BillDrift.Infrastructure.Tests --configuration Release --filter "FullyQualifiedName~Stripe" --verbosity normal
```

**Expected**: All tests pass, including:
- Full bundle golden-file extraction
- Deterministic re-parse (identical output twice)
- Partial-success fixture (skipped row logged, valid rows emitted)
- Mixed-status filter (canceled excluded by default)
- Column-order variant fixture
- Metadata gap warnings

## Manual Validation Scenarios

### Scenario 1: Full Export Bundle

1. Place `subscriptions-sample-a.csv`, `products-sample-a.csv`, `prices-sample-a.csv` in fixtures folder.
2. Run ingester via test helper with default options.
3. Compare output to `tests/fixtures/stripe-csv/expected/bundle-sample-a.json`.

**Pass**:
- `Status = Success` (or `PartialSuccess` if fixture includes bad row)
- Subscription item count matches manual CSV row count for active statuses
- Every item has non-empty `CustomerId`, `SubscriptionId`, `ProductId`, `PriceId`
- Products and prices counts match catalogue files

### Scenario 2: Active Status Filter

1. Ingest `subscriptions-sample-a.csv` containing active and canceled rows with default options.
2. Re-run with `IncludeInactiveSubscriptions = true`.

**Pass**:
- Default run: canceled rows absent; `Summary.SubscriptionsFilteredByStatus` > 0
- Inclusive run: canceled rows present with status preserved
- SC-005: 100% canceled excluded in default run

### Scenario 3: Metadata Warnings

1. Ingest fixture with rows missing `offer_id` or `sku_id`.

**Pass**:
- Rows still emitted in output
- `MetadataIncomplete` or `MetadataInconsistent` warnings in log
- `Summary.MetadataWarnings` matches expected count
- No invented Offer/SKU values on output records

### Scenario 4: Subscriptions Only (No Catalogue)

1. Ingest subscriptions file alone.

**Pass**:
- `Status = Success` or `PartialSuccess`
- Subscription items emitted
- `Products` and `Prices` collections empty
- No file-level failure

### Scenario 5: Catalogue Cross-Check

1. Ingest full bundle where one item references unknown `price_id`.

**Pass**:
- Item still emitted
- `CatalogueReferenceUnresolved` warning with price ID in message
- `Summary.CatalogueWarnings` incremented

### Scenario 6: Deterministic Re-Import

1. Ingest same bundle twice.

**Pass**:
- Identical `BundleId` and per-record `RawImportId` values (SC-004)
- Golden file comparison passes

## Performance Smoke Test

```powershell
dotnet test tests/BillDrift.Infrastructure.Tests --filter "FullyQualifiedName~StripePerformance" --configuration Release
```

**Pass**: 1,000-row synthetic bundle completes in < 60 seconds (SC-001).

## Troubleshooting

| Symptom | Check |
|---------|-------|
| `MandatoryHeaderMissing` | Compare headers to [stripe-csv-header-map.md](./contracts/stripe-csv-header-map.md) |
| All rows skipped | Verify `Status` column values and filter options |
| Wrong quantity | CSV locale — ensure no thousands separators in quantity column |
| Missing metadata | Confirm `metadata[key]` columns exported in Stripe dashboard |

## Next Steps

After validation passes:

1. Wire `AddStripeBillingCsvIngestion()` in AppHost/API DI
2. Implement `IStripeBillingNormalizer` (separate feature/tasks)
3. Integrate with reconciliation engine inputs
