# Implementation Plan: Billing Drift Domain Model

**Branch**: `001-billing-domain-model` | **Date**: 2026-07-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-billing-domain-model/spec.md`

## Summary

Define the core billing drift reconciliation domain for a Microsoft 365 Giacom reseller: raw import types for four data sources (Giacom PDFs, Subscription Management report, price list CSV, Stripe), normalized immutable entities, canonical product mapping, and reconciliation run/mismatch/proposed-change models. Implement as a pure `BillDrift.Domain` class library within a .NET Aspire solution; normalization and reconciliation engines live in `BillDrift.Application` per contracts. No UI, persistence, or API in this feature.

## Technical Context

**Language/Version**: C# 14 / .NET 10
**Primary Dependencies**: None in domain project (BCL only); solution uses .NET Aspire, Fluent UI Blazor, ASP.NET Core (downstream features)  
**Storage**: N/A for domain feature; solution uses Azure Tables/Blobs via Aspire-injected clients (Infrastructure, future)  
**Testing**: xUnit + FluentAssertions  
**Target Platform**: Azure (Aspire AppHost); domain runs anywhere .NET 10 runs  
**Project Type**: Modular .NET Aspire solution with standalone domain library  
**Performance Goals**: Reconcile 10k subscription lines in <30s single-threaded (design target)  
**Constraints**: Domain assembly zero external dependencies; deterministic reconciliation (FR-025); immutable records (FR-014)  
**Scale/Scope**: Single-tenant reseller operator; hundreds of customers, thousands of lines per reconciliation run

### Solution Architecture (user-provided)

| Layer | Technology |
|-------|------------|
| Frontend | Server-side Blazor + Fluent UI Blazor |
| Backend | ASP.NET Core Web API services |
| Orchestration | .NET Aspire AppHost |
| Storage | Azure Storage (Tables, Blobs) via DI-injected `TableServiceClient`, `BlobServiceClient` |
| Authentication | Microsoft Entra ID (future phase) |
| Secrets | Azure Key Vault |
| Deployment | Azure |

Modular layout supports future accounting/billing platform integrations via `BillDrift.Domain.Import.{Platform}` namespaces.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Code Quality & Maintainability | ✅ PASS | Domain isolated in `BillDrift.Domain`; typed public surface; no cross-source coupling |
| II. Testing Standards | ✅ PASS | Unit tests planned for value objects, price resolution, reconciliation rules |
| III. Consistent User Experience | ✅ N/A | Domain-only feature; terminology aligned (`Mismatch`, `ProposedChange`) for future UI |
| IV. Security by Design | ✅ PASS | No secrets in domain; Stripe metadata only as data fields |
| V. Billing Accuracy & Human Control | ✅ PASS | Deterministic engine contract; `ProposedChange` models approval-targeting actions without auto-apply |

### Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I | ✅ PASS | Bounded contexts documented; contracts separate normalization from reconciliation |
| II | ✅ PASS | quickstart.md defines fixture-based validation; contract tests specified |
| III | ✅ N/A | — |
| IV | ✅ PASS | Normalization failures isolated per record |
| V | ✅ PASS | Idempotency keys on `ProposedChange`; mapping ambiguity surfaces as mismatch not silent join |

**Gate result**: PASS — proceed to implementation tasks.

## Project Structure

### Documentation (this feature)

```text
specs/001-billing-domain-model/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── domain-assembly.md
│   ├── normalization.md
│   └── reconciliation-engine.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
BillDrift.sln
src/
├── BillDrift.AppHost/              # Aspire orchestrator (scaffold, minimal in domain feature)
├── BillDrift.ServiceDefaults/      # Aspire shared extensions
├── BillDrift.Web/                  # Blazor SSR + Fluent UI (scaffold only)
├── BillDrift.Api/                  # Web API (scaffold only)
├── BillDrift.Domain/               # ★ THIS FEATURE — pure domain types
│   ├── Common/
│   ├── Import/
│   │   └── Stripe/
│   ├── Billing/
│   ├── Mapping/
│   └── Reconciliation/
├── BillDrift.Application/          # Normalizers + IReconciliationEngine (interfaces + stubs)
└── BillDrift.Infrastructure/       # Empty placeholder — parsers/storage in later features

tests/
├── BillDrift.Domain.Tests/         # ★ THIS FEATURE — unit + contract tests
└── BillDrift.Application.Tests/    # Reconciliation engine tests (when implemented)
```

**Structure Decision**: .NET Aspire modular solution. Domain feature implements `BillDrift.Domain` and tests only; Application receives interface contracts. Aspire host and Blazor projects scaffolded for solution coherence but not functionally implemented in this feature.

## Complexity Tracking

> No constitution violations requiring justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Phase 0: Research

**Status**: ✅ Complete — see [research.md](./research.md)

Key decisions:
- Pure `BillDrift.Domain` library (R1)
- `readonly record struct` / `sealed record` immutability (R2)
- Deterministic idempotency key format (R3)
- `Money` with decimal + GBP default (R4)
- Normalization in Application, types in Domain (R5)
- Phased matching algorithm (R7)

## Phase 1: Design

**Status**: ✅ Complete

| Artifact | Path |
|----------|------|
| Data model | [data-model.md](./data-model.md) |
| Domain assembly contract | [contracts/domain-assembly.md](./contracts/domain-assembly.md) |
| Normalization contract | [contracts/normalization.md](./contracts/normalization.md) |
| Reconciliation engine contract | [contracts/reconciliation-engine.md](./contracts/reconciliation-engine.md) |
| Validation quickstart | [quickstart.md](./quickstart.md) |

## Phase 2: Implementation Tasks

**Status**: Pending — run `/speckit-tasks` to generate [tasks.md](./tasks.md)

Expected task groups:
1. Scaffold .NET Aspire solution and `BillDrift.Domain` project
2. Implement common value objects and validation
3. Implement raw import types
4. Implement normalized billing entities
5. Implement mapping types
6. Implement reconciliation types
7. Add `BillDrift.Domain.Tests` with fixture-based coverage
8. Stub `BillDrift.Application` interfaces per contracts

## Out of Scope (this feature)

- Giacom PDF parsing
- Stripe API/CSV ingestion
- Azure Table/Blob persistence mappings
- Blazor UI for discrepancy review
- Entra ID authentication
- Stripe write/approval workflow
- Aspire AppHost Azure provisioning

## Next Steps

1. `/speckit-tasks` — generate dependency-ordered implementation tasks
2. `/speckit-implement` — build domain library and tests
3. Future features: ingestion, infrastructure persistence, reconciliation UI
