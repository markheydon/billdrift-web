# Implementation Plan: Retail Pricing and Pricing Strategy Ingestion

**Branch**: `010-retail-pricing-ingestion` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/010-retail-pricing-ingestion/spec.md`

## Summary

Implement a Giacom `ResellerPricingVsRRP.csv` ingestion pipeline that extracts intended retail pricing (wholesale, RRP, margin, status, platform), accepts manual RRP overrides for bespoke/non-catalogue products, applies the **default charge-RRP pricing strategy** (manual override wins on conflict), and emits resolved `IntendedPrice` records for Stripe catalogue validation and margin analysis. Parsing uses CsvHelper with a header alias map; normalization via `PriceListNormalizer`; merge via existing `IntendedPriceResolver`. Persist uploads and results using **Azure Blob Storage** (source CSV, raw rows, catalogue/manual/resolved price JSON, manifest) and **Azure Table Storage** (ingestion run index) via Aspire-injected `BlobServiceClient` and `TableServiceClient` only — **no SQL**.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: CsvHelper (existing); `Azure.Data.Tables`, `Azure.Storage.Blobs` (Infrastructure); `System.Text.Json` source-gen serializers  
**Storage**: Azure Blob Storage — `ingestion-uploads` container (source CSV, `manual-overrides.json`, raw/resolved price payloads, manifest). Azure Table Storage — `ingestionruns` table (partition `GiacomPriceList`). **No SQL.** Clients via Aspire DI (`BlobServiceClient`, `TableServiceClient`) in API/Infrastructure only — no manual connection string construction  
**Testing**: xUnit + FluentAssertions; CSV golden-file tests (parser, no Azure); `InMemoryIngestion*` store extensions for unit tests; Azurite integration tests; normalizer + resolver unit tests  
**Target Platform**: Azure (Aspire AppHost + Azurite locally)  
**Project Type**: Modular .NET Aspire solution — Application contract + Infrastructure parser + extended ingestion stores + API upload endpoint  
**Performance Goals**: Ingest 500+ catalogue rows in <30 seconds including persist (SC-001); parser-only <15s for 500 rows  
**Constraints**: GBP default currency; deterministic output for identical CSV bytes + override set; partial row success; manual override classified NonCsp; Aspire DI storage clients only; Web calls API only (no storage clients in `BillDrift.Web`)  
**Scale/Scope**: Single-tenant reseller; one catalogue CSV type; optional manual override JSON per upload; monthly price list refresh

### Dependency on 001-billing-domain-model

| Artifact | Usage |
|----------|-------|
| `RawPriceListRow` | Extended with `PlatformRaw`, `CurrencyRaw` |
| `RawManualPriceEntry` | Unchanged; manual override input |
| `IntendedPrice` | Extended with `Platform`, `Classification` |
| `ImportSourceKind.GiacomPriceList`, `ManualPriceEntry` | Source kinds on records |
| `CommercialKey`, `Term`, `BillingFrequency` | Correlation keys; extend `Term` with `Triennial` |
| `PriceListStatus`, `PriceSource`, `ProductClassification` | Status, source, bespoke classification |
| `IPriceListNormalizer` | Implemented in this feature |
| `IIntendedPriceResolver` / `IntendedPriceResolver` | Pricing strategy merge (existing) |
| `contracts/normalization.md` | Money/status mapping rules |

### Dependency on 003-stripe-csv-ingestion / 009-giacom-subscription-csv

| Pattern | Reuse |
|---------|-------|
| `IngestionOutcomeStatus`, `IngestionLogEntry`, `IngestionFailureReason` | Shared diagnostics (extend enum) |
| SHA-256 `SourceDocumentId` | Per-file content fingerprint |
| Header alias map pattern | `ResellerPricingCsvHeaderMap` |
| Application/Infrastructure split | Identical layering |
| Partial success + structured logging | Operator trust model |
| Azure blob archive + table index | **Extend** 009 stores (not duplicate) |
| `RetailPricingIngestionService` | Mirror `SubscriptionManagementIngestionService` orchestration |

### Dependency on 004-reconciliation-engine / 008-reconciliation-run-history

| Artifact | Usage |
|----------|-------|
| `ReconciliationInputs.IntendedPrices` | Consumes `ResolvedPrices` from ingestion |
| `IntendedPriceIndex` | Built from resolved prices at reconciliation |
| `InputDomainType.IntendedPricing` | Run archive consumes `resolved-prices.json` blob |
| `PricingDriftAnalyzer` | Uses `PriceSource` for RRP vs override timeline |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Code Quality & Maintainability | ✅ PASS | Parser in `Infrastructure.Import.Giacom.RetailPricing`; stores extended in `Infrastructure.Ingestion`; typed Application contracts |
| II. Testing Standards | ✅ PASS | Golden-file CSV fixtures; pricing strategy precedence tests; Azurite store integration; normalizer unit tests |
| III. Consistent User Experience | ✅ PASS | Log reason codes align with 002/003/009; API returns structured summary with override resolution counts |
| IV. Security by Design | ✅ PASS | File size limits; no secrets in logs; Aspire DI storage; private blob container |
| V. Billing Accuracy & Human Control | ✅ PASS | Ingestion only — no Stripe writes; intended pricing is operator-supplied reference; manual overrides explicit and audited |
| VI. Pragmatic Simplicity | ✅ PASS | Reuse CsvHelper + existing resolver; extend 009 stores; in-memory fakes for tests; no SQL; no generic ingestion abstraction |

### Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I | ✅ PASS | Six contracts + data model; pricing strategy externalized |
| II | ✅ PASS | quickstart.md defines 10 validation scenarios + SC mapping |
| III | ✅ PASS | API list/detail/resolved-prices endpoints for operator history |
| IV | ✅ PASS | Table/Blob clients injected only in Infrastructure |
| V | ✅ PASS | Re-import produces identical `RawImportId` keys; no invented prices for catalogue gaps |
| VI | ✅ PASS | Extended existing stores vs parallel Azure implementations; concrete `PriceListNormalizer` |

**Gate result**: PASS — proceed to `/speckit-tasks`.

## Project Structure

### Documentation (this feature)

```text
specs/010-retail-pricing-ingestion/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── csv-ingestion-pipeline.md
│   ├── reseller-pricing-header-map.md
│   ├── pricing-strategy-rules.md
│   ├── azure-blob-ingestion-archive.md
│   └── azure-table-ingestion-index.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
BillDrift.sln
src/
├── BillDrift.Application/
│   ├── Import/
│   │   ├── IResellerPricingCsvIngester.cs              # ★ Public parser contract
│   │   ├── RetailPricingCsvIngestionRequest.cs
│   │   ├── RetailPricingCsvIngestionResult.cs
│   │   ├── ManualPriceOverrideRequest.cs
│   │   └── IngestionEnums.cs                           # Extend failure reasons
│   ├── Import/RetailPricing/
│   │   ├── IRetailPricingIngestionService.cs           # ★ Upload + persist orchestration
│   │   └── RetailPricingIngestionService.cs
│   ├── Ingestion/
│   │   ├── IIngestionBlobStore.cs                      # Extended for retail pricing
│   │   ├── IIngestionRunIndexStore.cs                  # Extended for retail pricing
│   │   └── RetailPricingIngestionRun.cs
│   └── Normalization/
│       ├── PriceListNormalizer.cs                      # ★ IPriceListNormalizer impl
│       └── IStripeBillingNormalizer.cs                 # IntendedPriceResolver (existing)
├── BillDrift.Infrastructure/
│   ├── Import/Giacom/RetailPricing/
│   │   ├── ResellerPricingCsvIngester.cs               # ★ Pipeline orchestrator
│   │   ├── ResellerPricingCsvHeaderMap.cs
│   │   ├── ResellerPricingCsvRowReader.cs
│   │   ├── PlatformClassifier.cs
│   │   ├── TermFrequencyParser.cs
│   │   ├── RawPriceListRowMapper.cs
│   │   ├── RetailPricingFileIdentity.cs
│   │   ├── RetailPricingIngestionLimits.cs
│   │   └── Internal/
│   │       └── ParsedResellerPricingRow.cs
│   ├── Import/Giacom/
│   │   └── GiacomImportServiceCollectionExtensions.cs  # Register price list ingester
│   └── Ingestion/
│       ├── AzureBlobIngestionArchiveStore.cs           # Extended — BlobServiceClient via DI
│       ├── AzureTableIngestionRunIndexStore.cs         # Extended — TableServiceClient via DI
│       ├── InMemoryIngestionBlobStore.cs               # Extended for tests
│       ├── InMemoryIngestionRunIndexStore.cs           # Extended for tests
│       └── IngestionServiceCollectionExtensions.cs     # Register retail pricing service
├── BillDrift.Domain/
│   ├── Import/RawPriceListRow.cs                       # Extended fields
│   ├── Billing/IntendedPrice.cs                        # Extended fields
│   └── Common/
│       ├── Term.cs                                     # + Triennial
│       └── PricingPlatform.cs                          # New enum
└── BillDrift.Api/
    ├── Program.cs
    └── Imports/
        └── RetailPricingImportEndpoints.cs             # ★ POST/GET upload API

