# Implementation Plan: Giacom Subscription Management CSV Ingestion

**Branch**: `009-giacom-subscription-csv` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009-giacom-subscription-csv/spec.md`

## Summary

Implement a Giacom `SubscriptionManagementReport.csv` ingestion pipeline that extracts Microsoft 365 / CSP subscription truth, normalises customer identity on **Mex ID** and product identity on **Offer ID + SKU ID**, and emits `RawSubscriptionManagementRow` plus `MicrosoftSubscriptionLine` records for reconciliation against Stripe. Parsing uses CsvHelper with a header alias map; product scope filtering excludes non-M365 products (e.g., Exclaimer). Persist uploads and results using **Azure Blob Storage** (source CSV + JSON payloads) and **Azure Table Storage** (ingestion run index) via Aspire-injected `BlobServiceClient` and `TableServiceClient` only — **no SQL**. API multipart upload endpoint orchestrates ingest-and-persist. `SubscriptionManagementNormalizer` implemented in Application layer.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: CsvHelper (existing, from Stripe ingestion); `Azure.Data.Tables`, `Azure.Storage.Blobs` (Infrastructure); `System.Text.Json` source-gen serializers  
**Storage**: Azure Blob Storage — `ingestion-uploads` container (source CSV, raw rows JSON, subscription truth JSON, manifest). Azure Table Storage — `ingestionruns` table (queryable ingestion index). **No SQL.** Clients via Aspire DI (`BlobServiceClient`, `TableServiceClient`) in API/Infrastructure only — no manual connection string construction  
**Testing**: xUnit + FluentAssertions; CSV golden-file tests (parser, no Azure); `InMemoryIngestion*` stores for unit tests; Azurite integration tests for blob/table stores; API integration tests  
**Target Platform**: Azure (Aspire AppHost + Azurite locally)  
**Project Type**: Modular .NET Aspire solution — Application contract + Infrastructure parser + Infrastructure ingestion stores + API upload endpoint  
**Performance Goals**: Ingest 5,000 qualifying rows in <2 minutes including persist (SC-003); parser-only <30s for 1,000 rows  
**Constraints**: Microsoft 365 / CSP scope only; deterministic output for identical CSV bytes (SC-004); partial row success; Aspire DI storage clients only; Web calls API only (no storage clients in `BillDrift.Web`)  
**Scale/Scope**: Single-tenant reseller; one CSV file type; monthly export; optional lifecycle/pricing columns

### Dependency on 001-billing-domain-model

| Artifact | Usage |
|----------|-------|
| `RawSubscriptionManagementRow` | Extended with lifecycle/pricing raw fields |
| `MicrosoftSubscriptionLine` | Extended with `ProductDisplayFacts`, `SubscriptionLifecycleFacts` |
| `ImportSourceKind.GiacomSubscriptionManagement` | Source kind on every record |
| `CustomerIdentity`, `CommercialKeyRoot` | Normalized correlation keys |
| `contracts/normalization.md` | Status/term/frequency mapping; normalizer contract |
| `ISubscriptionManagementNormalizer` | Implemented in this feature |

### Dependency on 002-giacom-pdf-ingestion / 003-stripe-csv-ingestion

| Pattern | Reuse |
|---------|-------|
| `IngestionOutcomeStatus`, `IngestionLogEntry`, `IngestionFailureReason` | Shared diagnostics (extend enum) |
| SHA-256 `SourceDocumentId` | Per-file content fingerprint |
| Header alias map pattern | `SubscriptionManagementCsvHeaderMap` |
| Application/Infrastructure split | Identical layering |
| Partial success + structured logging | Operator trust model |

### Dependency on 008-reconciliation-run-history

| Artifact | Usage |
|----------|-------|
| `InputSnapshotMetadata` | Consumes ingestion fingerprints + blob paths for `SubscriptionTruth` domain |
| Blob payload shape | `subscription-truth.json` feeds run archive `inputs/subscription-truth.json` |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Code Quality & Maintainability | ✅ PASS | Parser isolated in `Infrastructure.Import.Giacom.SubscriptionManagement`; stores in `Infrastructure.Ingestion`; typed Application contracts |
| II. Testing Standards | ✅ PASS | Golden-file CSV fixtures; scope filter regression; Azurite store integration; normalizer unit tests |
| III. Consistent User Experience | ✅ PASS | Log reason codes align with 002/003; API returns structured summary |
| IV. Security by Design | ✅ PASS | File size limits; no secrets in logs; Aspire DI storage; uploaded CSV in private blob container |
| V. Billing Accuracy & Human Control | ✅ PASS | Ingestion only — no Stripe writes; subscription truth is Giacom-authoritative input; partial import with visible skips |
| VI. Pragmatic Simplicity | ✅ PASS | Reuse CsvHelper; mirror 008 storage split; in-memory stores for tests; single orchestration service |

### Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I | ✅ PASS | Five contracts + data model; scope rules externalized |
| II | ✅ PASS | quickstart.md defines 6 validation scenarios + SC mapping |
| III | ✅ PASS | API list/detail endpoints for operator upload history |
| IV | ✅ PASS | Table/Blob clients injected only in Infrastructure; no Web storage access |
| V | ✅ PASS | Re-import produces identical `RawImportId` keys; scope exclusions logged not silent |
| VI | ✅ PASS | No SQL; no new CSV library; normalizer in Application (concrete class, no extra interface beyond 001 stub) |

**Gate result**: PASS — proceed to `/speckit-tasks`.

## Project Structure

### Documentation (this feature)

```text
specs/009-giacom-subscription-csv/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── csv-ingestion-pipeline.md
│   ├── subscription-csv-header-map.md
│   ├── product-scope-rules.md
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
│   │   ├── ISubscriptionManagementCsvIngester.cs       # ★ Public parser contract
│   │   ├── SubscriptionManagementCsvIngestionRequest.cs
│   │   ├── SubscriptionManagementCsvIngestionResult.cs
│   │   └── IngestionEnums.cs                           # Extend failure reasons
│   ├── Import/SubscriptionManagement/
│   │   ├── ISubscriptionManagementIngestionService.cs  # ★ Upload + persist orchestration
│   │   └── SubscriptionManagementIngestionService.cs
│   ├── Ingestion/
│   │   ├── IIngestionBlobStore.cs
│   │   └── IIngestionRunIndexStore.cs
│   └── Normalization/
│       └── SubscriptionManagementNormalizer.cs         # ★ ISubscriptionManagementNormalizer impl
├── BillDrift.Infrastructure/
│   ├── Import/Giacom/SubscriptionManagement/
│   │   ├── SubscriptionManagementCsvIngester.cs        # ★ Pipeline orchestrator
│   │   ├── SubscriptionManagementCsvHeaderMap.cs
│   │   ├── SubscriptionManagementCsvRowReader.cs
│   │   ├── ProductScopeClassifier.cs
│   │   ├── BooleanFlagParser.cs
│   │   ├── RawSubscriptionManagementRowMapper.cs
│   │   └── Internal/
│   │       └── ParsedSubscriptionManagementRow.cs
│   ├── Import/Giacom/
│   │   └── GiacomImportServiceCollectionExtensions.cs  # Register CSV ingester
│   └── Ingestion/
│       ├── AzureBlobIngestionArchiveStore.cs           # BlobServiceClient via DI
│       ├── AzureTableIngestionRunIndexStore.cs         # TableServiceClient via DI
│       ├── InMemoryIngestionBlobStore.cs               # Tests
│       ├── InMemoryIngestionRunIndexStore.cs           # Tests
│       ├── IngestionJsonSerializerContext.cs
│       ├── IngestionStorageOptions.cs
│       └── IngestionServiceCollectionExtensions.cs
├── BillDrift.Domain/
│   ├── Import/RawSubscriptionManagementRow.cs            # Extended fields
│   └── Billing/
│       ├── MicrosoftSubscriptionLine.cs                # Extended
│       ├── ProductDisplayFacts.cs                      # New VO
│       └── SubscriptionLifecycleFacts.cs               # New VO
└── BillDrift.Api/
    ├── Program.cs                                        # Register ingestion services
    └── Imports/
        └── SubscriptionManagementImportEndpoints.cs      # ★ POST/GET upload API

