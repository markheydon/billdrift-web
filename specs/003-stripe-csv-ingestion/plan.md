# Implementation Plan: Stripe Billing CSV Ingestion

**Branch**: `003-stripe-csv-ingestion` | **Date**: 2026-07-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-stripe-csv-ingestion/spec.md`

## Summary

Implement a Stripe billing CSV ingestion pipeline that reads manual dashboard exports (`subscriptions.csv`, `products.csv`, `prices.csv`) and emits domain raw import records: `RawStripeSubscriptionItem`, `RawStripeProduct`, `RawStripePrice`, plus supporting `RawStripeCustomer` and `RawStripeSubscription` rows extracted from the subscriptions export. Parsing lives in `BillDrift.Infrastructure.Import.Stripe` using header-mapped CSV reading (CsvHelper); the public contract lives in `BillDrift.Application.Import`. The pipeline applies default active-subscription filtering, parses mapping metadata (Mex ID, offer ID, SKU ID, supplier references), tolerates partial row failures, and produces deterministic idempotency keys. Normalization to `StripeBillingItem` via `IStripeBillingNormalizer` is out of scope for this feature.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: CsvHelper (header-mapped CSV parsing); BCL only in Application contract layer  
**Storage**: N/A — in-memory stream/file path input; blob upload deferred to infrastructure feature  
**Testing**: xUnit + FluentAssertions; CSV fixture regression suite in `BillDrift.Infrastructure.Tests`  
**Target Platform**: Azure (Aspire solution); parser runs anywhere .NET 10 runs  
**Project Type**: Modular .NET Aspire solution — Infrastructure parser + Application import contract  
**Performance Goals**: Ingest 1,000+ subscription items + catalogue in <1 minute (SC-001); typical monthly bundle <10s  
**Constraints**: Deterministic output for identical CSV bytes (SC-004); open-source dependencies only; row failures isolated (constitution II); no Stripe API calls in MVP  
**Scale/Scope**: Single-tenant reseller; 3 CSV file types; header alias map for export column drift; subscriptions-only ingest supported

### Dependency on 001-billing-domain-model

| Artifact | Usage |
|----------|-------|
| `RawStripeSubscriptionItem`, `RawStripeProduct`, `RawStripePrice`, `RawStripeCustomer`, `RawStripeSubscription` | Pipeline output types (extend with `RawImportId` + row trace fields) |
| `ImportSourceKind.StripeExport` | Source kind on every emitted record |
| `StripeMappingMetadata` | Target shape for parsed metadata (normalization stage) |
| `contracts/normalization.md` | Metadata key aliases, interval mapping, normalizer join rules |

### Dependency on 002-giacom-pdf-ingestion

| Pattern | Reuse |
|---------|-------|
| `IngestionOutcomeStatus`, `IngestionLogEntry` | Shared ingestion diagnostics model |
| SHA-256 `SourceDocumentId` | Per-file content fingerprint |
| Partial success + structured logging | Same operator trust model |
| Application/Infrastructure split | Identical layering |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Code Quality & Maintainability | ✅ PASS | Parser isolated in `BillDrift.Infrastructure.Import.Stripe`; typed Application contract; header map externalized for column drift |
| II. Testing Standards | ✅ PASS | Integration tests with representative CSV fixtures per file type; golden-file comparison for determinism |
| III. Consistent User Experience | ✅ N/A | No UI; log reason codes align with Giacom ingestion (`partial success`, `metadata missing`) |
| IV. Security by Design | ✅ PASS | File size limits at intake; no secrets in CSV logs; snippets capped; no Stripe API credentials required for MVP |
| V. Billing Accuracy & Human Control | ✅ PASS | Ingestion only — no Stripe writes; Stripe treated as billing source of truth input; metadata gaps logged not invented |

### Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I | ✅ PASS | Pipeline stages documented in contract; parse-stage types internal to Infrastructure |
| II | ✅ PASS | quickstart.md defines fixture-based validation; golden JSON per bundle |
| III | ✅ N/A | — |
| IV | ✅ PASS | `IngestionLogEntry` excludes full row content; metadata values in snippets redacted when sensitive |
| V | ✅ PASS | Re-import produces identical `RawImportId` keys; status filter counts visible in summary |

**Gate result**: PASS — proceed to `/speckit-tasks`.

## Project Structure

### Documentation (this feature)

```text
specs/003-stripe-csv-ingestion/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── csv-ingestion-pipeline.md
│   └── stripe-csv-header-map.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
BillDrift.sln
src/
├── BillDrift.Application/
│   └── Import/
│       ├── IStripeBillingCsvIngester.cs       # ★ Public ingestion contract
│       ├── StripeCsvIngestionRequest.cs       # ★ Input bundle + options
│       ├── StripeCsvIngestionResult.cs        # ★ Result + summary types
│       └── IngestionEnums.cs                  # Extend failure reasons for CSV
├── BillDrift.Infrastructure/
│   └── Import/
│       └── Stripe/
│           ├── StripeBillingCsvIngester.cs    # ★ Pipeline orchestrator
│           ├── StripeCsvHeaderMap.cs           # Column alias registry
│           ├── StripeCsvRowReader.cs           # CsvHelper wrapper
│           ├── SubscriptionsCsvParser.cs
│           ├── ProductsCsvParser.cs
│           ├── PricesCsvParser.cs
│           ├── StripeMetadataParser.cs
│           ├── StripeStatusFilter.cs
│           ├── RawStripeRecordMapper.cs
│           └── Internal/                      # Parse-only row DTOs
│               ├── ParsedSubscriptionRow.cs
│               ├── ParsedProductRow.cs
│               └── ParsedPriceRow.cs
└── BillDrift.Domain/
    └── Import/Stripe/                         # Extend raw types with RawImportId

