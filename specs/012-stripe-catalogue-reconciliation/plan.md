# Implementation Plan: Stripe Catalogue Reconciliation

**Branch**: `012-stripe-catalogue-reconciliation` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/012-stripe-catalogue-reconciliation/spec.md`

**Operator constraint (plan input)**: BillDrift v1 uses **Azure Blob Storage** and **Azure Table Storage** exclusively — **no SQL database** until a validated requirement emerges that cannot be satisfied by blob and table storage. Storage access MUST use Aspire DI-injected `BlobServiceClient` and `TableServiceClient`; **no manual connection string construction** or environment guessing unless phenomenally justified and documented in Complexity Tracking.

## Summary

Implement a standalone **catalogue reconciliation** pipeline that validates Stripe products and prices against canonical `ProductMapping` entries and intended `IntendedPrice` records (from retail pricing ingestion), detecting missing products, missing prices, incorrect RRP amounts, and duplicate/conflicting catalogue entries. Emit `CatalogueException` and `CatalogueProposedFix` outputs suitable for the existing approval workflow (007). Persist runs to **Azure Blob** (snapshots) and **Azure Table** (index) via Aspire-injected clients only. Reuse ingestion archives from Stripe CSV (003) and retail pricing (010). Pure engine in Application; API trigger + optional approval ingestion.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: BCL + existing domain types; `Azure.Data.Tables`, `Azure.Storage.Blobs` (Infrastructure); `System.Text.Json` source-gen serializers  
**Storage**: Azure Blob Storage — `catalogue-reconciliation-runs` container (input + result JSON). Azure Table Storage — `cataloguereconciliationruns` table (run index). **No SQL.** Clients via Aspire DI (`BlobServiceClient`, `TableServiceClient`) registered in `BillDrift.Api` only — infrastructure stores receive clients via constructor injection; **no** `new BlobServiceClient(...)`, `Environment.GetEnvironmentVariable`, or manual connection string parsing  
**Testing**: xUnit + FluentAssertions; engine unit tests with JSON fixtures (no Azure); `InMemoryCatalogueReconciliationStore` for service tests; Azurite integration tests for blob/table stores; approval adapter tests  
**Target Platform**: Azure (Aspire AppHost + Azurite emulator locally)  
**Project Type**: Modular .NET Aspire solution — Domain + Application engine + Infrastructure storage + API endpoints  
**Performance Goals**: Reconcile 500 mapped products × ~4 price slots in <10 seconds engine time (SC-001 review budget); persist + API round-trip <30 seconds  
**Constraints**: Deterministic output (FR-021, SC-004); no Stripe API writes; human approval before catalogue changes (FR-020); exact RRP match default; Web UI deferred to follow-on  
**Scale/Scope**: Single-tenant reseller; catalogue snapshots up to ~1,000 products / ~4,000 prices; monthly hygiene runs

### Dependency on 001-billing-domain-model

| Artifact | Usage |
|----------|-------|
| `ProductMapping`, `CommercialKey`, `CommercialKeyRoot` | Iteration scope and correlation |
| `IntendedPrice`, `PriceSource`, `Term`, `BillingFrequency` | Expected RRP reference |
| `ProposedChange`, `CatalogueEntryPayload` | Approval adapter target |
| `MismatchSeverity`, `IdempotencyKey` | Shared semantics |

### Dependency on 003-stripe-csv-ingestion

| Artifact | Usage |
|----------|-------|
| `RawStripeProduct`, `RawStripePrice` | Catalogue snapshot source |
| Ingestion blob archive | Load products/prices by `stripeIngestionRunId` |
| `IStripeBillingNormalizer` patterns | `IStripeCatalogueNormalizer` for catalogue-only normalization |

### Dependency on 010-retail-pricing-ingestion

| Artifact | Usage |
|----------|-------|
| Resolved `IntendedPrice` blob | Expected RRP per commercial key |
| `IIntendedPriceResolver` / `IntendedPriceIndex` | Manual override precedence |
| Ingestion run index | Resolve `pricingIngestionRunId` |

### Dependency on 004-reconciliation-engine

| Artifact | Usage |
|----------|-------|
| `ProductMappingIndex`, `IntendedPriceIndex` | Reuse indexing |
| `CatalogueReconcileStage` | Coexists — customer-scoped catalogue gaps during full reconciliation |
| `ProposedChangeFactory` patterns | Catalogue fix payload shapes |

### Dependency on 007-reconciliation-approval-workflow

| Artifact | Usage |
|----------|-------|
| `IApprovalStore`, `ApprovalIngestionService` | Ingest catalogue proposed fixes |
| `ApprovalProposalCategory.Catalogue`, `ApprovalEligibility.CatalogueConflict` | Eligibility rules |

### Dependency on 008-reconciliation-run-history

| Pattern | Reuse |
|---------|-------|
| Blob manifest + inputs/results layout | Adapted for catalogue runs (separate container) |
| Table run index | Adapted schema |
| `InMemory*Store` test pattern | `InMemoryCatalogueReconciliationStore` |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Code Quality & Maintainability | ✅ PASS | Dedicated pipeline stages; check rules documented in contracts; mandatory comments on RRP comparison and duplicate detection |
| II. Testing Standards | ✅ PASS | One fixture per `CatalogueExceptionType`; determinism test; Azurite store integration; approval adapter tests |
| III. Consistent User Experience | ✅ PASS | Terminology aligned with 004/005/007 ("proposed fix", "approval", "catalogue exception") |
| IV. Security by Design | ✅ PASS | No Stripe credentials in catalogue logic; Aspire DI storage only; operator context on API |
| V. Billing Accuracy & Human Control | ✅ PASS | Deterministic engine; explainable rules per `RuleId`; no auto-apply; replacement price semantics |
| VI. Pragmatic Simplicity | ✅ PASS | Reuse existing indexes and approval store; single engine class; no SQL; no parallel approval system |

### Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I | ✅ PASS | Six contracts + data model; pipeline and check rules specified |
| II | ✅ PASS | quickstart.md defines 15 validation scenarios + SC mapping |
| III | ✅ PASS | API response shapes operator-readable; Web deferred not blocking |
| IV | ✅ PASS | Blob/Table clients constructor-injected only per contracts; Web has no storage access |
| V | ✅ PASS | Immutable price correction via create-replacement; duplicate flags non-actionable |
| VI | ✅ PASS | Extends ingestion archives; `InMemoryCatalogueReconciliationStore`; no new database technology |

**Gate result**: PASS — proceed to `/speckit-tasks`.

## Project Structure

### Documentation (this feature)

```text
specs/012-stripe-catalogue-reconciliation/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── catalogue-reconciliation-pipeline.md
│   ├── catalogue-check-rules.md
│   ├── azure-blob-catalogue-run-archive.md
│   ├── azure-table-catalogue-run-schema.md
│   ├── catalogue-reconciliation-api-endpoints.md
│   └── approval-integration.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
BillDrift.sln
src/
├── BillDrift.AppHost/
│   └── AppHost.cs                          # storage.AddTables("tables") + AddBlobs("blobs") → api refs
├── BillDrift.Api/
│   ├── Program.cs                          # AddAzureBlobServiceClient + AddAzureTableServiceClient + AddCatalogueReconciliationStorage
│   └── CatalogueReconciliation/
│       └── CatalogueReconciliationEndpoints.cs
├── BillDrift.Domain/
│   └── CatalogueReconciliation/
├── BillDrift.Application/
│   └── CatalogueReconciliation/
│       ├── ICatalogueReconciliationEngine.cs
│       ├── CatalogueReconciliationEngine.cs
│       ├── CatalogueReconciliationPipeline.cs
│       ├── ICatalogueReconciliationService.cs
│       ├── CatalogueReconciliationService.cs
│       ├── CatalogueApprovalAdapter.cs
│       ├── ICatalogueReconciliationStore.cs
│       ├── IStripeCatalogueNormalizer.cs
│       ├── StripeCatalogueSnapshotIndex.cs
│       └── Stages/ + Detection/
├── BillDrift.Infrastructure/
│   └── CatalogueReconciliation/
│       ├── AzureCatalogueReconciliationStore.cs    # BlobServiceClient + TableServiceClient via DI
│       ├── InMemoryCatalogueReconciliationStore.cs
│       └── CatalogueReconciliationServiceCollectionExtensions.cs
└── BillDrift.Web/                          # HTTP to API only — no BlobServiceClient

