# Implementation Plan: Reconciliation Item Classification

**Branch**: `006-reconciliation-classification` | **Date**: 2026-07-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-reconciliation-classification/spec.md`

## Summary

Implement a deterministic reconciliation item classification system that assigns every normalized billing line one of four origin types (Microsoft CSP, Non-CSP supplier, Internal, Custom/service) using an ordered rule engine with manual overrides, persists classifications and audit history to Azure Table Storage via Aspire-injected `TableServiceClient`, and integrates with the reconciliation engine (004) and exception surfacing (005) to suppress false missing-billing alerts for internal/custom items and route non-CSP lines to manual mapping workflows. Domain types live in `BillDrift.Domain.Classification`; rule engine in `BillDrift.Application.Classification`; Azure persistence in `BillDrift.Infrastructure.Classification`; minimal API endpoints in `BillDrift.Api`. No Blazor UI in this feature.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: `Azure.Data.Tables`, `Azure.Storage.Blobs` (Infrastructure only); BCL in Application rule engine  
**Storage**: Azure Table Storage — `itemclassifications` table for overrides, history, config; optional Blob container for config snapshots. **No SQL.** Clients via Aspire DI (`TableServiceClient`, `BlobServiceClient`) — no manual connection string construction  
**Testing**: xUnit + FluentAssertions; `InMemoryItemClassificationStore` for rule/pipeline tests; Azurite integration tests for table store  
**Target Platform**: Azure (Aspire AppHost + storage emulator locally)  
**Project Type**: Modular .NET Aspire solution — Domain + Application + Infrastructure + API  
**Performance Goals**: Classify 2,000 items + load config/overrides in <2s; table lookups cached per reconciliation run  
**Constraints**: Deterministic classification (FR-017); conservative defaults (FR-018); mandatory code comments on rule precedence; Aspire-injected storage clients only; Fluent UI deferred to future UI feature  
**Scale/Scope**: Single-tenant reseller; 4 classification types; 5 automatic rules + override; stable business keys for persistence across re-ingestion

### Dependency on 001-billing-domain-model

| Artifact | Usage |
|----------|-------|
| `SupplierCostLine`, `MicrosoftSubscriptionLine`, `StripeBillingItem` | Classification item sources |
| `CustomerIdentity`, `CommercialKey`, `MexId` | Signals and stable keys |
| `ProductMapping`, `ProductClassification` | Hint signal only (not authoritative) |
| Entity ID types | In-run correlation |

### Dependency on 004-reconciliation-engine

| Artifact | Usage |
|----------|-------|
| `IReconciliationEngine`, `ReconciliationRequest`, `ReconciliationContext` | Extended with `ClassificationContext` |
| `MatchGroupBuildStage`, `MismatchDetector`, `ProposedChangeFactory` | Classification-aware guards |
| `ReconciliationOptions` | Unchanged; `IncludeNonCspProducts` still honoured |

### Dependency on 005-reconciliation-exceptions

| Artifact | Usage |
|----------|-------|
| `ExceptionSurfacingService`, `SuppressPhase` | New SR-6 classification suppression |
| `ExceptionCategory.NonCspManualReview` | Unchanged mapping from non-CSP classification |
| `SurfacingContext` | Extended with classification lookup |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Code Quality & Maintainability | ✅ PASS | Rule chain isolated in `ClassificationRuleEngine`; contracts document precedence and storage schema |
| II. Testing Standards | ✅ PASS | Fixture per classification type; determinism test; integration tests for reconciliation impact |
| III. Consistent User Experience | ✅ PASS (design) | Rule basis strings operator-readable; API returns classification metadata for future UI |
| IV. Security by Design | ✅ PASS | No secrets in table entities; storage via Aspire DI |
| V. Billing Accuracy & Human Control | ✅ PASS | Overrides audited; conservative defaults; no auto-apply; internal suppression explicit |
| VI. Pragmatic Simplicity | ✅ PASS | `IItemClassificationStore` justified (Azure isolation); single `ClassificationService`; no strategy pattern proliferation |

### Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I | ✅ PASS | Four contracts + data model; stable key algorithm specified in research R2 |
| II | ✅ PASS | quickstart.md defines 8 validation scenarios + SC mapping |
| III | ✅ PASS | Classification exposed on exceptions via evidence (optional) and API |
| IV | ✅ PASS | Table/Blob clients injected only; auth placeholder on API |
| V | ✅ PASS | Override notes required for alert-suppressing classifications |
| VI | ✅ PASS | In-memory store for tests; no SQL; blob secondary to tables |

**Gate result**: PASS — proceed to `/speckit-tasks`.

## Project Structure

### Documentation (this feature)

```text
specs/006-reconciliation-classification/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── classification-pipeline.md
│   ├── classification-rules.md
│   ├── azure-table-schema.md
│   └── reconciliation-integration.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
BillDrift.sln
src/
├── BillDrift.AppHost/
│   └── AppHost.cs                          # ★ Add Azure Storage (tables + blobs) emulator refs
├── BillDrift.Api/
│   └── Classification/                     # ★ Minimal REST endpoints
│       └── ClassificationEndpoints.cs
├── BillDrift.Domain/
│   └── Classification/                     # ★ New domain types
│       ├── ReconciliationItemClassification.cs
│       ├── ReconciliationItemRef.cs
│       ├── ItemClassification.cs
│       ├── ClassificationOverride.cs
│       ├── ClassificationHistoryEntry.cs
│       └── ClassificationRuleConfiguration.cs
├── BillDrift.Application/
│   └── Classification/                     # ★ Rule engine + service
│       ├── ClassificationService.cs
│       ├── ClassificationRuleEngine.cs
│       ├── ClassificationContext.cs
│       ├── IItemClassificationStore.cs
│       ├── ReconciliationItemRefFactory.cs
│       └── Stages/
│           └── ClassificationEnrichmentStage.cs
│   └── Reconciliation/                     # ★ Modified stages (004)
│       ├── ReconciliationContext.cs        # +Classifications
│       ├── Stages/MatchGroupBuildStage.cs
│       ├── Detection/MismatchDetector.cs
│       └── ExceptionSurfacing/Phases/SuppressPhase.cs  # SR-6
├── BillDrift.Infrastructure/
│   └── Classification/                     # ★ Azure Table store
│       ├── AzureTableItemClassificationStore.cs
│       ├── ClassificationTableEntities.cs
│       ├── ClassificationStorageOptions.cs
│       └── ClassificationStorageExtensions.cs
tests/
├── BillDrift.Application.Tests/
│   └── Classification/
│       ├── ClassificationRuleEngineTests.cs
│       ├── ClassificationServiceTests.cs
│       ├── ClassificationIntegrationTests.cs
│       └── InMemoryItemClassificationStore.cs
├── BillDrift.Infrastructure.Tests/
│   └── Classification/
│       └── AzureTableItemClassificationStoreTests.cs
└── fixtures/
    └── classification/
        ├── classify-csp-full-signals.json
        ├── internal-customer-no-missing-billing.json
        ├── non-csp-supplier-only.json
        ├── classify-custom-stripe-only.json
        └── classify-conservative-partial-sku.json
