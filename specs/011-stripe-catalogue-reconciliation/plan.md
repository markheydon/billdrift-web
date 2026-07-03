# Implementation Plan: Stripe Catalogue Reconciliation

**Branch**: `011-stripe-catalogue-reconciliation` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/011-stripe-catalogue-reconciliation/spec.md`

**Operator constraint (plan input)**: BillDrift v1 uses **Azure Blob Storage** and **Azure Table Storage** exclusively — no SQL. Storage access via Aspire DI-injected `BlobServiceClient` and `TableServiceClient` only; no manual connection string construction.

## Summary

Implement a standalone **catalogue reconciliation** pipeline that validates Stripe products and prices against canonical `ProductMapping` entries and intended `IntendedPrice` records (from retail pricing ingestion), detecting missing products, missing prices, incorrect RRP amounts, and duplicate/conflicting catalogue entries. Emit `CatalogueException` and `CatalogueProposedFix` outputs suitable for the existing approval workflow (007). Persist runs to **Azure Blob** (snapshots) and **Azure Table** (index) via Aspire-injected clients. Reuse ingestion archives from Stripe CSV (003) and retail pricing (010); extend catalogue indexing beyond subscription-derived `StripeCatalogueIndex`. Pure engine in Application; API trigger + optional approval ingestion.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: BCL + existing domain types; `Azure.Data.Tables`, `Azure.Storage.Blobs` (Infrastructure); `System.Text.Json` source-gen serializers  
**Storage**: Azure Blob Storage — `catalogue-reconciliation-runs` container (input + result JSON). Azure Table Storage — `cataloguereconciliationruns` table (run index). **No SQL.** Clients via Aspire DI (`BlobServiceClient`, `TableServiceClient`) in API/Infrastructure only — no manual connection string construction  
**Testing**: xUnit + FluentAssertions; engine unit tests with JSON fixtures (no Azure); `InMemoryCatalogueReconciliationStore` for service tests; Azurite integration tests for blob/table stores; approval adapter tests  
**Target Platform**: Azure (Aspire AppHost + Azurite locally)  
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
| `IStripeBillingNormalizer` patterns | New `IStripeCatalogueNormalizer` for catalogue-only normalization |

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
| `CatalogueReconcileStage` | Coexists — customer-scoped catalogue gaps during full reconciliation; 011 is proactive full-catalogue sweep |
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
| III. Consistent User Experience | ✅ PASS | Terminology aligned with 004/005/007 ("proposed fix", "approval", "catalogue exception"); API summaries match approval queue labels |
| IV. Security by Design | ✅ PASS | No Stripe credentials in catalogue logic; Aspire DI storage; operator context on API |
| V. Billing Accuracy & Human Control | ✅ PASS | Deterministic engine; explainable rules per `RuleId`; no auto-apply; replacement price semantics for immutable Stripe prices |
| VI. Pragmatic Simplicity | ✅ PASS | Reuse existing indexes and approval store; single engine class; no SQL; no parallel approval system |

### Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I | ✅ PASS | Six contracts + data model; pipeline and check rules specified |
| II | ✅ PASS | quickstart.md defines 15 validation scenarios + SC mapping |
| III | ✅ PASS | API response shapes operator-readable; Web deferred not blocking |
| IV | ✅ PASS | Blob/Table clients constructor-injected only per contracts |
| V | ✅ PASS | Immutable price correction via create-replacement; duplicate flags non-actionable |
| VI | ✅ PASS | Extends ingestion archives; `InMemoryCatalogueReconciliationStore`; no new database technology |

**Gate result**: PASS — proceed to `/speckit-tasks`.

## Project Structure

### Documentation (this feature)

```text
specs/011-stripe-catalogue-reconciliation/
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
│   └── AppHost.cs                          # Existing storage refs (no change expected)
├── BillDrift.Api/
│   ├── Program.cs                          # + AddCatalogueReconciliationStorage
│   └── CatalogueReconciliation/
│       └── CatalogueReconciliationEndpoints.cs
├── BillDrift.Domain/
│   └── CatalogueReconciliation/            # ★ Domain types
│       ├── CatalogueRunId.cs
│       ├── CatalogueExceptionType.cs
│       ├── CatalogueException.cs
│       ├── CatalogueProposedFix.cs
│       ├── CatalogueReconciliationRun.cs
│       ├── CatalogueReconciliationInputs.cs
│       ├── CatalogueReconciliationOptions.cs
│       ├── CatalogueReconciliationSummary.cs
│       ├── StripeCatalogueProduct.cs
│       └── StripeCataloguePrice.cs
├── BillDrift.Application/
│   └── CatalogueReconciliation/            # ★ Engine + orchestration
│       ├── ICatalogueReconciliationEngine.cs
│       ├── CatalogueReconciliationEngine.cs
│       ├── CatalogueReconciliationPipeline.cs
│       ├── ICatalogueReconciliationService.cs
│       ├── CatalogueReconciliationService.cs
│       ├── CatalogueApprovalAdapter.cs
│       ├── ICatalogueReconciliationStore.cs
│       ├── IStripeCatalogueNormalizer.cs
│       ├── StripeCatalogueNormalizer.cs
│       ├── StripeCatalogueSnapshotIndex.cs   # ★ Full catalogue index
│       ├── CatalogueReconciliationContext.cs
│       ├── Stages/
│       │   ├── ValidateInputsStage.cs
│       │   ├── BuildIndexesStage.cs
│       │   ├── DetectDuplicateConflictsStage.cs
│       │   ├── DetectUnmappedCatalogueStage.cs
│       │   ├── ReconcileMappedProductsStage.cs
│       │   ├── AttachProposedFixesStage.cs
│       │   └── OrderOutputStage.cs
│       └── Detection/
│           ├── CatalogueExceptionFactory.cs
│           └── CatalogueProposedFixFactory.cs
├── BillDrift.Infrastructure/
│   └── CatalogueReconciliation/
│       ├── AzureCatalogueReconciliationStore.cs    # ★ BlobServiceClient + TableServiceClient via DI
│       ├── InMemoryCatalogueReconciliationStore.cs
│       ├── CatalogueReconciliationJsonSerializerContext.cs
│       ├── CatalogueReconciliationStorageOptions.cs
│       └── CatalogueReconciliationServiceCollectionExtensions.cs
└── BillDrift.Web/                          # Deferred: ICatalogueReconciliationApiClient