tests/
├── BillDrift.Infrastructure.Tests/
│   ├── Import/Giacom/SubscriptionManagement/
│   │   ├── SubscriptionManagementCsvIngesterTests.cs
│   │   ├── ProductScopeClassifierTests.cs
│   │   ├── BooleanFlagParserTests.cs
│   │   └── GoldenFileComparer.cs                       # Reuse from Giacom/Stripe tests
│   └── Ingestion/
│       ├── AzureBlobIngestionArchiveStoreTests.cs
│       └── AzureTableIngestionRunIndexStoreTests.cs
├── BillDrift.Application.Tests/
│   └── Normalization/
│       └── SubscriptionManagementNormalizerTests.cs
└── fixtures/
    └── subscription-management/                        # ★ NEW — sanitized CSV fixtures
        ├── subscription-management-sample-a.csv
        ├── mixed-products.csv
        ├── column-variant.csv
        └── expected/
            └── sample-a.json
```

**Structure Decision**: Mirror 003 parser architecture plus 008 Azure persistence pattern. Parser tests run stream-only without Azure. Upload workflow adds thin API + orchestration service. Domain types extended minimally for lifecycle fields required by spec FR-006–FR-012.

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
- Two-tier product scope classifier with deny/allow lists (R4)
- Boolean flag parser for NCE/trial (R5)
- Mex ID + commercial key normalisation aligned with 002/003 (R6)
- Extended raw + normalized domain types with lifecycle VOs (R7)
- Implement `SubscriptionManagementNormalizer` in this feature (R8)
- Azure Table + Blob hybrid for ingestion persistence via Aspire DI (R9)
- API multipart upload endpoint (R10)
- Extended `IngestionFailureReason` codes (R11)
- UK date / GBP money parsing (R12)

## Phase 1: Design

**Status**: ✅ Complete

| Artifact | Path |
|----------|------|
| Data model | [data-model.md](./data-model.md) |
| Pipeline contract | [contracts/csv-ingestion-pipeline.md](./contracts/csv-ingestion-pipeline.md) |
| Header map | [contracts/subscription-csv-header-map.md](./contracts/subscription-csv-header-map.md) |
| Product scope rules | [contracts/product-scope-rules.md](./contracts/product-scope-rules.md) |
| Blob archive | [contracts/azure-blob-ingestion-archive.md](./contracts/azure-blob-ingestion-archive.md) |
| Table index | [contracts/azure-table-ingestion-index.md](./contracts/azure-table-ingestion-index.md) |
| Validation quickstart | [quickstart.md](./quickstart.md) |

### Design Invariants

1. **Aspire DI only**: `AzureBlobIngestionArchiveStore(BlobServiceClient, …)` and `AzureTableIngestionRunIndexStore(TableServiceClient, …)` — never `new BlobServiceClient(connectionString)`.
2. **Blob first, manifest last**: Write result payloads, then `manifest.json` as commit marker (008 pattern).
3. **Parser pure**: `ISubscriptionManagementCsvIngester` has no storage dependency; orchestration service composes parser + stores.
4. **No Web storage access**: Upload UI (future) posts to API only.
5. **No SQL**: Ingestion index in Table Storage; all payloads in Blob Storage.
6. **Scope before map**: Product scope filter runs before raw row emission — excluded rows never in output collections.

## Phase 2: Implementation Tasks

**Status**: Pending — run `/speckit-tasks` to generate [tasks.md](./tasks.md)

Expected task groups:
1. Extend domain types (`RawSubscriptionManagementRow`, `MicrosoftSubscriptionLine`, lifecycle VOs)
2. Extend `IngestionFailureReason` enum
3. Define Application import + ingestion store contracts
4. Implement header map, row reader, scope classifier, boolean parser
5. Implement CSV ingester pipeline + raw row mapper
6. Implement `SubscriptionManagementNormalizer`
7. Implement Azure blob archive + table index stores (Aspire DI)
8. Implement in-memory store fakes for tests
9. Implement `SubscriptionManagementIngestionService` orchestration
10. Add API upload/list/detail endpoints
11. Register DI in Infrastructure + Api `Program.cs`
12. Add CSV fixtures + golden-file + Azurite integration tests
13. Wire `InputSnapshotMetadata` fingerprint fields for 008 consumption

## Out of Scope (this feature)

- Giacom PDF or Stripe CSV retroactive blob persistence (002/003 remain stream-only until dedicated task)
- Price list CSV ingestion
- Reconciliation engine integration (consumes output via existing `ReconciliationInputs`)
- Blazor upload UI (API-ready; UI deferred)
- Offer/SKU canonical product mapping
- Stripe corrective actions
- OCR or non-CSV formats
- SQL database of any kind

## User Constraints (Applied)

- BillDrift v1 uses **Azure Blob Storage** and **Azure Table Storage** exclusively for ingestion persistence — **no SQL**
- Storage implemented via Aspire-provided DI-injected `BlobServiceClient` and `TableServiceClient` — no manual connection string construction

## Next Steps

1. `/speckit-tasks` — generate dependency-ordered implementation tasks
2. Obtain sanitized production `SubscriptionManagementReport.csv` exports (minimum 2 variants + mixed-product sample) before header map lock-in
3. `/speckit-implement` — build parser, normalizer, Azure stores, and regression tests