```

**Structure Decision**: Classification spans Domain (types), Application (rules + reconciliation hooks), Infrastructure (Azure Tables), and API (override/config endpoints). Reconciliation engine modifications stay in existing Application `Reconciliation` folder. No new solution projects. Fluent UI Blazor classification UI deferred — when built, follow `.cursor/skills/fluentui-blazor-usage/SKILL.md` and consume API endpoints from this feature.

## Complexity Tracking

> No constitution violations requiring justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Phase 0 Output

See [research.md](./research.md) — all technical context items resolved:

- R1: Separate `ReconciliationItemClassification` enum
- R2: Stable `ReconciliationItemRef` business keys
- R3: Domain / Application / Infrastructure split + enrichment stage
- R4: Ordered rule chain with conservative fallback
- R5: Azure Table schema (no SQL)
- R6: Aspire storage wiring with DI clients
- R7: Engine integration points
- R8: Exception surfacing SR-6
- R9: Testing strategy with in-memory + Azurite stores
- R10: API endpoints; UI deferred
- R11: `ProductMapping` as hint only

## Phase 1 Output

| Artifact | Path |
|----------|------|
| Data model | [data-model.md](./data-model.md) |
| Pipeline contract | [contracts/classification-pipeline.md](./contracts/classification-pipeline.md) |
| Rules contract | [contracts/classification-rules.md](./contracts/classification-rules.md) |
| Table schema contract | [contracts/azure-table-schema.md](./contracts/azure-table-schema.md) |
| Reconciliation integration | [contracts/reconciliation-integration.md](./contracts/reconciliation-integration.md) |
| Validation guide | [quickstart.md](./quickstart.md) |

## Implementation Notes

1. **Aspire first**: Wire `AddAzureStorage` + table/blob references in AppHost before implementing store; register `AddAzureTableServiceClient("tables")` in API/Infrastructure.
2. **Stable keys**: `ReconciliationItemRefFactory` centralises key derivation — single place to audit identity rules.
3. **Backward compatibility**: `ClassificationContext == null` preserves existing 004 test behaviour until fixtures updated.
4. **First persistence feature**: This introduces Azure Tables to the solution; establish `ClassificationStorageExtensions` pattern for future blob/table features.
5. **Override API**: Implement PUT/DELETE before UI; enables manual testing and future Fluent UI forms.
6. **Engine then surfacing**: Implement RI-1a engine guards first; SR-6 surfacing suppression is defence-in-depth.
7. **Config seeding**: Default empty config; test fixtures set `InternalMexIds` via in-memory store or API in integration tests.

## Storage Constraints (user-provided)

- Azure Blob + Table Storage exclusively for v1 — no SQL
- Use Aspire DI-injected `BlobServiceClient` and `TableServiceClient` only
- Blobs secondary (optional config snapshots); Tables primary for operational data

## UI Constraints (user-provided)

- No Fluent UI work in this feature
- Future Blazor UI uses Fluent UI Blazor skill to refactor skeleton app when classification screens are scheduled
