# Feature Specification: Stripe Catalogue Reconciliation

**Feature Branch**: `011-stripe-catalogue-reconciliation`

**Created**: 2026-07-03

**Status**: Draft

**Input**: User description: "Design catalogue reconciliation to ensure Stripe Products and Prices are correct. Stripe is the source of truth for billing, but catalogue must align to intended RRP strategy. Inputs: Stripe products.csv and prices.csv (or API), pricing reference from SPECIFY 16, canonical mapping model from SPECIFY 1. Checks: For each canonical product: Stripe Product exists, Stripe Prices exist for required term/frequency combos, Stripe price amounts equal expected RRP, Detect duplicates (multiple Stripe products/prices for same Offer/SKU). Output: Catalogue exceptions (missing product, missing price, incorrect price, duplicates / conflicts), Proposed catalogue fixes (for approval workflow)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reconcile Stripe Catalogue Against Intended Retail Pricing (Priority: P1)

As a billing operator, I need to run catalogue reconciliation using the current Stripe product and price catalogue together with intended retail pricing so that I can see whether every billable product in my canonical mapping has the correct Stripe products and prices before subscription reconciliation flags customer-level price drift.

**Why this priority**: Subscription price mismatch detection depends on a correct catalogue. Validating catalogue structure and RRP alignment upstream prevents false positives and surfaces setup gaps operators must fix once for all customers.

**Independent Test**: Given Stripe products and prices fixtures, a canonical product mapping covering three offer/SKU combinations, and intended pricing records with RRP for each required term and billing frequency, reconciliation produces a complete exception list and proposed fixes without requiring live subscription data.

**Acceptance Scenarios**:

1. **Given** a canonical product with intended pricing for monthly and annual billing frequencies, **When** catalogue reconciliation runs, **Then** each required term/frequency combination is checked for a matching Stripe price with the expected RRP amount.
2. **Given** a canonical product with no corresponding Stripe product in the catalogue snapshot, **When** reconciliation completes, **Then** a missing-product exception is recorded with offer ID, SKU ID, normalized product identity, and mapping reference.
3. **Given** a Stripe product exists but a required price for a term/frequency is absent, **When** reconciliation completes, **Then** a missing-price exception is recorded naming the commercial key, expected interval, and intended RRP.
4. **Given** a Stripe price exists for the correct interval but the unit amount differs from intended RRP, **When** reconciliation completes, **Then** an incorrect-price exception records expected RRP, actual Stripe amount, currency, and commercial key.

---

### User Story 2 - Detect Duplicate and Conflicting Catalogue Entries (Priority: P1)

As a billing operator, I need duplicate or conflicting Stripe products and prices for the same commercial offer/SKU key identified during catalogue reconciliation so that ambiguous catalogue entries are resolved before they cause inconsistent subscription billing.

**Why this priority**: Multiple Stripe products or prices mapped to the same offer/SKU create unpredictable subscription behaviour and mapping ambiguity. Surfacing conflicts early is essential for catalogue hygiene.

**Independent Test**: Given a Stripe catalogue fixture with two products both carrying metadata for the same offer ID and SKU ID, and two active prices on one product for the same billing interval, reconciliation emits duplicate/conflict exceptions without proposing automatic merge or deletion.

**Acceptance Scenarios**:

1. **Given** two or more Stripe products associated with the same offer ID and SKU ID via mapping metadata or canonical mapping, **When** reconciliation completes, **Then** a duplicate-product conflict exception lists all conflicting product identities.
2. **Given** one Stripe product with two or more active prices for the same billing interval and currency, **When** reconciliation completes, **Then** a duplicate-price conflict exception lists all conflicting price identities for that interval.
3. **Given** a duplicate or conflict exception, **When** proposed fixes are generated, **Then** the proposal is limited to flagging for manual cleanup and does not include automatic delete or merge actions.

---

### User Story 3 - Propose Catalogue Fixes for Human Approval (Priority: P1)

As a billing operator, I need each catalogue exception accompanied by a clear proposed corrective action suitable for the existing approval workflow so that missing products, missing prices, and incorrect amounts can be reviewed and approved before any Stripe catalogue change is applied.

**Why this priority**: Catalogue changes affect all future billing. Proposed fixes must be explainable and gated by human approval consistent with BillDrift's billing safety model.

