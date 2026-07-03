# Data Model: V1 MVP Operator UI

**Feature**: `013-v1-mvp-ui`  
**Date**: 2026-07-03

This feature introduces **no new domain entities**. It exposes existing domain types through HTTP and Blazor view models. Below documents the operator-facing data shapes and UI state — not new Application-layer models.

---

## Existing Domain Types (Consumed, Not Modified)

| Type | Source feature | Role in UI |
|------|----------------|------------|
| `SubscriptionManagementIngestionRun` | 009 | Subscription CSV import history row |
| `RetailPricingIngestionRun` | 010 | Retail pricing import history row |
| `GiacomPdfIngestionRun` *(new run DTO, mirrors 009 pattern)* | 002 + enablement | PDF import history row |
| `StripeCsvIngestionRun` *(new run DTO, mirrors 009 pattern)* | 003 + enablement | Stripe CSV import history row |
| `ReconciliationRun` | 004 | Engine output; reconciliation results |
| `ReconciliationExceptionViewModel` | 005 | Exception dashboard rows |
| `ApprovalQueueViewModel` | 007 | Approval queue page |
| `ApprovalProposalViewModel` | 007 | Proposal row with prior/proposed values |
| `ReconciliationRunRecord` | 008 | Run history list/detail |
| `CatalogueReconciliationRunSummary` | 012 | Catalogue reconciliation results |
| `ProductMapping` | 001 | Inline mapping for reconciliation requests |
| `ClassificationOverride` | 006 | Per-item override state |

---

## New API DTOs (BillDrift.Api — transport only)

### `GiacomPdfIngestionRun`

Mirrors `SubscriptionManagementIngestionRun` fields:

| Field | Type | Description |
|-------|------|-------------|
| `IngestionId` | `Guid` | Run identifier |
| `OriginalFileName` | `string?` | Uploaded filename |
| `ContentFingerprint` | `string` | SHA-256 hex |
| `UploadedAt` | `DateTimeOffset` | Upload timestamp |
| `CompletedAt` | `DateTimeOffset?` | Parse completion |
| `Status` | `IngestionRunStatus` | InProgress / Completed / PartialSuccess / Failed |
| `Summary` | `GiacomPdfIngestionSummary` | Line counts, document type |
| `FailureReason` | `string?` | Error message when failed |

### `StripeCsvIngestionRun`

| Field | Type | Description |
|-------|------|-------------|
| `IngestionId` | `Guid` | Run identifier |
| `OriginalFileName` | `string?` | Primary file label |
| `ContentFingerprint` | `string` | Combined hash |
| `UploadedAt` | `DateTimeOffset` | Upload timestamp |
| `CompletedAt` | `DateTimeOffset?` | Parse completion |
| `Status` | `IngestionRunStatus` | Outcome status |
| `Summary` | `StripeCsvIngestionSummary` | Subscription/product/price counts |
| `FailureReason` | `string?` | Error when failed |

### `StartReconciliationRunRequest`

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `BillingPeriod` | `BillingPeriod` | Yes | Scope for reconciliation |
| `SupplierCostIngestionId` | `Guid?` | No | PDF ingestion run |
| `SubscriptionTruthIngestionId` | `Guid?` | No | Subscription Management CSV run |
| `IntendedPricingIngestionId` | `Guid?` | No | Retail pricing CSV run |
| `StripeBillingIngestionId` | `Guid?` | No | Stripe CSV run |
| `ProductMappings` | `ProductMapping[]` | No | Inline mappings (required for meaningful results) |
| `Options` | `ReconciliationOptions` | No | Engine tuning |
| `PersistRun` | `bool` | No | Default `true` — archive to run history |
| `InitiatorId` | `string?` | No | Operator identifier for audit |

### `ReconciliationRunResponse`

