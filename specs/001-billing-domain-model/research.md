# Research: Billing Drift Domain Model

**Feature**: `001-billing-domain-model`  
**Date**: 2026-07-01

## R1: Domain Project Placement in .NET Aspire Solution

**Decision**: Implement all domain types in a standalone class library `BillDrift.Domain` with zero dependencies on Aspire, Azure SDK, Blazor, or Stripe client packages.

**Rationale**: The spec (FR-026) and constitution (Principle I) require domain logic isolated from infrastructure. A pure library enables fast unit tests, deterministic reconciliation tests, and future reuse by CLI or background workers without pulling web stack dependencies.

**Alternatives considered**:
- *Domain types inside `BillDrift.Application`* — rejected; couples entities to use-case orchestration and encourages infrastructure leaks.
- *Shared DTO project for raw + normalized* — rejected; violates raw/normalized separation (FR-015).

---

## R2: C# Type Style — Records and Value Objects

**Decision**: Use `readonly record struct` for small value objects (identifiers, money, commercial keys) and `sealed record` for entity aggregates (normalized lines, reconciliation run). Use `enum` for closed sets (ChargeType, MismatchType, etc.).

**Rationale**: Immutability is a spec requirement (FR-014). Records provide value equality, `with` expressions for derived corrections, and align with modern C# conventions (FR-027). Struct records avoid heap allocation for frequently compared keys during matching.

**Alternatives considered**:
- *Mutable classes with private setters* — rejected; harder to guarantee determinism and thread safety during reconciliation.
- *Source generators for value objects* — rejected for v1; adds complexity without current need.

---

## R3: Identifier and Idempotency Key Strategy

**Decision**:
- **Entity IDs**: `Guid` for domain-generated IDs (`SupplierCostLineId`, `MismatchId`, etc.); opaque `string` wrappers for external IDs (`StripeSubscriptionId`, `RawImportId`).
- **Idempotency keys**: Deterministic string composed as `{RunId}:{MismatchId}:{ProposedActionType}` (per spec assumption); implemented via `IdempotencyKey` value object with validated format.
- **Raw import deduplication**: Composite key `{SourceKind}:{SourceDocumentId}:{SourceLineKey}` where `SourceLineKey` is supplier reference ID, Stripe object ID, or `{RowIndex}` for CSV.

**Rationale**: Supports FR-003, FR-024, FR-025. Guid IDs avoid collision during in-memory tests; string wrappers prevent accidental cross-typing of Stripe vs Giacom IDs.

**Alternatives considered**:
- *ULID for all IDs* — deferred; Guid sufficient for v1, ULID adds dependency.
- *Hash-based idempotency only* — rejected; harder to debug operator-facing audit trails.

---

## R4: Money and Currency Handling

**Decision**: `Money` value object with `decimal Amount` and `CurrencyCode` (ISO 4217 string, default `GBP`). All comparisons use exact decimal equality; no floating point.

**Rationale**: Spec assumes single currency per reseller (GBP). Decimal is standard for billing in .NET. Multi-currency deferred.

**Alternatives considered**:
- *Minor units (long pence)* — viable but adds conversion noise for v1 single-currency scope.
- *NodaMoney library* — rejected for v1; domain stays dependency-free.

---

## R5: Normalization Boundary

**Decision**: Normalization is defined as pure functions / services in `BillDrift.Application` (future), accepting raw import types from `BillDrift.Domain.Import` and returning normalized types from `BillDrift.Domain`. Domain project defines both type families and validation rules only — no parsing logic in domain.

**Rationale**: Keeps PDF/CSV parsing in infrastructure while domain owns the shape contracts ingestion must produce.

**Alternatives considered**:
- *Normalization methods on raw records* — rejected; mixes layers and complicates testing raw fidelity.

---

## R6: Product Mapping Resolution

**Decision**: `ProductMapping` indexed by `OfferId` + `SkuId`; supplier name resolution via case-insensitive normalized string dictionary built at reconciliation input assembly time. Ambiguity (multiple mappings for same variant) yields `MappingAmbiguous` mismatch.

**Rationale**: Implements FR-016–FR-018 and user story 3 acceptance scenarios.

**Alternatives considered**:
- *Fuzzy string matching at runtime* — rejected; non-deterministic, violates FR-025.

---

## R7: Reconciliation Matching Algorithm (Domain Contract)

**Decision**: Matching proceeds in ordered phases:
1. Resolve customer by `MexId` (required on all sides except price list).
2. Resolve product via `CommercialKey` or `ProductMapping` from supplier name.
3. Group into `EntityMatchGroup` with at most one entity per domain per group.
4. Emit mismatches by rule priority: mapping issues first, then missing Stripe, quantity, frequency, price, catalogue.

**Rationale**: Deterministic ordering ensures stable output (FR-025). Mapping-first prevents false quantity/price mismatches on wrong product joins.

**Alternatives considered**:
- *Single-pass join on product name* — rejected; fragile across Giacom naming variants.

---

## R8: Testing Framework

**Decision**: xUnit + FluentAssertions in `BillDrift.Domain.Tests`. Property-based tests considered for CommercialKey equality only in later tasks.

**Rationale**: Constitution Principle II (NON-NEGOTIABLE) requires unit tests for reconciliation logic. xUnit is .NET default; FluentAssertions improve mismatch assertion readability.

**Alternatives considered**:
- *NUnit* — equivalent; xUnit chosen for Aspire template alignment.

---

## R9: Modular Architecture for Future Platforms

**Decision**: Namespace and folder layout use bounded contexts:
- `BillDrift.Domain.Import.*` — raw types per supplier
- `BillDrift.Domain.Billing.*` — normalized billing entities
- `BillDrift.Domain.Mapping` — product mapping
- `BillDrift.Domain.Reconciliation` — runs, matches, mismatches, proposed changes
- `BillDrift.Domain.Common` — shared value objects

Future accounting platforms add `BillDrift.Domain.Import.{Platform}` without changing reconciliation core.

**Rationale**: User-requested modular architecture for multiple billing platforms.

---

## R10: Persistence Mapping (Deferred)

**Decision**: No EF Core entities or Azure Table entity types in this feature. Infrastructure will map domain types to Azure Table/Blob storage in a separate feature. Domain types remain persistence-ignorant.

**Rationale**: FR-026 explicit scope boundary. Azure Storage noted in technical approach for solution-level planning only.

---

## R11: Authentication and Secrets (Deferred)

**Decision**: Microsoft Entra ID and Azure Key Vault integration excluded from domain model feature; documented in plan Technical Context as future AppHost concerns.

**Rationale**: User stated Entra ID is future phase; domain has no auth surface.

---

## Resolved Unknowns

| Unknown | Resolution |
|---------|------------|
| Language/version | C# 14 / .NET 10 |
| Testing | xUnit + FluentAssertions |
| Storage in domain | None — persistence deferred |
| Auth in domain | None — deferred |
| Performance targets | Reconcile 10k subscription lines in <30s single-threaded (design goal, not hard SLA) |
| Scale | Single-tenant reseller operator tool; hundreds of customers, thousands of lines per run |