**Independent Test**: Given exceptions of each type (missing product, missing price, incorrect price, duplicate/conflict), reconciliation attaches proposed fixes with action type, target commercial key, prior Stripe state, proposed values, and rationale; duplicate/conflict proposals are non-actionable flags only.

**Acceptance Scenarios**:

1. **Given** a missing-product exception, **When** a proposed fix is generated, **Then** it proposes creating a Stripe product with normalized name, mapping metadata (offer ID, SKU ID), and links to the canonical mapping entry.
2. **Given** a missing-price exception, **When** a proposed fix is generated, **Then** it proposes creating a price on the identified or proposed product with the intended RRP amount, currency, billing interval, and term/frequency dimensions from intended pricing.
3. **Given** an incorrect-price exception where Stripe prices are immutable by amount, **When** a proposed fix is generated, **Then** it proposes creating a new price with the correct RRP and retiring or flagging use of the incorrect price, without silently modifying the existing price amount.
4. **Given** any catalogue proposed fix, **When** it enters the approval workflow, **Then** it is presented separately from subscription corrections and requires explicit operator approval before export or application.

---

### User Story 4 - Scope Reconciliation to Canonical Mapped Products (Priority: P2)

As a reconciliation operator, I need catalogue reconciliation driven by the canonical product mapping and intended pricing reference so that only products the business expects to bill are checked, and unmapped Stripe catalogue entries are surfaced separately for review.

**Why this priority**: Checking the entire Stripe account catalogue without a mapping anchor produces noise. Canonical mapping defines the expected product universe; orphan Stripe entries indicate setup drift or legacy items.

**Independent Test**: Given a canonical mapping with five products, intended pricing for four of them, and a Stripe catalogue containing six products (five mapped, one orphan), reconciliation checks the four with pricing, flags the mapped product missing pricing as a mapping/pricing gap, and reports the orphan Stripe product as unmapped catalogue entry.

**Acceptance Scenarios**:

1. **Given** a canonical mapping entry with offer ID, SKU ID, and expected term/frequency price slots, **When** reconciliation runs, **Then** checks are performed for each mapped product using the mapping's Stripe product ID when present, or by resolving via metadata when absent.
2. **Given** intended pricing with no entry for a mapped commercial key, **When** reconciliation runs, **Then** a pricing-reference gap is recorded and price amount comparison is skipped for that key until pricing is available.
3. **Given** a Stripe product or price with no corresponding canonical mapping or offer/SKU metadata, **When** reconciliation runs, **Then** an unmapped catalogue entry warning is recorded without proposing automatic deletion.

---

### User Story 5 - Run Catalogue Reconciliation from Export Snapshots (Priority: P2)

As a billing operator, I need to run catalogue reconciliation from uploaded Stripe products and prices exports (and optionally live catalogue retrieval later) so that I can validate catalogue correctness on the same schedule as other BillDrift ingestion workflows without manual spreadsheet comparison.

**Why this priority**: CSV export is the established MVP ingestion path for Stripe data in BillDrift. Catalogue reconciliation must consume the same normalized catalogue collections produced by Stripe ingestion.

**Independent Test**: Given ingested products and prices collections from CSV fixtures plus intended pricing and canonical mapping inputs, catalogue reconciliation executes deterministically and produces the same exception set on repeat runs with identical inputs.

**Acceptance Scenarios**:

1. **Given** normalized Stripe product and price collections from CSV ingestion, **When** catalogue reconciliation runs, **Then** no re-parsing of raw CSV is required and all checks use the normalized catalogue snapshot.
2. **Given** identical catalogue, mapping, and pricing snapshots, **When** reconciliation runs twice, **Then** the exception set and proposed fixes are identical (deterministic output).
3. **Given** a reconciliation run completes, **When** the operator reviews the summary, **Then** counts by exception type (missing product, missing price, incorrect price, duplicate/conflict, unmapped) are available alongside per-item detail.

---

### Edge Cases

