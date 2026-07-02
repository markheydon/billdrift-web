# Implementation Plan: Reconciliation Change Approval Workflow

**Branch**: `007-reconciliation-approval-workflow` | **Date**: 2026-07-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-reconciliation-approval-workflow/spec.md`

## Summary

Implement a human-in-the-loop approval workflow that ingests `ProposedChange` items from reconciliation runs, persists proposal state and immutable audit decisions to **Azure Table Storage**, exports approved changesets to **Azure Blob Storage**, and exposes operator review through **Fluent UI Blazor v5** pages in `BillDrift.Web` (first UI feature — refactors Bootstrap skeleton). Domain types in `BillDrift.Domain.Approval`; orchestration in `BillDrift.Application.Approval`; Azure persistence via Aspire-injected `TableServiceClient` and `BlobServiceClient` only; REST API in `BillDrift.Api`; no Stripe auto-apply; no SQL.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: `Azure.Data.Tables`, `Azure.Storage.Blobs` (Infrastructure); `Microsoft.FluentUI.AspNetCore.Components` v5 (Web); BCL in Application  
**Storage**: Azure Table Storage — `reconciliationapprovals` table (proposals, decisions, audit, export metadata). Azure Blob Storage — `approved-changesets` container for JSON exports. **No SQL.** Clients via Aspire DI (`TableServiceClient`, `BlobServiceClient`) in API/Infrastructure only — no manual connection string construction  
**Testing**: xUnit + FluentAssertions; `InMemoryApprovalStore` for unit tests; Azurite integration tests for table/blob stores; API integration tests; optional bUnit for Fluent layout smoke  
**Target Platform**: Azure (Aspire AppHost + storage emulator locally)  
**Project Type**: Modular .NET Aspire solution — Domain + Application + Infrastructure + API + Web (Blazor Interactive Server)  
**Performance Goals**: Load approval queue for 200 proposals in <2s; export changeset <1s for typical runs  
**Constraints**: No automatic Stripe mutations; immutable audit; deterministic export order; Fluent UI v5 patterns per skill; Web calls API only (no direct storage from Blazor)  
**Scale/Scope**: Single-tenant reseller; first operator UI; manual apply via export JSON; automated Stripe apply deferred

### Dependency on 004-reconciliation-engine

| Artifact | Usage |
|----------|-------|
| `ReconciliationRun`, `ProposedChange` | Ingest source |
| `ProposedActionType`, `IdempotencyKey` | Action typing and supersession keys |
| `ProposedChangeTarget`, `CatalogueEntryPayload` | Prior/proposed value extraction |

### Dependency on 005-reconciliation-exceptions

| Artifact | Usage |
|----------|-------|
| `ReconciliationExceptionViewModel` | Eligibility input |
| `RequiresActionNow`, `ProposedChangeId` | Investigation vs actionable |
| Suppression rules | Mirrored in `ApprovalEligibilityEvaluator` |

### Dependency on 006-reconciliation-classification

| Artifact | Usage |
|----------|-------|
| `ClassificationContext` | Non-CSP / internal gating |
| `ItemClassification` | Eligibility blocks |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Code Quality & Maintainability | ✅ PASS | Approval pipeline isolated; contracts document state machine and storage |
| II. Testing Standards | ✅ PASS | Fixture per action type; no-auto-approve regression; export filtering tests |
| III. Consistent User Experience | ✅ PASS | Fluent UI v5 layout; prior vs proposed visible; approve/reject/export pattern |
| IV. Security by Design | ✅ PASS | Audit events; operator context; storage via Aspire DI; no secrets in exports |
| V. Billing Accuracy & Human Control | ✅ PASS | Pending→Approved/Rejected only by operator; no auto-apply; export handoff only |
| VI. Pragmatic Simplicity | ✅ PASS | `IApprovalStore` for Azure isolation; single `ApprovalService`; no SQL |

### Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I | ✅ PASS | Five contracts + data model; supersession algorithm in research R7 |
| II | ✅ PASS | quickstart.md defines 10 validation scenarios + SC mapping |
| III | ✅ PASS | Fluent UI integration contract; terminology aligned with 005 |
| IV | ✅ PASS | Table/Blob clients injected only in API/Infrastructure |
| V | ✅ PASS | Investigation items non-exportable; stale acknowledgment required |
| VI | ✅ PASS | In-memory store for tests; Web via HTTP not direct storage |

**Gate result**: PASS — proceed to `/speckit-tasks`.

## Project Structure

### Documentation (this feature)

```text
specs/007-reconciliation-approval-workflow/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── approval-workflow-pipeline.md
│   ├── azure-table-schema.md
│   ├── azure-blob-changeset-export.md
│   ├── approval-api-endpoints.md
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
│   ├── Program.cs                            # + AddApprovalStorage, MapApprovalEndpoints
│   └── Approval/                             # ★ REST endpoints
│       └── ApprovalEndpoints.cs
├── BillDrift.Domain/
│   └── Approval/                             # ★ Domain types
│       ├── ApprovalProposal.cs
│       ├── ApprovalDecision.cs
│       ├── ApprovalEnums.cs
│       ├── ApprovedChangeset.cs
│       └── ApprovalAuditEvent.cs
├── BillDrift.Application/
│   └── Approval/                             # ★ Workflow services
│       ├── ApprovalService.cs
│       ├── ApprovalIngestionService.cs
│       ├── ApprovalEligibilityEvaluator.cs
│       ├── ApprovedChangesetBuilder.cs
│       ├── IApprovalStore.cs
│       └── ApprovalServiceCollectionExtensions.cs
├── BillDrift.Infrastructure/
│   └── Approval/                             # ★ Azure persistence
│       ├── AzureTableApprovalStore.cs
│       ├── AzureBlobChangesetExporter.cs
│       ├── ApprovalTableEntities.cs
│       ├── ApprovalStorageOptions.cs
│       └── ApprovalStorageExtensions.cs
├── BillDrift.Web/
│   ├── Program.cs                            # ★ AddFluentUIComponents, HttpClient
│   ├── Components/
│   │   ├── App.razor                         # ★ FluentProviders, CSS link
│   │   ├── Layout/
│   │   │   └── MainLayout.razor              # ★ FluentLayout refactor
│   │   └── Approval/                         # ★ Dialogs, panels
│   │       ├── RejectProposalDialog.razor
│   │       ├── BulkApproveDialog.razor
│   │       └── ExportChangesetPanel.razor
│   ├── Pages/
│   │   └── Approvals/
│   │       └── ApprovalQueuePage.razor       # ★ Primary operator UI
│   └── Services/
│       └── ApprovalApiClient.cs
tests/
├── BillDrift.Application.Tests/
│   └── Approval/
│       ├── ApprovalEligibilityEvaluatorTests.cs
│       ├── ApprovalServiceTests.cs
│       ├── ApprovedChangesetBuilderTests.cs
│       └── InMemoryApprovalStore.cs
├── BillDrift.Infrastructure.Tests/
│   └── Approval/
│       ├── AzureTableApprovalStoreTests.cs
│       └── AzureBlobChangesetExporterTests.cs
├── BillDrift.Api.Tests/
│   └── Approval/
│       └── ApprovalEndpointsTests.cs
└── fixtures/
    └── approval/
        ├── mixed-subscription-proposals.json
        ├── quantity-mismatch-proposal.json
        └── mapping-ambiguous-investigation.json