tests/
├── BillDrift.Infrastructure.Tests/
│   └── Import/
│       └── Stripe/
│           ├── StripeBillingCsvIngesterTests.cs
│           ├── StripeMetadataParserTests.cs
│           ├── StripeStatusFilterTests.cs
│           └── GoldenFileComparer.cs          # Reuse or share with Giacom tests
└── fixtures/
    └── stripe-csv/                          # ★ NEW — sanitized CSV fixtures
        ├── subscriptions-sample-a.csv
        ├── products-sample-a.csv
        ├── prices-sample-a.csv
        ├── subscriptions-column-variant.csv
        └── expected/
            └── bundle-sample-a.json
```

**Structure Decision**: Mirror 002 architecture — parsers in Infrastructure, contracts in Application, domain raw types extended minimally (`RawImportId`, optional `SourceRowNumber`). Reuse existing `BillDrift.Infrastructure.Tests` project. Internal parse row types stay in Infrastructure and are not referenced by Application or Domain.

## Complexity Tracking

> No constitution violations requiring justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Phase 0: Research

**Status**: ✅ Complete — see [research.md](./research.md)

Key decisions:
- CsvHelper for header-mapped CSV parsing (R1)
- SHA-256 per-file content hash as `SourceDocumentId` (R2)
- Header alias registry for Stripe dashboard column drift (R3)
- Subscriptions export at subscription-item row granularity (R4)
- Metadata column patterns: `metadata[key]` and flat `Metadata Key` columns (R5)
- Default active status set: `active`, `trialing`, `past_due` (R6)
- Line key: Stripe object ID when present, else `{fileHash}:{rowNumber}` (R7)
- Extend shared `IngestionFailureReason` with CSV-specific codes (R8)

## Phase 1: Design

**Status**: ✅ Complete

| Artifact | Path |
|----------|------|
| Data model | [data-model.md](./data-model.md) |
| Pipeline contract | [contracts/csv-ingestion-pipeline.md](./contracts/csv-ingestion-pipeline.md) |
| Header map contract | [contracts/stripe-csv-header-map.md](./contracts/stripe-csv-header-map.md) |
| Validation quickstart | [quickstart.md](./quickstart.md) |

## Phase 2: Implementation Tasks

**Status**: Pending — run `/speckit-tasks` to generate [tasks.md](./tasks.md)

Expected task groups:
1. Add CsvHelper package reference to Infrastructure
2. Extend `IngestionFailureReason` with Stripe CSV codes
3. Define Application import contract, request, and result types
4. Implement header alias map and CSV row reader
5. Implement subscriptions, products, and prices parsers
6. Implement metadata parser and status filter
7. Extend domain raw Stripe types with `RawImportId` + row trace fields
8. Implement raw record mapper and bundle orchestrator
9. Add CSV fixtures and golden-file integration tests
10. Register DI extension for `IStripeBillingCsvIngester`

## Out of Scope (this feature)

- Stripe API integration and webhook sync
- `IStripeBillingNormalizer` implementation (Application, separate task)
- Offer/SKU mapping and canonical product resolution
- Reconciliation engine integration
- Blazor upload UI and API upload endpoint
- Blob storage and export file lifecycle
- Automated metadata correction on Stripe records

## Next Steps

1. `/speckit-tasks` — generate dependency-ordered implementation tasks
2. Obtain sanitized production Stripe CSV exports (minimum 2 subscription variants + catalogue pair) before parser implementation
3. `/speckit-implement` — build parser and regression tests