- Canonical mapping references a Stripe product ID that no longer exists in the catalogue snapshot — treated as missing product with mapping stale reference noted.
- Intended pricing marks a product End of Sale but Stripe catalogue still has active prices — RRP comparison still runs; end-of-sale status is visible on the exception or summary for operator context.
- Manual price override takes precedence over catalogue RRP for the same commercial key — reconciliation uses the effective intended retail price from the pricing reference resolution rules (manual override wins).
- Stripe price is archived or inactive but still referenced by subscriptions — flagged as catalogue hygiene issue; proposed fix distinguishes inactive incorrect prices from active ones.
- Currency mismatch between intended pricing and Stripe price — incorrect-price or unsupported-currency exception; no silent currency conversion.
- Zero-decimal currencies or minor-unit rounding differences — comparison uses currency-appropriate rules; immaterial rounding differences below configured tolerance are not flagged (see Assumptions).
- Product mapping has low confidence or ambiguous mapping — reconciliation flags mapping-ambiguous before asserting missing or incorrect catalogue entries.
- Multiple term lengths share the same billing frequency label in Stripe metadata — term and frequency dimensions from intended pricing and canonical mapping are both required to select the correct expected price slot.
- Stripe catalogue contains draft or test-mode products mixed with live catalogue — reconciliation scope assumes a single environment snapshot; cross-environment mixing is out of scope for one run.

## Requirements *(mandatory)*

### Functional Requirements

#### Inputs and Scope

- **FR-001**: System MUST accept a Stripe catalogue snapshot comprising product and price records, either from ingested CSV exports or an equivalent live catalogue retrieval, normalized to the same domain shape used elsewhere in BillDrift.
- **FR-002**: System MUST accept the canonical product mapping model defining offer ID, SKU ID, normalized product identity, Stripe product and price ID slots per term/frequency where known, supplier naming variants, and mapping confidence.
- **FR-003**: System MUST accept intended retail pricing reference records keyed by offer ID, SKU ID, term, and billing frequency, including effective intended RRP after pricing strategy resolution (catalogue RRP default with manual override precedence).
- **FR-004**: Catalogue reconciliation MUST iterate over canonical mapped products that have intended pricing for at least one term/frequency combination; mapped products without pricing reference MUST produce a pricing-reference gap outcome rather than silent pass.

#### Catalogue Presence Checks

- **FR-005**: For each in-scope canonical product, system MUST verify that a Stripe product exists, resolved by mapped Stripe product ID when present, or by offer ID and SKU ID metadata match when ID is absent.
- **FR-006**: When no Stripe product is found, system MUST emit a missing-product catalogue exception with commercial key, mapping reference, and normalized product identity.
- **FR-007**: For each in-scope canonical product, system MUST verify that an active Stripe price exists for every term/frequency combination required by intended pricing for that commercial key.
- **FR-008**: When a required price is absent, system MUST emit a missing-price catalogue exception identifying commercial key, term, billing frequency, intended RRP, and currency.

#### Price Amount Checks

- **FR-009**: For each matched Stripe price, system MUST compare the Stripe unit amount to the effective intended RRP from the pricing reference for the same commercial key, term, and billing frequency.
- **FR-010**: When amounts differ beyond configured currency tolerance, system MUST emit an incorrect-price catalogue exception recording expected RRP, actual Stripe amount, currency, and price identity.
- **FR-011**: System MUST NOT treat a price as correct when currency differs between intended pricing and Stripe price, even if numeric values appear similar.

#### Duplicate and Conflict Detection

- **FR-012**: System MUST detect when multiple Stripe products map to the same offer ID and SKU ID pair and emit a duplicate-product conflict exception listing all involved products.
- **FR-013**: System MUST detect when multiple active Stripe prices on the same product share the same billing interval and currency and emit a duplicate-price conflict exception listing all involved prices.
- **FR-014**: Duplicate and conflict exceptions MUST NOT generate proposals that automatically delete, merge, or archive Stripe catalogue entries.

#### Proposed Fixes and Exceptions Output

- **FR-015**: System MUST produce a structured catalogue exception for each detected issue, categorized as: missing product, missing price, incorrect price, duplicate/conflict, pricing-reference gap, mapping-ambiguous, or unmapped catalogue entry.
- **FR-016**: For missing-product exceptions, system MUST attach a proposed catalogue fix to create a product with normalized name and mapping metadata for operator approval.
- **FR-017**: For missing-price exceptions, system MUST attach a proposed catalogue fix to create a price with intended RRP, currency, and billing interval on the associated product for operator approval.
- **FR-018**: For incorrect-price exceptions, system MUST attach a proposed catalogue fix that creates a new correct price and flags retirement or non-use of the incorrect price, reflecting that existing Stripe price amounts cannot be edited in place.
- **FR-019**: For duplicate/conflict exceptions, system MUST attach a flag-for-manual-cleanup proposal with no bill-impacting auto-apply action.
- **FR-020**: All catalogue proposed fixes MUST be suitable for ingestion by the human-in-the-loop approval workflow: distinct from subscription corrections, requiring explicit approval, and including prior state, proposed state, and human-readable rationale.