tests/
├── BillDrift.Application.Tests/
│   └── CatalogueReconciliation/
│       ├── CatalogueReconciliationEngineTests.cs     # ★ Per rule fixtures
│       ├── DeterminismTests.cs
│       ├── StripeCatalogueSnapshotIndexTests.cs
│       ├── CatalogueApprovalAdapterTests.cs
│       └── GoldenRunComparer.cs
├── BillDrift.Infrastructure.Tests/
│   └── CatalogueReconciliation/
│       └── AzureCatalogueReconciliationStoreTests.cs
└── fixtures/
    └── catalogue-reconciliation/                     # ★ NEW JSON fixtures
        ├── catalogue-clean-match.json
        ├── catalogue-missing-product.json
        ├── catalogue-missing-price.json
        ├── catalogue-incorrect-price.json
        ├── catalogue-duplicate-products.json
        ├── catalogue-duplicate-prices.json
        ├── catalogue-pricing-gap.json
        ├── catalogue-unmapped-stripe.json
        ├── catalogue-manual-override-rrp.json
        └── catalogue-determinism.json
```

**Structure Decision**: Follow established BillDrift layering (Domain → Application → Infrastructure → Api). Engine is pure Application; Azure persistence isolated behind `ICatalogueReconciliationStore` with in-memory implementation for tests. No SQL. No `BlobServiceClient` usage in Web project.

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

1. **`StripeCatalogueSnapshotIndex`** is the critical new component — must index full products/prices CSV output, not subscription items only.
2. **Incorrect price** always proposes **create replacement price** — document Stripe immutability in code comments (constitution I).
3. **Storage registration** mirrors 008/010:
   ```csharp
   builder.AddAzureBlobServiceClient("blobs");
   builder.AddAzureTableServiceClient("tables");
   services.AddCatalogueReconciliationStorage();
   ```
4. **Product mappings** accepted inline on API for v1 until dedicated mapping persistence feature exists; blob path reference reserved in contracts.
5. **Full reconciliation (004)** unchanged; operators may run catalogue reconciliation after Stripe + pricing imports as a hygiene gate.

## Next Step

Run `/speckit-tasks` to generate dependency-ordered implementation tasks.