| Field | Type | Description |
|-------|------|-------------|
| `RunId` | `Guid` | Reconciliation run identifier |
| `BillingPeriod` | `BillingPeriod` | Scope |
| `Summary` | `ReconciliationRunSummary` | Mismatch counts by category, proposal count, clean-run flag |
| `Exceptions` | `ReconciliationExceptionViewModel` | Surfaced exceptions for dashboard |
| `MarginLines` | `MarginLineViewModel[]` | Cost/RRP/margin display rows (derived from run, not new domain calc) |
| `Archived` | `bool` | Whether run was persisted to history |
| `ArchiveRecord` | `ReconciliationRunRecord?` | Summary when persisted |

### `IngestApprovalsFromRunRequest`

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `IncludeInvestigationItems` | `bool` | No | Default `true` |

### `ImportHistoryFilter`

UI-only filter state (not persisted):

| Field | Type | Description |
|-------|------|-------------|
| `SourceType` | `ImportSourceKind?` | Filter by ingestion source |
| `Status` | `IngestionRunStatus?` | Filter by outcome |
| `Take` | `int` | Page size, default 20 |

---

## UI View Models (BillDrift.Web — presentation only)

### `WorkflowStatusViewModel` (Home page)

| Field | Type | Description |
|-------|------|-------------|
| `LatestImports` | `ImportStatusChip[]` | Per-source latest import status |
| `LatestRunId` | `Guid?` | Most recent reconciliation run |
| `PendingApprovalCount` | `int` | Across latest run |
| `WorkflowSteps` | `WorkflowStep[]` | Upload → Reconcile → Review → Approve → Export |

### `ExceptionDashboardFilter`

| Field | Type | Description |
|-------|------|-------------|
| `Category` | `ExceptionCategory?` | Filter by exception type |
| `CustomerMexId` | `string?` | Customer filter |
| `SearchText` | `string?` | Product/customer search |

### `MarginLineViewModel`

| Field | Type | Description |
|-------|------|-------------|
| `CustomerLabel` | `string` | Customer display name |
| `ProductLabel` | `string` | Product display name |
| `Cost` | `Money?` | Supplier cost (null if unavailable) |
| `Rrp` | `Money?` | Intended RRP (null if unavailable) |
| `MarginAmount` | `Money?` | RRP − Cost |
| `MarginPercent` | `decimal?` | Margin percentage |
| `Severity` | `MarginSeverity` | Healthy / Low / Negative / Unknown |

---

## State Transitions

### Ingestion run (all source types)

```text
[Upload received] → InProgress → Completed | PartialSuccess | Failed
```

Failed runs retain `IngestionId` and `FailureReason` for operator diagnosis.

### Reconciliation operator workflow

```text
Upload inputs → Start reconciliation → Review exceptions → Ingest proposals
  → Approve/reject → Export changeset
```

### Approval proposal states (unchanged from 007)

```text
Pending → Approved | Rejected | Stale
```

---

## Validation Rules (UI + API)

| Rule | Enforced by |
|------|-------------|
| PDF max 20 MB | API (matches 002 intake) |
| CSV max size per source options | API |
| Stripe upload requires subscriptions.csv | API |
| Reconciliation warns when required ingestion IDs missing | API + UI pre-check |
| Reject proposal requires non-empty reason | UI + API |
| Bulk approve requires preview confirmation | UI |
| Export contains approved items only | API (007) |
| No automatic Stripe writes | API + UI (export only) |

---

## Relationships

```text
Import Runs (4 source types)
    ↓ referenced by ingestion ID
Reconciliation Run
    ↓ produces
Exceptions + Proposals
    ↓ ingest-from-run
Approval Queue
    ↓ approve/reject
Approved Changeset Export

Catalogue Reconciliation Run (parallel path)
    ↓ ingest-approvals
Approval Queue (Catalogue tab)
```

Product mappings: supplied inline on reconciliation/catalogue requests; optionally viewed from run-history `product-mappings` input domain — **no persistent mapping entity in this feature**.
