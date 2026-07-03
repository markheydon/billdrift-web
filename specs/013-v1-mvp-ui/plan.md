# Implementation Plan: V1 MVP Operator UI

**Branch**: `013-v1-mvp-ui` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/013-v1-mvp-ui/spec.md`

**Scope boundary**: Application-layer domain/business logic is **frozen**. In scope: (1) API endpoints that thinly expose existing Application services, (2) Blazor operator UI for all MVP workflows, (3) thin ingestion orchestration glue mirroring 009/010 patterns (not new domain rules).

## Summary

Deliver the V1 operator experience end-to-end: upload all source files (Giacom PDF, Stripe CSV, Subscription Management CSV, retail pricing CSV), trigger reconciliation, review exceptions and margins, manage session mappings/classification, run catalogue reconciliation, approve proposals, export changesets, and browse run history — all from the Blazor UI without CLI tooling.

**Technical approach**: Add missing API endpoints (`/api/imports/giacom-pdf`, `/api/imports/stripe-csv`, `/api/reconciliation/runs`, approval `ingest-from-run`) as thin adapters over existing Application services. Build Fluent UI Blazor v5 pages and typed HTTP clients. Extend partial implementations (approvals ~70%, run history ~60%) rather than rewrite. Defer persistent product mapping catalogue (Application-layer gap).

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: Microsoft.FluentUI.AspNetCore.Components v5 (Web); existing BillDrift Application/Infrastructure services (frozen); Aspire service discovery (`https+http://api`)  
**Storage**: Azure Blob + Table via Aspire DI (existing — ingestion archives, run history, approval store); **no SQL**  
**Testing**: xUnit + FluentAssertions; API contract tests via WebApplicationFactory; existing Application tests unchanged  
**Target Platform**: .NET Aspire AppHost (local Azurite + Blazor Interactive Server)  
**Project Type**: Modular Aspire solution — Api (new endpoints) + Web (new/extended pages) + minimal enablement glue  
**Performance Goals**: Upload confirmation <1 min per file (SC-001); reconciliation orchestration <30s for typical monthly bundle  
**Constraints**: No Application domain logic changes; no Stripe writes; human approval required; export-only corrective actions  
**Scale/Scope**: Single-tenant operator; ~15 new/extended Blazor pages; 4 new API endpoint groups; 4 new/extended HTTP clients

### Dependency on prior features

| Feature | Usage |
|---------|-------|
| 001-billing-domain-model | Domain types consumed by UI/API DTOs |
| 002-giacom-pdf-ingestion | `IGiacomBillingPdfIngester` exposed via new API |
| 003-stripe-csv-ingestion | `IStripeBillingCsvIngester` exposed via new API |
| 004-reconciliation-engine | `IReconciliationEngine` orchestrated by new API |
| 005-reconciliation-exceptions | `ExceptionSurfacingService` in orchestration response |
| 006-reconciliation-classification | Classification UI + existing API |
| 007-reconciliation-approval-workflow | Extend approval UI + ingest-from-run endpoint |
| 008-reconciliation-run-history | Orchestration persists runs; polish history UI |
| 009-giacom-subscription-csv | Subscription import UI (API exists) |
| 010-retail-pricing-ingestion | Retail pricing import UI (API exists) |
| 012-stripe-catalogue-reconciliation | Catalogue UI (API exists) |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Code Quality & Maintainability | ✅ PASS | Thin API adapters; typed HTTP clients; mandatory comments on orchestration glue; reuse existing endpoint patterns |
| II. Testing Standards | ✅ PASS | Contract tests for new endpoints; existing Application tests untouched; quickstart validation scenarios |
| III. Consistent User Experience | ✅ PASS | Fluent UI v5 throughout; consistent terminology; error/empty/loading states specified; extends 007/008 patterns |
| IV. Security by Design | ✅ PASS | File size validation on uploads; no secrets in responses; operator context on approval actions; Web has no storage access |
| V. Billing Accuracy & Human Control | ✅ PASS | No auto-Stripe writes; approval workflow unchanged; export-only; reconciliation deterministic via existing engine |
| VI. Pragmatic Simplicity | ✅ PASS | Mirror 009 ingestion service pattern; extend partial UI; no new abstractions beyond typed clients; mapping persistence deferred |

### Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I | ✅ PASS | Six contracts + data model; orchestration sequence documented |
| II | ✅ PASS | quickstart.md defines 18 validation scenarios mapped to FR/SC |
| III | ✅ PASS | fluent-ui-operator-pages.md specifies nav, pages, shared components |
| IV | ✅ PASS | Upload validation; Web consumes API only |
| V | ✅ PASS | ingest-from-run preserves approval gate; no engine changes |
| VI | ✅ PASS | Enablement glue only; Complexity Tracking empty; mapping store deferred to future feature |

**Gate result**: PASS — proceed to `/speckit-tasks`.

## Project Structure

### Documentation (this feature)