tests/
├── BillDrift.Infrastructure.Tests/
│   └── Import/Giacom/RetailPricing/
│       ├── ResellerPricingCsvIngesterTests.cs
│       ├── PlatformClassifierTests.cs
│       ├── TermFrequencyParserTests.cs
│       └── GoldenFileComparer.cs
├── BillDrift.Application.Tests/
│   └── Normalization/
│       └── PriceListNormalizerTests.cs
└── fixtures/
    └── reseller-pricing/                               # ★ NEW — sanitized CSV fixtures
        ├── reseller-pricing-sample-a.csv
        ├── column-variant.csv
        ├── partial-bad-rows.csv
        ├── duplicate-keys.csv
        └── expected/
            └── sample-a.json
```

**Structure Decision**: Mirror 009 parser architecture and extend shared Azure ingestion stores for the price-list source kind. Parser tests run stream-only without Azure. Upload workflow adds thin API + orchestration service. Domain types extended minimally for platform and classification on `IntendedPrice`.

## Complexity Tracking

> No constitution violations requiring justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Phase 0: Research

**Status**: ✅ Complete — see [research.md](./research.md)

Key decisions:
- Reuse CsvHelper (R1)
- SHA-256 content hash as `SourceDocumentId` (R2)
- Header alias registry (R3)
- Extend `Term` with Triennial; term/frequency parser (R4)
- `PricingPlatform` enum + classifier (R5)
- `PriceListStatus` mapping (R6)
- GBP money parsing (R7)
- `IntendedPriceResolver` merge for pricing strategy (R8)
- Multipart CSV + optional manual-overrides JSON (R9)
- Extend 009 Azure stores via Aspire DI — no SQL (R10)
- Implement `PriceListNormalizer` (R11)
- Last-row-wins duplicate catalogue keys (R12)
- Extend ingestion interfaces explicitly — no generic abstraction (R13)
- Wire to reconciliation + run history (R14)
- Production CSV sample required before header lock-in (R15)

## Phase 1: Design

**Status**: ✅ Complete

| Artifact | Path |
|----------|------|
| Data model | [data-model.md](./data-model.md) |
| Pipeline contract | [contracts/csv-ingestion-pipeline.md](./contracts/csv-ingestion-pipeline.md) |
| Header map | [contracts/reseller-pricing-header-map.md](./contracts/reseller-pricing-header-map.md) |
| Pricing strategy | [contracts/pricing-strategy-rules.md](./contracts/pricing-strategy-rules.md) |
| Blob archive | [contracts/azure-blob-ingestion-archive.md](./contracts/azure-blob-ingestion-archive.md) |
| Table index | [contracts/azure-table-ingestion-index.md](./contracts/azure-table-ingestion-index.md) |
| Validation quickstart | [quickstart.md](./quickstart.md) |

### Design Invariants

1. **Aspire DI only**: `AzureBlobIngestionArchiveStore(BlobServiceClient, …)` and `AzureTableIngestionRunIndexStore(TableServiceClient, …)` — never `new BlobServiceClient(connectionString)`.
2. **Blob first, manifest last**: Write result payloads, then `manifest.json` as commit marker (009 pattern).
3. **Parser pure**: `IResellerPricingCsvIngester` has no storage dependency; orchestration composes parser + normalizer + resolver + stores.
4. **No Web storage access**: Upload UI (future) posts to API only.
5. **No SQL**: Ingestion index in Table Storage; all payloads in Blob Storage.
6. **Strategy in Application**: `IntendedPriceResolver` owns precedence; parser does not embed business rules.
7. **Manual = NonCsp**: Manual overrides always `ProductClassification.NonCsp`; catalogue rows default `Csp`.
8. **No invented prices**: Keys without catalogue or override absent from `ResolvedPrices`.

## Phase 2: Implementation Tasks

**Status**: Pending — run `/speckit-tasks` to generate [tasks.md](./tasks.md)

Expected task groups:
1. Extend domain types (`Term.Triennial`, `PricingPlatform`, `RawPriceListRow`, `IntendedPrice`)
2. Extend `IngestionFailureReason` enum
3. Define Application import + retail pricing orchestration contracts
4. Implement header map, row reader, platform classifier, term/frequency parser
5. Implement CSV ingester pipeline + raw row mapper
6. Implement `PriceListNormalizer`
7. Extend Azure blob archive + table index stores for retail pricing (Aspire DI)
8. Extend in-memory store fakes for tests
9. Implement `RetailPricingIngestionService` (parse → normalize → resolve → persist)
10. Add API upload/list/detail/resolved-prices endpoints
11. Register DI in Infrastructure + Api `Program.cs`
12. Add CSV fixtures + golden-file + pricing strategy precedence tests
13. Wire `InputSnapshotMetadata` for `IntendedPricing` domain (008 consumption)

## Out of Scope (this feature)

- Giacom PDF or Subscription Management retroactive changes
- Stripe CSV ingestion changes
- Reconciliation engine rule changes (consumes output via existing `ReconciliationInputs`)
- Blazor upload UI (API-ready; UI deferred)
- Offer/SKU canonical product mapping table
- Stripe corrective actions or catalogue mutation
- SQL database of any kind
- Multi-currency support beyond GBP in v1

## User Constraints (Applied)

- BillDrift v1 uses **Azure Blob Storage** and **Azure Table Storage** exclusively for ingestion persistence — **no SQL**
- Storage implemented via Aspire-provided DI-injected `BlobServiceClient` and `TableServiceClient` — no manual connection string construction unless a documented exceptional case arises (none identified)

## Next Steps

1. `/speckit-tasks` — generate dependency-ordered implementation tasks
2. Obtain sanitized production `ResellerPricingVsRRP.csv` exports (minimum 2 variants) before header map lock-in
3. `/speckit-implement` — build parser, normalizer, extended Azure stores, and regression tests
