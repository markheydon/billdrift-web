# Quickstart: Giacom PDF Ingestion Validation

**Feature**: `002-giacom-pdf-ingestion`  
**Prerequisites**: [.NET 10 SDK](https://dotnet.microsoft.com/download), Git, sanitized Giacom PDF fixtures

This guide validates the PDF ingestion pipeline once implementation tasks are complete.

## Prerequisites

```powershell
dotnet --version   # Expect 10.x
```

Place sanitized PDF fixtures under `tests/fixtures/giacom-pdf/` (not committed until obtained — see plan.md).

## Solution Layout (after implementation)

```text
BillDrift.sln
src/
  BillDrift.Application/Import/
  BillDrift.Infrastructure/Import/Giacom/
tests/
  BillDrift.Infrastructure.Tests/
  fixtures/giacom-pdf/
```

## Build

```powershell
cd D:\repos\markheydon\billdrift-web
dotnet build BillDrift.sln --configuration Release
```

**Expected**: Build succeeds; `BillDrift.Infrastructure` references PdfPig.

## Run Parser Tests

```powershell
dotnet test tests/BillDrift.Infrastructure.Tests --configuration Release --verbosity normal
```

**Expected**: All tests pass, including:
- Pre-billing golden-file extraction
- Post-billing golden-file extraction
- Deterministic re-parse (identical output twice)
- Partial-success fixture (skipped line logged, valid lines emitted)
- Encrypted PDF rejection

## Manual Validation Scenarios

### Scenario 1: Pre-Billing Full Extract

1. Open `tests/fixtures/giacom-pdf/pre-billing-sample-a.pdf`.
2. Run ingester via test helper or CLI stub.
3. Compare output to `tests/fixtures/giacom-pdf/expected/pre-billing-sample-a.json`.

**Pass**:
- `Status = Success` (or `PartialSuccess` if fixture includes bad row)
- Line count matches manual PDF line count ±0
- Every line has non-empty `MexIdRaw`, `ProductNameRaw`, `QuantityRaw`, `LineCostRaw`
- `ReportType = PreBilling`

### Scenario 2: Post-Billing Report Type

1. Ingest `post-billing-sample-a.pdf`.

**Pass**:
- `ReportType = PostBilling`
- Line structure equivalent to pre-billing fixture for same customer

### Scenario 3: Wrapped Product Name

1. Ingest fixture containing multi-row product name.
2. Find line in output for that product.

**Pass**:
- Single line with full product name concatenated
- `ProductNameRaw` matches expected golden string character-for-character (SC-005)

### Scenario 4: Partial Success

1. Ingest fixture with one corrupt line (missing quantity).

**Pass**:
- `Status = PartialSuccess`
- Valid lines present in `Lines`
- Skip log entry with `QuantityUnparseable` and `Location.LineIndex` set
- `Summary.LinesSkipped >= 1`

### Scenario 5: Determinism

1. Ingest same PDF twice in one test.
2. Deep-compare `Lines` and each `RawImportId`.

**Pass**: Outputs identical (SC-004).

### Scenario 6: Downstream Handoff

1. Take first line from ingestion result.
2. Pass to `IGiacomBillingNormalizer.Normalize` (when implemented).

**Pass**:
- Normalizer accepts line without modification to raw fields
- `RawImportId.SourceLineKey` matches supplier reference from PDF when present

## Fixture Requirements

| Fixture | Validates |
|---------|-----------|
| `pre-billing-sample-a.pdf` | FR-002, FR-005–FR-009, SC-002 |
| `pre-billing-sample-b.pdf` | FR-012 format variant |
| `post-billing-sample-a.pdf` | FR-002, FR-027 |
| `post-billing-sample-b.pdf` | FR-012 post-billing variant |
| `partial-success-sample.pdf` | FR-018, SC-003 |
| `encrypted-sample.pdf` | Document fail, FR-020 edge case |

Golden JSON files contain arrays of expected line field objects (not full `RawImportId` if timestamps differ — compare business fields and line keys separately).

## Related Artifacts

- [data-model.md](./data-model.md) — result and log types
- [contracts/pdf-ingestion-pipeline.md](./contracts/pdf-ingestion-pipeline.md) — interface guarantees
- [contracts/giacom-block-grammar.md](./contracts/giacom-block-grammar.md) — parsing rules
- [spec.md](./spec.md) — business requirements
- [../001-billing-domain-model/contracts/normalization.md](../001-billing-domain-model/contracts/normalization.md) — downstream normalizer

## Out of Scope for This Quickstart

- Blob upload and API endpoints
- Blazor file upload UI
- Full Aspire host integration test
- Normalizer implementation (separate feature task)

Parser validation is complete when `BillDrift.Infrastructure.Tests` passes with all required fixtures.