```text
specs/013-v1-mvp-ui/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── giacom-pdf-import-api-endpoints.md
│   ├── stripe-csv-import-api-endpoints.md
│   ├── reconciliation-orchestration-api-endpoints.md
│   ├── approval-ingest-convenience.md
│   └── fluent-ui-operator-pages.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
BillDrift.sln
src/
├── BillDrift.Api/
│   ├── Program.cs                          # Register new services + map endpoints
│   ├── Imports/
│   │   ├── SubscriptionManagementImportEndpoints.cs  # existing
│   │   ├── RetailPricingImportEndpoints.cs           # existing
│   │   ├── GiacomPdfImportEndpoints.cs               # NEW
│   │   └── StripeCsvImportEndpoints.cs               # NEW
│   ├── Reconciliation/
│   │   └── ReconciliationEndpoints.cs                # NEW
│   ├── Approval/
│   │   └── ApprovalEndpoints.cs                      # extend ingest-from-run
│   ├── Classification/                               # existing
│   ├── CatalogueReconciliation/                      # existing
│   └── History/                                        # existing
├── BillDrift.Application/
│   ├── Import/
│   │   ├── Giacom/
│   │   │   ├── GiacomPdfIngestionService.cs          # NEW enablement glue
│   │   │   └── IGiacomPdfIngestionService.cs
│   │   └── Stripe/
│   │       ├── StripeCsvIngestionService.cs          # NEW enablement glue
│   │       └── IStripeCsvIngestionService.cs
│   └── Reconciliation/
│       └── ReconciliationOrchestrationService.cs     # NEW enablement glue
├── BillDrift.Infrastructure/
│   └── Ingestion/
│       └── IIngestionBlobStore extensions            # supplier-cost persist if needed
└── BillDrift.Web/
    ├── Program.cs                          # Register new HTTP clients
    ├── Services/
    │   ├── IngestionApiClient.cs           # NEW
    │   ├── ReconciliationApiClient.cs      # NEW
    │   ├── ClassificationApiClient.cs      # NEW
    │   ├── CatalogueReconciliationApiClient.cs  # NEW
    │   ├── ApprovalApiClient.cs            # extend
    │   └── RunHistoryApiClient.cs          # extend
    ├── Pages/
    │   ├── Home/WorkflowHomePage.razor     # NEW
    │   ├── Ingestion/IngestionHubPage.razor  # NEW
    │   ├── Reconciliation/                 # NEW
    │   ├── Mapping/MappingPage.razor       # NEW (session)
    │   ├── Classification/                 # NEW
    │   ├── Catalogue/                      # NEW
    │   ├── Approvals/                      # extend
    │   └── History/                        # extend
    └── Components/                         # shared UI components

tests/
├── BillDrift.Api.Tests/                    # NEW or extend — endpoint contract tests
└── BillDrift.Web/                          # manual quickstart validation
```

**Structure Decision**: Follow established BillDrift layering. New API endpoints in `BillDrift.Api`. Thin orchestration services in Application classified as enablement glue (mirrors 009/010 — not new domain logic). All UI in `BillDrift.Web` consuming API via typed clients only. **No BlobServiceClient in Web.**

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
| PDF import API | [contracts/giacom-pdf-import-api-endpoints.md](./contracts/giacom-pdf-import-api-endpoints.md) | ✅ Complete |
| Stripe import API | [contracts/stripe-csv-import-api-endpoints.md](./contracts/stripe-csv-import-api-endpoints.md) | ✅ Complete |
| Reconciliation orchestration | [contracts/reconciliation-orchestration-api-endpoints.md](./contracts/reconciliation-orchestration-api-endpoints.md) | ✅ Complete |
| Approval ingest convenience | [contracts/approval-ingest-convenience.md](./contracts/approval-ingest-convenience.md) | ✅ Complete |
| Fluent UI pages | [contracts/fluent-ui-operator-pages.md](./contracts/fluent-ui-operator-pages.md) | ✅ Complete |
| Quickstart | [quickstart.md](./quickstart.md) | ✅ Complete |

## Implementation Phasing

| Phase | Scope | Delivers |
|-------|-------|----------|
| **A — API enablement** | PDF import, Stripe import, reconciliation orchestration, ingest-from-run | UI can call all workflows |
| **B — Ingestion UI** | `/ingestion` hub for all 4 source types | SC-001 |
| **C — Reconciliation UI** | `/reconciliation`, exceptions, margin | SC-002, SC-004, SC-005, SC-009 |
| **D — Complete partial UI** | Approvals bulk/ingest, history polish, compare dropdowns | SC-006, SC-008 |
| **E — Remaining pages** | Mapping (session), classification, catalogue, home | SC-003, SC-010 |

## Application-Layer Freeze Checklist

Before merging each API endpoint, verify:

- [ ] No changes to reconciliation comparison rules
- [ ] No changes to exception surfacing business logic
- [ ] No changes to approval eligibility rules
- [ ] No new domain entity types in `BillDrift.Domain`
- [ ] New Application code is orchestration-only (ingest → normalize → persist → call existing service)

## Deferred (Out of Scope)

- Persistent product mapping CRUD store
- Stripe API write/apply UI
- Authentication UI
- Automated scheduled reconciliation

## Next Step

Run `/speckit-tasks` to generate dependency-ordered implementation tasks.
