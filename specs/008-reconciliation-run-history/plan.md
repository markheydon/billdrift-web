# Implementation Plan: Reconciliation Run History & Audit

**Branch**: `008-reconciliation-run-history` | **Date**: 2026-07-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-reconciliation-run-history/spec.md`

## Summary

Persist every reconciliation run as an immutable historical record using **Azure Table Storage** for queryable run metadata, drift indexes, and audit events, and **Azure Blob Storage** for large normalized input and results snapshots. Domain types in `BillDrift.Domain.History`; orchestration in `BillDrift.Application.History`; Azure persistence via Aspire-injected `TableServiceClient` and `BlobServiceClient` only; REST API in `BillDrift.Api`; operator views in **Fluent UI Blazor v5** (`BillDrift.Web`). Cross-run comparison, drift trends, and pricing drift timelines are computed in Application from stored snapshots. Approval status is joined at read time from feature 007 — not duplicated. No SQL; write-back execution outcomes reserved for future apply feature.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: `Azure.Data.Tables`, `Azure.Storage.Blobs` (Infrastructure); `Microsoft.FluentUI.AspNetCore.Components` v5 (Web); `System.Text.Json` source-gen serializers in Infrastructure  
**Storage**: Azure Table Storage — `reconciliationrunhistory` table (run index, input metadata, drift index rows, audit). Azure Blob Storage — `reconciliation-runs` container (per-run JSON snapshots). **No SQL.** Clients via Aspire DI (`TableServiceClient`, `BlobServiceClient`) in API/Infrastructure only — no manual connection string construction  
**Testing**: xUnit + FluentAssertions; `InMemoryRunHistoryStore` for unit tests; Azurite integration tests for table/blob stores; API integration tests; comparison/trend algorithm unit tests with multi-run fixtures  
**Target Platform**: Azure (Aspire AppHost + Azurite locally)  
**Project Type**: Modular .NET Aspire solution — Domain + Application + Infrastructure + API + Web (Blazor Interactive Server)  
**Performance Goals**: Run list load <2s for 100 runs; run detail summary <5s (SC-001); two-run comparison <10s (SC-003); drift trend query <5s for six-month window  
**Constraints**: Immutable finalized runs; Aspire DI storage clients only; Web calls API only; manual upload inputs (Phase 1 build queue); no Stripe write-back in v1; 24-month default retention  
**Scale/Scope**: Single-tenant reseller; ~12–24 runs/year typical; runs up to ~500 customer-product groups; blob payloads up to low tens of MB per run

### Dependency on 004-reconciliation-engine

| Artifact | Usage |
|----------|-------|
| `ReconciliationRun`, `ReconciliationInputs` | Persist source aggregate |
| `Mismatch`, `ProposedChange`, `EntityMatchGroup` | Results snapshot |
| `RunId`, `MismatchId`, `ProposedChangeId` | Identity and linking |

### Dependency on 007-reconciliation-approval-workflow

| Artifact | Usage |
|----------|-------|
| `IApprovalStore` | Join proposal decision state at read time |
| `ApprovalProposal`, `ApprovalDecisionState` | Proposal status links on run detail |
| Export metadata | Referenceable from run record |

### Dependency on 001–003 (ingestion)

| Artifact | Usage |
|----------|-------|
| Normalized billing entities | Input snapshot payloads |
| Upload metadata (filename, fingerprint, timestamp) | `InputSnapshotMetadata` per domain |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Code Quality & Maintainability | ✅ PASS | History pipeline isolated; stable mismatch fingerprint documented in contracts |
| II. Testing Standards | ✅ PASS | Multi-run fixtures for compare/trends; Azurite integration; algorithm unit tests |
| III. Consistent User Experience | ✅ PASS | Fluent UI run history pages; terminology aligned with 004/005/007 |
| IV. Security by Design | ✅ PASS | Audit events; storage via Aspire DI; no secrets in blob payloads |
| V. Billing Accuracy & Human Control | ✅ PASS | Immutable snapshots preserve deterministic run outcomes; approval joined not copied |
| VI. Pragmatic Simplicity | ✅ PASS | `IRunHistoryStore` for Azure isolation; trends computed on read initially; denormalized drift index optional |

### Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I | ✅ PASS | Six contracts + data model; blob layout and table schema documented |
| II | ✅ PASS | quickstart.md defines 12 validation scenarios + SC mapping |
| III | ✅ PASS | Fluent UI integration contract; run list/detail/compare/trends pages |
| IV | ✅ PASS | Table/Blob clients injected only in API/Infrastructure |
| V | ✅ PASS | Stored results immutable; approval state read from 007 store |
| VI | ✅ PASS | In-memory store for tests; single `RunHistoryService`; no SQL |

**Gate result**: PASS — proceed to `/speckit-tasks`.

## Project Structure

### Documentation (this feature)

```text
specs/008-reconciliation-run-history/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── run-history-pipeline.md
│   ├── azure-table-schema.md
│   ├── azure-blob-run-archive.md
│   ├── run-history-api-endpoints.md
│   ├── mismatch-comparison-rules.md
│   ├── pricing-drift-timeline.md
│   └── fluent-ui-integration.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
BillDrift.sln
src/
├── BillDrift.AppHost/
│   └── AppHost.cs                          # Existing storage refs (no change expected)
├── BillDrift.Api/
│   ├── Program.cs                          # + AddRunHistoryStorage, MapRunHistoryEndpoints
│   └── History/
│       └── RunHistoryEndpoints.cs
├── BillDrift.Domain/
│   └── History/                            # ★ Domain types
│       ├── ReconciliationRunRecord.cs
│       ├── InputSnapshotMetadata.cs
│       ├── MappingVersionReference.cs
│       ├── RunResultsSnapshot.cs
│       ├── RunComparisonReport.cs
│       ├── DriftTrendEntry.cs
│       ├── PricingDriftTimelineEntry.cs
│       ├── StableMismatchKey.cs
│       ├── RunHistoryEnums.cs
│       └── ExecutionOutcome.cs             # Future-ready placeholder
├── BillDrift.Application/
│   └── History/                            # ★ Orchestration + analysis
│       ├── RunHistoryService.cs
│       ├── RunArchiveService.cs
│       ├── RunComparisonService.cs
│       ├── DriftTrendAnalyzer.cs
│       ├── PricingDriftAnalyzer.cs
│       ├── StableMismatchKeyFactory.cs
│       ├── IRunHistoryStore.cs
│       └── RunHistoryServiceCollectionExtensions.cs
├── BillDrift.Infrastructure/
│   └── History/                            # ★ Azure persistence
│       ├── AzureTableRunHistoryStore.cs
│       ├── AzureBlobRunArchiveStore.cs
│       ├── RunHistoryTableEntities.cs
│       ├── RunHistoryStorageOptions.cs
│       ├── RunHistoryJsonSerializerContext.cs
│       └── RunHistoryStorageExtensions.cs
├── BillDrift.Web/
│   ├── Pages/
│   │   └── History/
│   │       ├── RunHistoryListPage.razor
│   │       ├── RunDetailPage.razor
│   │       ├── RunComparisonPage.razor
│   │       └── DriftTrendsPage.razor
│   └── Services/
│       └── RunHistoryApiClient.cs
tests/
├── BillDrift.Application.Tests/
│   └── History/
│       ├── RunComparisonServiceTests.cs
│       ├── DriftTrendAnalyzerTests.cs
│       ├── PricingDriftAnalyzerTests.cs
│       ├── StableMismatchKeyFactoryTests.cs
│       └── InMemoryRunHistoryStore.cs
├── BillDrift.Infrastructure.Tests/
│   └── History/
│       ├── AzureTableRunHistoryStoreTests.cs
│       └── AzureBlobRunArchiveStoreTests.cs
├── BillDrift.Api.Tests/
│   └── History/
│       └── RunHistoryEndpointsTests.cs
└── fixtures/
    └── run-history/
        ├── jan-2026-run.json
        ├── feb-2026-run.json
        ├── recurring-quantity-drift/
        └── pricing-lag-timeline/
