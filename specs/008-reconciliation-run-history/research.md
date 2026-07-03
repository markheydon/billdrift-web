# Research: Reconciliation Run History & Audit

**Feature**: `008-reconciliation-run-history`  
**Date**: 2026-07-02

## R1: Storage Topology — Table + Blob Hybrid

**Decision**: Azure Table Storage for queryable metadata and indexes; Azure Blob Storage for large JSON snapshots.

**Rationale**: A single reconciliation run may contain thousands of normalized lines plus full match groups, mismatches, and proposals — easily exceeding Table Storage's 64 KB entity limit. Tables excel at listing runs by billing period, filtering by date, and indexing drift fingerprints for trend queries. This mirrors the proven 007 approval pattern (table state + blob exports).

**Alternatives considered**:
- **Table-only**: Rejected — entity size limits require chunking logic with no query benefit.
- **Blob-only**: Rejected — listing/filtering runs requires full blob scans; unacceptable for operator UX.
- **SQL database**: Rejected per user guardrail and constitution VI; no validated requirement yet.

---

## R2: Per-Run Blob Layout

**Decision**: Container `reconciliation-runs` with prefix `{runId}/`:

```text
{runId}/manifest.json
{runId}/inputs/supplier-cost.json
{runId}/inputs/subscription-truth.json
{runId}/inputs/intended-pricing.json
{runId}/inputs/stripe-billing.json
{runId}/inputs/product-mappings.json
{runId}/results/match-groups.json
{runId}/results/mismatches.json
{runId}/results/proposed-changes.json
```

**Rationale**: Separate blobs per domain allow partial retrieval (e.g., pricing drift reads only pricing + stripe inputs). Manifest holds hashes, counts, and cross-references. Missing domains write a `{ "present": false }` stub.

**Alternatives considered**:
- **Single monolithic blob per run**: Simpler writes but forces full download for any detail view; rejected for SC-001 latency.
- **Separate container per domain**: Overhead without benefit for single-tenant v1.

---

## R3: Cross-Run Mismatch Identity

**Decision**: Introduce `StableMismatchKey` — deterministic string derived from `MexId` + `CommercialKeyRoot` (or product label fallback) + `MismatchType` + normalized distinguishing token (quantity/price interval/amount bucket).

**Rationale**: `MismatchId` and `IdempotencyKey` are run-scoped (include `RunId`). Drift trends and month-to-month comparison require a key stable across runs for the same underlying business condition.

**Alternatives considered**:
- **Reuse `IdempotencyKey`**: Rejected — includes `RunId`, not stable across runs.
- **Reuse `MismatchId` GUID**: Rejected — new GUID each run.
- **Fuzzy description matching**: Rejected — non-deterministic, breaks SC-004.

See [mismatch-comparison-rules.md](./contracts/mismatch-comparison-rules.md) for algorithm.

---

## R4: Mapping Version Reference

**Decision**: Record `MappingVersionReference` with `{ VersionId, ContentHash, EffectiveDate, Label }` where `ContentHash` is SHA-256 of serialized `ProductMappings` ordered by key.

**Rationale**: Product mappings may not have formal semver releases in v1. Content hash provides deterministic version identity; operator label optional for display. Comparison reports flag hash changes between runs.

**Alternatives considered**:
- **Git commit hash of mapping file**: Requires git integration at runtime; rejected for simplicity.
- **Manual version string only**: No integrity verification; rejected.

---

## R5: Project Layer Split

**Decision**: `BillDrift.Domain.History` + `BillDrift.Application.History` + `BillDrift.Infrastructure.History` + API endpoints + Web pages.

**Rationale**: Consistent with 007 approval and 006 classification patterns. Domain holds records and analysis result types; Application owns comparison/trend algorithms; Infrastructure owns Azure serialization.

**Alternatives considered**:
- **Extend `BillDrift.Domain.Reconciliation` only**: Mixes ephemeral run output with persistence concerns; rejected.
- **Single Infrastructure project without Application services**: Comparison logic belongs in testable Application layer.

