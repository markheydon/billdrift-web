# Research: Stripe Catalogue Reconciliation

**Feature**: `012-stripe-catalogue-reconciliation`  
**Date**: 2026-07-03

## R1: Dedicated Pipeline vs Extending 004 Catalogue Stage

**Decision**: Implement a standalone `ICatalogueReconciliationEngine` pipeline in `BillDrift.Application.CatalogueReconciliation` that runs without subscription or customer match groups.

**Rationale**: Feature spec (US1, US5) requires catalogue validation from products/prices exports alone. The existing `CatalogueReconcileStage` in feature 004 only evaluates catalogue gaps **per `EntityMatchGroup`** derived from active subscription truth — it cannot detect missing products for offer/SKU keys with no active subscriptions, cannot scan the full Stripe catalogue for duplicates, and does not compare RRP amounts against Stripe unit prices.

**Alternatives considered**:
- *Extend `CatalogueReconcileStage` only* — rejected: wrong iteration model (customer-scoped groups vs mapping-scoped catalogue sweep).
- *Run 004 with empty subscription inputs* — rejected: produces no match groups; catalogue stage never executes.

**Coexistence**: Full four-domain reconciliation (004) retains its per-customer catalogue gap checks. Feature 012 provides proactive catalogue hygiene runs that operators trigger after Stripe/pricing imports.

---

## R2: Full Catalogue Index from Products + Prices CSV

**Decision**: Introduce `StripeCatalogueSnapshotIndex` built from normalized `RawStripeProduct` + `RawStripePrice` records (003 ingestion output), replacing the subscription-derived-only `StripeCatalogueIndex.Build(items)` for this feature.

**Rationale**: Current `StripeCatalogueIndex` is populated only from `StripeBillingItem` rows — products without active subscriptions are invisible. Catalogue reconciliation must see the complete exported catalogue.

**Alternatives considered**:
- *Reuse subscription-derived index* — rejected: misses products/prices not referenced by active subscriptions (violates SC-002).
- *Query Stripe API live* — deferred: spec MVP is CSV; same normalized snapshot shape reserved for future API retrieval.

**Implementation note**: Add `IStripeCatalogueNormalizer` to produce `StripeCatalogueProduct` / `StripeCataloguePrice` domain snapshots from raw Stripe import records, including metadata parsing for offer ID / SKU ID and active/archived state from export columns.

---

## R3: Exception and Run Domain Types

**Decision**: New domain types `CatalogueReconciliationRun`, `CatalogueException`, `CatalogueExceptionType`, and `CatalogueProposedFix` in `BillDrift.Domain.CatalogueReconciliation`. Map to existing `Mismatch` + `ProposedChange` when ingesting into approval workflow (007).

**Rationale**: Spec defines seven exception categories including unmapped entries and pricing-reference gaps that do not map 1:1 to existing `MismatchType` values. A dedicated model keeps catalogue runs independent of customer reconciliation while reusing approval infrastructure via an adapter.

**Alternatives considered**:
- *Extend `MismatchType` enum* — rejected: pollutes subscription reconciliation semantics; catalogue-only runs have no `EntityMatchGroup`.
- *Reuse only `ExceptionCategory` from 005* — rejected: those are view-model subdivisions of mismatches, not standalone run outputs.

---

## R4: Azure Storage (Blob + Table Only, Aspire DI) — Operator Constraint

**Decision**: BillDrift v1 uses **Azure Blob Storage** and **Azure Table Storage exclusively** for catalogue run persistence. **No SQL database** is introduced unless a validated requirement emerges that cannot be satisfied by blob and table storage.

Persist catalogue reconciliation runs using:
- **Azure Blob Storage** — container `catalogue-reconciliation-runs` (inputs + results JSON snapshots)
- **Azure Table Storage** — table `cataloguereconciliationruns` (queryable run index)

**Client access pattern (mandatory)**:
- Register clients in `BillDrift.Api` only via Aspire:
  ```csharp
  builder.AddAzureBlobServiceClient("blobs");
  builder.AddAzureTableServiceClient("tables");
  ```
- Infrastructure stores accept `BlobServiceClient` and `TableServiceClient` via **constructor DI** injected by Aspire.
- **Prohibited**: Manual `new BlobServiceClient(connectionString)`, `Environment.GetEnvironmentVariable` connection string parsing, or guessing storage account details from configuration unless a phenomenally good reason exists and is documented in Complexity Tracking.
- `BillDrift.Web` has **no** direct storage access; calls API over HTTP service discovery.