```

**Structure Decision**: Run history spans Domain, Application, Infrastructure, API, and Web. Reconciliation engine gains a post-run hook call (`RunArchiveService.PersistAsync`) but engine logic unchanged. Comparison and trend analyzers are pure Application services over deserialized snapshots — no SQL analytics layer.

## Complexity Tracking

> No constitution violations requiring justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Phase 0 Output

See [research.md](./research.md) — all technical context items resolved:

- R1: Hybrid Table (metadata/index) + Blob (payloads) storage split
- R2: Per-run blob layout under `{runId}/`
- R3: `StableMismatchKey` for cross-run compare/trends (distinct from run-scoped `MismatchId`)
- R4: Mapping version as content hash + operator label
- R5: Domain / Application / Infrastructure / API / Web split
- R6: Aspire DI clients only (API/Infrastructure)
- R7: Approval status joined at read via `IApprovalStore`
- R8: Denormalized drift index rows on persist for trend query performance
- R9: Comparison/trends computed in Application (no SQL)
- R10: Retention via `ArchivedAt` table flag + blob tier metadata
- R11: Manual upload Phase 1 — input metadata from ingestion pipeline
- R12: Testing strategy (in-memory + Azurite + multi-run fixtures)

## Phase 1 Output

| Artifact | Path |
|----------|------|
| Data model | [data-model.md](./data-model.md) |
| Pipeline contract | [contracts/run-history-pipeline.md](./contracts/run-history-pipeline.md) |
| Table schema | [contracts/azure-table-schema.md](./contracts/azure-table-schema.md) |
| Blob archive | [contracts/azure-blob-run-archive.md](./contracts/azure-blob-run-archive.md) |
| API endpoints | [contracts/run-history-api-endpoints.md](./contracts/run-history-api-endpoints.md) |
| Comparison rules | [contracts/mismatch-comparison-rules.md](./contracts/mismatch-comparison-rules.md) |
| Pricing drift | [contracts/pricing-drift-timeline.md](./contracts/pricing-drift-timeline.md) |
| Fluent UI integration | [contracts/fluent-ui-integration.md](./contracts/fluent-ui-integration.md) |
| Validation guide | [quickstart.md](./quickstart.md) |

## Implementation Notes

1. **Persist immediately after reconciliation**: `RunArchiveService.PersistAsync(ReconciliationRun, RunArchiveContext)` called from reconciliation completion path (API orchestration layer, not inside engine stages).
2. **Blob first, then table index**: Write blob payloads, compute content hashes, then insert table rows — partial failure leaves failed run record with `Status=Failed`.
3. **Approval join on read**: `RunHistoryService.GetRunDetailAsync` queries `IApprovalStore.ListProposalsByRunAsync` — never copy decision state into blob snapshot.
4. **Stable mismatch key**: Computed at persist time and stored in drift index table rows; see [mismatch-comparison-rules.md](./contracts/mismatch-comparison-rules.md).
5. **No Web storage access**: Do not add `TableServiceClient` or `BlobServiceClient` to `BillDrift.Web`.
6. **Idempotent persist**: Re-persist same `RunId` replaces blob payloads and upserts index rows (only allowed before `FinalizedAt` or for failed-run retry).
7. **Phase 1 manual uploads**: Input metadata populated from ingestion upload handlers (features 002/003/015–018); API integrations deferred to Phase 2 build queue.

## Storage Constraints (user-provided)

- Azure Blob + Table Storage exclusively for v1 — **no SQL**
- Use Aspire DI-injected `BlobServiceClient` and `TableServiceClient` only
- Tables: run index, input metadata, drift index, audit
- Blobs: per-run input and results JSON snapshots

## Phase 2 Status

**Status**: Pending — see `/speckit-tasks`

## Deferred (explicitly out of scope)

- Stripe API write-back / execution outcome population
- Real-time drift alerting
- SQL persistence or analytics warehouse
- Cross-tenant partitioning
- Automatic customer identity merge across Mex ID changes
- Raw PDF/CSV binary storage as primary snapshot format