```

**Structure Decision**: Approval spans Domain, Application, Infrastructure, API, and Web. First feature to deliver operator UI — Fluent UI refactor is scoped to shared layout + approval pages, not full product chrome. Reconciliation engine unchanged except optional helper to bundle run + surfacing for ingest endpoint.

## Complexity Tracking

> No constitution violations requiring justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Phase 0 Output

See [research.md](./research.md) — all technical context items resolved:

- R1: Durable approval proposals keyed by `IdempotencyKey` + `RunId`
- R2: `ApprovalEligibility` for investigation/conflict/blocked items
- R3: Domain / Application / Infrastructure / API / Web split
- R4: Azure Table schema (`reconciliationapprovals`)
- R5: Blob export for approved changesets
- R6: Aspire DI clients only (API/Infrastructure)
- R7: Supersession on re-run with historical audit
- R8: Fluent UI v5 first-time Web setup
- R9: Web → API HttpClient via Aspire service discovery
- R10: Eligibility integration with 005/006
- R11: Testing strategy (in-memory + Azurite)
- R12: Bulk approve confirmation token

## Phase 1 Output

| Artifact | Path |
|----------|------|
| Data model | [data-model.md](./data-model.md) |
| Pipeline contract | [contracts/approval-workflow-pipeline.md](./contracts/approval-workflow-pipeline.md) |
| Table schema | [contracts/azure-table-schema.md](./contracts/azure-table-schema.md) |
| Blob export | [contracts/azure-blob-changeset-export.md](./contracts/azure-blob-changeset-export.md) |
| API endpoints | [contracts/approval-api-endpoints.md](./contracts/approval-api-endpoints.md) |
| Fluent UI integration | [contracts/fluent-ui-integration.md](./contracts/fluent-ui-integration.md) |
| Validation guide | [quickstart.md](./quickstart.md) |

## Implementation Notes

1. **Storage first**: Implement `ApprovalStorageExtensions` following `ClassificationStorageExtensions` pattern; register in API `Program.cs` with existing `AddAzureTableServiceClient` / `AddAzureBlobServiceClient`.
2. **Ingest before UI**: API ingest + approve + export endpoints with integration tests before Blazor pages.
3. **Fluent UI skeleton refactor**: Complete layout/providers before approval grid — prevents duplicate Bootstrap/Fluent styling.
4. **Eligibility parity**: `ApprovalEligibilityEvaluator` unit tests must mirror 005 suppression fixtures to prevent approvable mapping-ambiguous items.
5. **No Stripe writes**: Export JSON is the only bill-impacting output artifact; grep CI for Stripe mutation clients in Approval namespace.
6. **Operator context**: `IOperatorContext` dev header → future auth middleware hook without changing service signatures.
7. **Web storage boundary**: Do not add `TableServiceClient` to `BillDrift.Web` — constitution IV + user constraint.
8. **Idempotent ingest**: Re-running ingest for same `RunId` replaces proposal snapshots without duplicating rows.

## Storage Constraints (user-provided)

- Azure Blob + Table Storage exclusively for v1 — **no SQL**
- Use Aspire DI-injected `BlobServiceClient` and `TableServiceClient` only
- Tables: proposal state, decisions, audit, export index
- Blobs: approved changeset JSON exports

## UI Constraints (user-provided)

- First UI feature — refactor `BillDrift.Web` skeleton to **Fluent UI Blazor v5** per `.cursor/skills/fluentui-blazor-usage/SKILL.md`
- Replace Bootstrap layout with `FluentLayout` / `FluentNav` (v5 names)
- Approval queue as primary operator workflow page
- Web consumes API over HTTP; no direct Azure storage from Blazor

## Phase 2 Status

**Status**: Pending — run `/speckit-tasks` to generate [tasks.md](./tasks.md)

## Deferred (explicitly out of scope)

- Stripe API apply (manual or automated)
- Full reconciliation exception browser UI
- Authentication/authorization UI
- SQL persistence layer
