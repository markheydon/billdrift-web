# Quickstart: Giacom Subscription Management CSV Ingestion Validation

**Feature**: `009-giacom-subscription-csv`  
**Prerequisites**: [.NET 10 SDK](https://dotnet.microsoft.com/download), Git, Azurite (via Aspire AppHost emulator), sanitized Giacom CSV fixtures

## Prerequisites

```powershell
dotnet --version   # Expect 10.x
```

Place sanitized CSV fixtures under `tests/fixtures/subscription-management/` (obtain from production export before parser lock-in — see [research.md](./research.md) R12).

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
  BillDrift.Application/Normalization/SubscriptionManagementNormalizer.cs
  BillDrift.Infrastructure/Import/Giacom/SubscriptionManagement/
  BillDrift.Infrastructure/Ingestion/
  BillDrift.Api/Imports/
tests/
  BillDrift.Infrastructure.Tests/Import/Giacom/SubscriptionManagement/
  BillDrift.Infrastructure.Tests/Ingestion/
  fixtures/subscription-management/
```

## Build

```powershell
dotnet build BillDrift.sln --configuration Release
```

**Expected**: Build succeeds; `BillDrift.Infrastructure` uses existing CsvHelper reference.

## Run Parser Tests (no Azure)

```powershell
dotnet test tests/BillDrift.Infrastructure.Tests --configuration Release --filter "FullyQualifiedName~SubscriptionManagement" --verbosity normal
```

**Expected**: All tests pass, including:
- Full report golden-file extraction
- Deterministic re-parse (identical output twice)
- Mixed-product scope filter (Exclaimer excluded)
- Partial-success fixture (skipped row logged)
- Column-order variant fixture
- Missing commercial key warnings
- Lifecycle column capture

## Run Storage Integration Tests (Azurite)

```powershell
# Default dotnet test skips Azure storage tests in <1s when Azurite is not running.
# With Azurite up, run ingestion storage integration tests explicitly:
dotnet test tests/BillDrift.Infrastructure.Tests --configuration Release --filter "Category=Integration&FullyQualifiedName~Ingestion" --verbosity normal
```

**Expected**: Blob upload + table index round-trip passes when Azurite available; skipped quickly otherwise.

## Manual Validation Scenarios

### Scenario 1: Full Report Import

1. Place `subscription-management-sample-a.csv` in fixtures folder.
2. Run ingester via test helper with default options.
3. Compare output to `tests/fixtures/subscription-management/expected/sample-a.json`.

**Pass**:
- `Status = Success` (or `PartialSuccess` if fixture includes bad row)
- Row count matches qualifying M365 rows in CSV
- Every emitted line has non-empty Mex ID, licence count, status
- Offer ID + SKU ID present on ≥98% of M365 rows (SC-001)

### Scenario 2: Product Scope Filter

1. Ingest `mixed-products.csv` containing M365 and Exclaimer rows.

**Pass**:
- Exclaimer rows absent from output
- `Summary.RowsExcludedByScope` matches Exclaimer row count
- SC-002: 100% non-CSP excluded with logged reasons

### Scenario 3: API Upload + Persistence

```powershell
curl -X POST "https://localhost:{apiPort}/api/imports/subscription-management" `
  -F "file=@tests/fixtures/subscription-management/subscription-management-sample-a.csv"
```

**Pass**:
- Response includes `ingestionId`, `status`, `summary`
- Table row in `ingestionruns`
- Blobs under `ingestion-uploads/{ingestionId}/`
- `manifest.json` present with matching `contentFingerprint`

### Scenario 4: Commercial Key Warnings

1. Ingest fixture with rows missing offer ID or SKU ID.

**Pass**:
- Raw rows still emitted where Mex ID valid
- `CommercialKeyWarnings` > 0 in summary
- SC-005: warnings identifiable from log without opening CSV

### Scenario 5: Re-import Determinism

1. Ingest same CSV bytes twice.

**Pass**:
- Identical `SourceDocumentId`
- Identical `RawImportId` keys per row
- SC-004 satisfied

### Scenario 6: Lifecycle Columns

1. Ingest fixture with NCE, trial, end-of-term, cancellable-until populated.

**Pass**:
- `Lifecycle` fields populated on normalized lines
- SC-006: ≥95% of populated source columns reflected on output

## Performance Check

Ingest fixture with ~1,000 M365 rows:

**Pass**: Completes in <30 seconds (parser only); full upload workflow <2 minutes (SC-003).

## Final Build Gate

```powershell
dotnet clean
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

## Validation Checklist (2026-07-03)

| Scenario | Status |
|----------|--------|
| V1 Full report import (`subscription-management-sample-a.csv`) | PASS |
| V2 Product scope filter (`mixed-products.csv`) | PASS |
| V3 API upload + persistence (in-memory integration test) | PASS |
| V4 Commercial key warnings (partial-success / normalizer tests) | PASS |
| V5 Re-import determinism | PASS |
| V6 Lifecycle columns (`lifecycle-columns.csv`) | PASS |

## Related Contracts

- [csv-ingestion-pipeline.md](./contracts/csv-ingestion-pipeline.md)
- [subscription-csv-header-map.md](./contracts/subscription-csv-header-map.md)
- [product-scope-rules.md](./contracts/product-scope-rules.md)
- [azure-blob-ingestion-archive.md](./contracts/azure-blob-ingestion-archive.md)
- [azure-table-ingestion-index.md](./contracts/azure-table-ingestion-index.md)
- [data-model.md](./data-model.md)

## Success Criteria Mapping

| SC | Scenario |
|----|----------|
| SC-001 | Scenario 1 |
| SC-002 | Scenario 2 |
| SC-003 | Performance Check |
| SC-004 | Scenario 5 |
| SC-005 | Scenario 4 |
| SC-006 | Scenario 6 |