tests/
├── BillDrift.Application.Tests/CatalogueReconciliation/
├── BillDrift.Infrastructure.Tests/CatalogueReconciliation/
└── fixtures/catalogue-reconciliation/
```

**Structure Decision**: Follow established BillDrift layering (Domain → Application → Infrastructure → Api). Engine is pure Application; Azure persistence isolated behind `ICatalogueReconciliationStore` with in-memory implementation for tests. **No SQL.** No `BlobServiceClient` / `TableServiceClient` in Web or Application projects.

## Complexity Tracking

> No constitution violations requiring justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Phase 0 & Phase 1 Artifacts

| Artifact | Path | Status |
|----------|------|--------|
| Research | [research.md](./research.md) | ✅ Complete |
| Data model | [data-model.md](./data-model.md) | ✅ Complete |
| Pipeline contract | [contracts/catalogue-reconciliation-pipeline.md](./contracts/catalogue-reconciliation-pipeline.md) | ✅ Complete |
| Check rules | [contracts/catalogue-check-rules.md](./contracts/catalogue-check-rules.md) | ✅ Complete |
| Blob archive | [contracts/azure-blob-catalogue-run-archive.md](./contracts/azure-blob-catalogue-run-archive.md) | ✅ Complete |
| Table schema | [contracts/azure-table-catalogue-run-schema.md](./contracts/azure-table-catalogue-run-schema.md) | ✅ Complete |
| API endpoints | [contracts/catalogue-reconciliation-api-endpoints.md](./contracts/catalogue-reconciliation-api-endpoints.md) | ✅ Complete |
| Approval integration | [contracts/approval-integration.md](./contracts/approval-integration.md) | ✅ Complete |
| Quickstart | [quickstart.md](./quickstart.md) | ✅ Complete |

## Implementation Notes

1. **`StripeCatalogueSnapshotIndex`** indexes full products/prices CSV output, not subscription items only.
2. **Incorrect price** always proposes **create replacement price** — document Stripe immutability in code comments (constitution I).
3. **Storage registration** (API only, Aspire DI):
   ```csharp
   // AppHost.cs — already wired
   var storage = builder.AddAzureStorage("storage").RunAsEmulator();
   var tables = storage.AddTables("tables");
   var blobs = storage.AddBlobs("blobs");
   api.WithReference(tables).WithReference(blobs);

   // Program.cs
   builder.AddAzureBlobServiceClient("blobs");
   builder.AddAzureTableServiceClient("tables");
   services.AddCatalogueReconciliationStorage();
   ```
4. **Product mappings** accepted inline on API for v1 until dedicated mapping persistence feature exists.
5. **Full reconciliation (004)** unchanged; operators may run catalogue reconciliation after Stripe + pricing imports as a hygiene gate.

## Next Step

Run `/speckit-tasks` to generate dependency-ordered implementation tasks.