**Rationale**: Operator constraint and established pattern from features 006–011. Catalogue run payloads are JSON snapshots (low tens of MB) well suited to blob archive; run list/filtering suits table index. Aspire AppHost already wires `storage` → `tables` + `blobs` references to the API project.

**Alternatives considered**:
- *SQL Server / PostgreSQL for run index* — rejected: violates v1 storage policy; table index sufficient for run list/filter at reseller scale.
- *Extend `reconciliation-runs` container (008)* — rejected: different run semantics, input shape, and operator workflows; separate container avoids conflating customer reconciliation history with catalogue hygiene runs.
- *Manual connection string in Infrastructure* — rejected: breaks Aspire service discovery and emulator portability.

---

## R5: RRP Amount Comparison Rules

**Decision**: Compare Stripe `unit_amount` (minor currency units) to `IntendedPrice.Rrp` exactly; default zero tolerance. Currency must match on both sides; mismatch emits `IncorrectPrice` with currency detail, not silent conversion.

**Rationale**: Spec Assumptions and FR-010/FR-011. Stripe prices are immutable — incorrect amounts produce `CreateReplacementPrice` proposed fix (new price + flag old price), not in-place edit.

**Alternatives considered**:
- *Percentage tolerance for rounding* — deferred: configurable per currency in `CatalogueReconciliationOptions`; default exact match per spec assumption.

---

## R6: Duplicate and Conflict Detection

**Decision**:
- **Duplicate products**: Group Stripe products by resolved `(OfferId, SkuId)` from metadata and/or canonical mapping cross-reference; flag when count > 1.
- **Duplicate prices**: Group active prices per `(StripeProductId, BillingFrequency, Currency)`; flag when count > 1.
- **Conflicts**: Proposals are `FlagForManualCleanup` only (FR-014, spec US2).

**Rationale**: Aligns with approval workflow `ApprovalEligibility.CatalogueConflict` (007) and spec duplicate detection requirements.

---

## R7: Input Snapshot Assembly

**Decision**: `CatalogueReconciliationService` assembles inputs from:
1. Latest (or operator-selected) Stripe ingestion run — products + prices blobs from 003 archive
2. Latest (or selected) retail pricing ingestion run — resolved prices from 010 archive
3. Product mapping snapshot — JSON blob (`product-mappings.json`) uploaded or referenced by version ID

**Rationale**: Reuses existing ingestion persistence; avoids duplicating Stripe/pricing parse pipelines. Mapping has no dedicated persistence feature yet — catalogue run accepts mapping as part of run request or references mapping blob path consistent with 008 input snapshots.

**Alternatives considered**:
- *Embed mapping in pricing ingestion* — rejected: mapping is orthogonal to pricing; 001 domain model treats them separately.

---

## R8: Approval Workflow Integration

**Decision**: `CatalogueApprovalAdapter` converts `CatalogueProposedFix` records to `ProposedChange` + synthetic `Mismatch` (or direct `ApprovalProposal` ingestion) with `ApprovalProposalCategory.Catalogue` and eligibility rules from 007.

**Rationale**: Spec FR-020 and SC-006. Reuses existing approval store, UI, and export without duplicating decision state.

**Alternatives considered**:
- *Separate approval queue for catalogue* — rejected: violates UX consistency (constitution III) and duplicates 007 infrastructure.

---

## R9: API and Web Scope for v1

**Decision**: v1 delivers API endpoints (`POST /catalogue-reconciliation/runs`, `GET` list/detail) and defers Fluent UI pages to a follow-on task unless quickstart validation via API is sufficient for MVP.

**Rationale**: Spec is domain/reconciliation focused; 008 pattern included Web but catalogue UI can follow after engine + API prove value. Plan documents API contract; Web integration noted as Phase 2 optional in tasks.

**Alternatives considered**:
- *Full Blazor UI in same feature* — deferred to keep scope aligned with engine-first delivery and constitution VI simplicity.

---

## R10: Relationship to Feature 011

**Decision**: Feature `012-stripe-catalogue-reconciliation` is the authoritative spec/plan track for this capability. Feature `011-stripe-catalogue-reconciliation` contains an earlier spec/plan pass and may have partial or complete implementation on its branch. Implementation work should align with 012 contracts; divergences from 011 are resolved in favour of 012.

**Rationale**: User re-ran `/speckit-specify` producing spec 012 on branch `012-stripe-catalogue-reconciliation`. Codebase may already contain catalogue reconciliation types from 011 development — plan validates design against operator storage constraints without reintroducing SQL or manual storage client construction.
