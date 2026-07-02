# Research: Reconciliation Item Classification

**Feature**: `006-reconciliation-classification`  
**Date**: 2026-07-02

## R1: Classification vs existing `ProductClassification`

**Decision**: Introduce a separate domain enum `ReconciliationItemClassification` with four values (`MicrosoftCsp`, `NonCspSupplier`, `Internal`, `CustomService`). Keep existing `ProductClassification` (`Csp` / `NonCsp`) on `ProductMapping` as a mapping hint; reconciliation item classification is computed per item from multiple signals and may diverge from mapping classification until operator override.

**Rationale**: Spec FR-001 requires four origin types including Internal and Custom/service, which are customer- and billing-context classifications not expressible in the binary CSP/non-CSP mapping enum. Separation avoids overloading `ProductMapping.Classification` and preserves backward compatibility with 004 engine behaviour during migration.

**Alternatives considered**:
- **Extend `ProductClassification` enum** — Rejected: Internal/Custom are item-level, not product-catalogue attributes; would conflate mapping table with per-line reconciliation state.
- **Application-only enum** — Rejected: classification affects reconciliation semantics and persisted audit history; belongs in Domain as a first-class billing concept.

---

## R2: Stable item identity for persistence

**Decision**: Define `ReconciliationItemRef` (Domain) with `ItemKind` (`SupplierCost`, `SubscriptionTruth`, `StripeBilling`) and a deterministic `StableKey` string derived from business identifiers, not normalization GUIDs:

| Kind | StableKey format |
|------|------------------|
| `SupplierCost` | `{mexId}:supplier:{primarySupplierRef or hash(productName+periodStart)}` |
| `SubscriptionTruth` | `{mexId}:truth:{offerId}:{skuId}:{supplierSubscriptionId or tenantId}` |
| `StripeBilling` | `{mexId}:stripe:{subscriptionItemId}` |

Domain entity `Id` (GUID) is carried for in-run correlation but **Table row keys use `StableKey`** so overrides survive re-ingestion.

**Rationale**: FR-009/FR-010 require durable overrides across reconciliation runs. Normalization assigns new GUIDs per import; business keys from Stripe IDs, offer/SKU, and supplier references are stable.

**Alternatives considered**:
- **GUID-only persistence** — Rejected: overrides lost on every re-import.
- **Single composite commercial key for all kinds** — Rejected: supplier-only and Stripe-only lines lack shared offer/SKU; would collide.

---

## R3: Layer placement and pipeline integration

**Decision**: Three-layer split:

1. **Domain** — `ReconciliationItemClassification`, `ItemClassification`, `ClassificationOverride`, `ClassificationRuleConfiguration`, `ReconciliationItemRef`, `ClassificationHistoryEntry`
2. **Application** — `ClassificationService` (rule engine + orchestration), `IClassificationRuleEngine` internal concrete rules, integration hooks in reconciliation pipeline via new `ClassificationEnrichmentStage` (runs before `MatchGroupBuildStage`) and `ClassificationContext` on `ReconciliationContext`
3. **Infrastructure** — `AzureTableItemClassificationStore` implementing `IItemClassificationStore` using DI-injected `TableServiceClient`; config blob optional for large rule exports only

Reconciliation engine consumes `ClassificationContext` (read-only map `StableKey → ItemClassification`). Exception surfacing adds suppression rule **SR-6** for Internal items (suppress `MissingBillingItem`).

**Rationale**: Rule logic is billing-critical and testable without Azure (Application + in-memory store for tests). External persistence isolated per Principle VI and constitution storage guidance.

**Alternatives considered**:
- **Classification inside `MatchGroupBuildStage`** — Rejected: mixes concerns; rules need inputs from all domains before matching.
- **Post-reconciliation classification** — Rejected: engine and surfacing need classification before mismatch detection.

---

## R4: Rule precedence and conservative defaults

**Decision**: Implement explicit ordered rule chain (spec FR-007):

1. **Manual override** (from store) — short-circuit if active override exists
2. **Internal customer** — `config.InternalMexIds.Contains(item.Customer.MexId)`
3. **Custom/service** — Stripe-only item + product category rule `CustomService`, OR mapping category rule, OR no supplier/truth evidence with Stripe billing present
4. **Non-CSP supplier** — supplier cost evidence present AND no subscription truth line for same customer + product correlation key within scope
5. **Microsoft CSP** — offer ID + SKU ID present AND (truth line OR intended price list entry) AND product category `Microsoft365`

When multiple signals at same tier conflict, set `ClassificationConfidence` to `Medium` or `Low` and record all signals in `RuleBasis`. When no rule reaches high confidence, default to `NonCspSupplier` with `Low` confidence (FR-018 conservative default).

**Product category resolution order**: manual category rule → `ProductMapping` metadata (future field) → subscription truth product family → `Other`.

**Alternatives considered**:
- **Score-based weighted classifier** — Rejected: harder to audit and test; violates determinism transparency.
- **Default Microsoft CSP when offer/SKU present** — Rejected: causes false positives for partial metadata.

---

## R5: Azure Table Storage schema

**Decision**: Single table `ItemClassifications` (name configurable via options, default `itemclassifications`):