#### Determinism and Reporting

- **FR-021**: Catalogue reconciliation MUST be deterministic: identical inputs MUST produce identical exceptions and proposed fixes.
- **FR-022**: System MUST provide a run summary with counts per exception category and a per-item detail list sufficient for operator review without opening source files.
- **FR-023**: System MUST record reconciliation run metadata (input snapshot references, timestamp, exception counts) for audit and repeatability consistent with other BillDrift reconciliation runs.

### Key Entities

- **Catalogue Reconciliation Run**: A bounded analysis over a Stripe catalogue snapshot, canonical mapping, and pricing reference; records inputs, exceptions, proposed fixes, and summary counts.
- **Canonical Product (Mapping Entry)**: The business definition of a billable product linking offer ID, SKU ID, normalized identity, Stripe catalogue ID slots, and mapping confidence.
- **Intended Pricing Record**: Effective retail reference for a commercial key (offer ID + SKU ID + term + frequency) including RRP, currency, price source (catalogue or manual), and product status.
- **Stripe Product Snapshot**: Normalized Stripe product with identifier, name, metadata, and active/archived state from ingestion.
- **Stripe Price Snapshot**: Normalized Stripe price with identifier, linked product, unit amount, currency, billing interval, and active/archived state.
- **Catalogue Exception**: A detected catalogue issue with type, commercial key, affected Stripe identities, expected vs actual values where applicable, and severity for operator triage.
- **Proposed Catalogue Fix**: An approval-ready corrective action (create product, create price, create replacement price, flag manual cleanup) with rationale and idempotency identity.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operators can complete a full catalogue reconciliation review for a typical reseller catalogue (up to 500 mapped products) and understand all exceptions in under 15 minutes using the summary and detail output alone.
- **SC-002**: 100% of missing Stripe products, missing prices, and RRP amount mismatches for in-scope mapped products are detected in a single reconciliation run when present in the input snapshot.
- **SC-003**: 100% of duplicate-product and duplicate-price conflicts present in the input snapshot are detected and flagged for manual cleanup with no false auto-merge proposals.
- **SC-004**: Identical catalogue, mapping, and pricing snapshots produce identical exception and proposed-fix outputs across repeated runs (zero non-determinism).
- **SC-005**: At least 90% of operators can correctly identify the required corrective action for each exception type (missing product, missing price, incorrect price, duplicate) without consulting external documentation, based on proposal descriptions in acceptance testing.
- **SC-006**: Catalogue proposed fixes integrate with the approval workflow such that unapproved fixes are never included in an approved changeset export (zero unapproved catalogue changes in export validation tests).

## Assumptions

- **Pricing reference source**: "SPECIFY 16" in the feature request refers to the retail pricing and pricing strategy ingestion capability (feature `010-retail-pricing-ingestion`), which supplies intended pricing records and effective RRP resolution rules.
- **Canonical mapping source**: "SPECIFY 1" refers to the billing domain model (feature `001-billing-domain-model`), specifically the ProductMapping entity and commercial key conventions (offer ID + SKU ID + term + frequency).
- **Stripe billing authority**: Stripe remains the source of truth for what customers are actually billed; catalogue reconciliation validates that Stripe's catalogue setup aligns with intended RRP strategy—it does not override live subscription prices directly.
- **MVP input path**: CSV export ingestion for Stripe products and prices is the primary input for v1; live API catalogue retrieval is a later enhancement using the same normalized snapshot shape.
- **Default currency**: Intended pricing and Stripe catalogue comparison assume GBP unless a different currency is explicitly present on both sides of the comparison.
- **Amount tolerance**: Unit amounts are compared exactly in minor currency units; immaterial one-minor-unit differences may be ignored only when documented per currency in reconciliation configuration (default: no tolerance, exact match).
- **Approval integration**: Proposed catalogue fixes follow the same human approval discipline as feature `007-reconciliation-approval-workflow`—no automatic Stripe catalogue writes.
- **Active prices**: "Required" prices refer to active catalogue prices; archived prices satisfy presence checks only when no active price exists for the interval, and are flagged for hygiene review.
- **Out of scope**: Customer subscription reconciliation, supplier cost comparison, and automated application of catalogue changes without approval are handled by other features or explicitly excluded from this feature's scope.