---

## R6: Aspire DI Storage Clients

**Decision**: Register via existing AppHost storage resource; inject `TableServiceClient` and `BlobServiceClient` in Infrastructure only.

**Rationale**: User guardrail and constitution IV. Matches `ApprovalStorageExtensions` and `ClassificationStorageExtensions` patterns.

**Alternatives considered**:
- **Manual connection string from env**: Explicitly prohibited by user guardrail.

---

## R7: Approval Status Join Strategy

**Decision**: Run blob snapshots store proposals as reconciliation produced them. Approval decision state is joined at read time from `IApprovalStore` by `RunId` + `ProposedChangeId` / `IdempotencyKey`.

**Rationale**: Avoids stale duplicated state in immutable run records. 007 remains system of record for decisions. Satisfies FR-015 and spec assumption.

**Alternatives considered**:
- **Snapshot approval state into run record on persist**: Would go stale when decisions change; rejected.
- **Event-driven sync to history store**: Over-engineered for v1; deferred.

---

## R8: Drift Index Denormalization

**Decision**: On persist, write one `drift` partition table row per `(RunId, StableMismatchKey)` with summary fields for trend queries.

**Rationale**: Trend analysis over six months (SC-004) querying blob payloads for every run is too slow. Denormalized index enables `StableMismatchKey` aggregation via table queries.

**Alternatives considered**:
- **Compute trends from blobs on every request**: Acceptable for MVP with <24 runs/year but fails SC-004 performance at scale; index chosen for headroom.
- **Materialized view in SQL**: Prohibited.

---

## R9: Comparison and Trend Computation Location

**Decision**: `RunComparisonService`, `DriftTrendAnalyzer`, and `PricingDriftAnalyzer` in Application layer; deserialize blobs in Infrastructure store, pass domain snapshots to analyzers.

**Rationale**: Pure functions over immutable data — highly testable without Azure. No SQL analytics engine needed.

**Alternatives considered**:
- **Pre-compute comparison reports on persist**: Only two-run compare needed on demand; storing all pairs wasteful.
- **Background job queue**: Out of scope for v1 manual-upload cadence.

---

## R10: Retention and Archival

**Decision**: Table row `ArchivedAt` + `RetentionExpiresAt` fields; blob access tier metadata set to Cool/Archive when policy triggers. Runs remain listable with `IsArchived=true`.

**Rationale**: Satisfies FR-017/FR-018 without SQL. Default 24-month online availability configurable via `RunHistoryStorageOptions`.

**Alternatives considered**:
- **Hard delete after retention**: Rejected — audit requirement favors discoverability with archived status.
- **Separate archive storage account**: Deferred until scale requires it.

---

## R11: Manual Upload Input Metadata (Phase 1 Build Queue)

**Decision**: Input snapshot metadata populated from ingestion upload pipeline: filename, upload timestamp, SHA-256 fingerprint, billing period scope. Normalized payloads come from same ingestion output fed to reconciliation.

**Rationale**: User build queue specifies Phase 1 = manual uploads for all sources (features 008, 015–018). History feature consumes ingestion metadata; does not implement upload UI itself.

**Alternatives considered**:
- **Defer metadata until API integrations (Phase 2)**: Would leave FR-003 unmet; rejected.

---

## R12: Testing Strategy

**Decision**:
- `InMemoryRunHistoryStore` for Application unit tests
- Azurite integration tests for table/blob stores
- Multi-run JSON fixtures under `tests/fixtures/run-history/` for compare/trend algorithms
- API integration tests for list/detail/compare/trends endpoints

**Rationale**: Constitution II; mirrors 007 testing pyramid. Algorithm tests use concrete deserialized snapshots — no interface mocking for comparison logic.

**Alternatives considered**:
- **Table-only integration tests**: Insufficient — blob round-trip must be verified.