| Entity | PartitionKey | RowKey | Payload |
|--------|--------------|--------|---------|
| Current classification | `item` | `{StableKey}` (URL-safe) | Classification, Source, RuleBasis, Confidence, OverrideNotes, OperatorId, UpdatedAt |
| History entry | `hist` | `{StableKey}:{Ticks}` | Prior, New, Source, Notes, OperatorId, Timestamp |
| Config: internal Mex IDs | `config` | `internal-mex-ids` | JSON array of Mex ID strings |
| Config: product categories | `config` | `product-category-rules` | JSON rule list |

Use `TableClient` from injected `TableServiceClient.GetTableClient(tableName)`. Create table on first use if not exists (idempotent). No manual connection string parsing.

**Rationale**: User constraint — Azure Tables for structured keyed lookups; small config documents fit single entities. History partitioned separately to avoid hot-row growth on current state entity.

**Alternatives considered**:
- **SQL database** — Rejected per project v1 constraint.
- **Blob-only persistence** — Rejected: keyed lookup per item during reconciliation requires table semantics; blobs reserved for optional config snapshots/exports.
- **One row per history in same partition as item** — Rejected: unbounded row growth on hot partition; separate `hist` partition with time-based row keys.

---

## R6: Aspire storage wiring

**Decision**: Extend `BillDrift.AppHost/AppHost.cs`:

```csharp
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var tables = storage.AddTables("tables");
var blobs = storage.AddBlobs("blobs");

var api = builder.AddProject<Projects.BillDrift_Api>("api")
    .WithReference(tables)
    .WithReference(blobs)
    ...
```

Register in API/Infrastructure:

```csharp
builder.AddAzureTableServiceClient("tables");
builder.AddAzureBlobServiceClient("blobs");
```

Infrastructure `ClassificationStorageExtensions` registers `IItemClassificationStore` as scoped, injecting `TableServiceClient` only.

**Rationale**: User mandate — Aspire-provided DI clients, no environment guessing. Emulator for local dev.

**Alternatives considered**:
- **Construct `TableServiceClient` from `ConnectionStrings:Storage`** — Rejected per user constraint.

---

## R7: Reconciliation engine integration points

**Decision**: Replace direct `ProductMapping.Classification == NonCsp` checks in `MatchGroupBuildStage`, `SupplierCostReconcileStage`, `MismatchDetector`, and `ProposedChangeFactory` with `ClassificationContext.Get(ref).Classification` when present; fall back to mapping classification only when item not yet classified (transition period).

| Classification | Engine behaviour |
|----------------|------------------|
| `Internal` | Skip `MissingInStripe` emission for subscription truth lines; still run quantity/price checks when Stripe item matched |
| `NonCspSupplier` | Same as current non-CSP path — `MappingMissing` with manual review prefix, no bill-impacting proposals |
| `CustomService` | Skip truth-vs-Stripe missing-billing for truth absence; flag orphaned Stripe review per scope |
| `MicrosoftCsp` | Standard CSP matching path |

**Alternatives considered**:
- **Duplicate logic in exception surfacing only** — Rejected: engine would still emit false mismatches; surfacing suppression alone violates FR-012 clarity.

---

## R8: Exception surfacing SR-6 (Internal suppression)

**Decision**: Add suppression rule **SR-6** in `SuppressPhase`: when `MissingBillingItem` candidate's match group subscription line (or group key) has `ItemClassification.Internal`, suppress exception. Attach `SuppressionRule.ClassificationInternal` audit record.

Pass `ClassificationContext` into `SurfacingContext` from run metadata or parallel lookup by `StableKey`.

**Rationale**: FR-012; defence in depth if engine guard missed. Aligns with 005 suppression pattern.

---

## R9: Testing strategy

**Decision**:
- **Unit tests** — each rule in isolation with in-memory `IItemClassificationStore` fake (concrete test double class, not interface-only mock)
- **Integration tests** — `AzureTableItemClassificationStore` against Azurite emulator (optional CI job) + mandatory in-memory store tests for CI speed
- **Pipeline tests** — extend reconciliation fixtures: `internal-customer-no-missing-billing`, `non-csp-manual-review`, `custom-service-stripe-only`, `override-precedence`, `classification-determinism`
- **Golden comparison** — classification snapshot JSON per fixture

**Rationale**: Constitution Principle II; billing-critical classification rules require regression fixtures per type.

---

## R10: API surface and UI deferral

**Decision**: Expose minimal API endpoints in `BillDrift.Api` for override CRUD and config read (no Blazor UI in this feature):

- `GET /api/classifications/{stableKey}`
- `PUT /api/classifications/{stableKey}/override`
- `DELETE /api/classifications/{stableKey}/override`
- `GET /api/classification-config`
- `PUT /api/classification-config/internal-mex-ids`

Future UI feature will use Fluent UI Blazor per project skill; this feature delivers API + Application service only.

**Rationale**: Spec out-of-scope for UI; FR-008/FR-016 still need programmatic persistence path for tests and future Blazor forms.

---

## R11: Relationship to `ProductMapping`

**Decision**: `ProductMapping.Classification` remains a **hint** for product catalogue maintenance. `ReconciliationItemClassification` is authoritative for reconciliation behaviour. When both exist and disagree, item classification wins unless mapping is used only as a signal in automatic rule chain (tier 5 CSP / tier 4 non-CSP).

Migration: existing tests using `ProductClassification.NonCsp` continue to pass; new classification fixtures set explicit item classifications.

**Rationale**: Avoids breaking 001/004 mapping model while centralising per-item behaviour.
