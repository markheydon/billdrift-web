# Feature Specification: Billing Drift Domain Model

**Feature Branch**: `001-billing-domain-model`

**Created**: 2026-07-01

**Status**: Draft

**Input**: User description: "Define the core domain model for a billing drift reconciliation system used by a Microsoft 365 reseller. Stripe is the source of truth for customer billing. The system reconciles four data domains: supplier cost billing (Giacom PDFs), Microsoft subscription truth (Giacom Subscription Management report), intended retail pricing (Giacom price list / ResellerPricingVsRRP.csv), and customer billing state (Stripe subscriptions + product catalogue). Include canonical mapping, reconciliation run concepts, mismatch types, and proposed corrective actions. Domain-only scope — no UI, persistence, or API."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Represent Imported Billing Data Faithfully (Priority: P1)

As a reconciliation developer, I need each external data source (Giacom PDFs, Subscription Management report, price list CSV, Stripe exports/API) represented as distinct **raw import** structures so that ingestion never loses source fidelity and downstream normalization can be traced back to original values.

**Why this priority**: All reconciliation depends on accurate capture of supplier and Stripe inputs. Without raw/import separation, drift analysis cannot be audited or replayed.

**Independent Test**: Given representative fixture files from each source, the domain model can hold every field needed for normalization without mixing raw strings with computed values.

**Acceptance Scenarios**:

1. **Given** a Giacom pre/post billing PDF line, **When** it is captured in the raw import model, **Then** customer Mex ID, product name as written, quantity, charge type, billing period, line cost, and supplier reference identifiers are all preserved exactly as extracted.
2. **Given** a Subscription Management report row, **When** it is captured in the raw import model, **Then** customer name, Mex ID, tenant ID, offer ID, SKU ID, licence count, term, billing frequency, renewal date, status, and supplier subscription reference are preserved.
3. **Given** a ResellerPricingVsRRP.csv row, **When** it is captured in the raw import model, **Then** offer ID, SKU ID, term, frequency, wholesale cost, RRP, margin, margin percentage, and product status are preserved.
4. **Given** Stripe subscription and catalogue data, **When** it is captured in the raw import model, **Then** customer, subscription, subscription items, products, prices, quantities, intervals, amounts, and mapping metadata (Mex ID, offer ID, SKU ID, references) are preserved.

---

### User Story 2 - Normalize Cross-Domain Identifiers (Priority: P1)

As a reconciliation operator, I need supplier lines, Microsoft subscription truth, intended pricing, and Stripe billing state normalized into comparable domain entities keyed by shared identifiers (Mex ID, offer ID, SKU ID, term, frequency) so that the engine can match records across domains deterministically.

**Why this priority**: Reconciliation requires apples-to-apples comparison. Normalization is the bridge between heterogeneous sources.

**Independent Test**: Given raw imports from all four domains for one customer, normalized entities can be grouped by Mex ID and product keys without ambiguity.

**Acceptance Scenarios**:

1. **Given** raw supplier cost lines and raw Microsoft subscription lines for the same Mex ID, **When** both are normalized, **Then** they share a common customer identity value object and can be correlated by offer/SKU where present.
2. **Given** a price list entry and a Stripe price with matching offer ID, SKU ID, term, and frequency, **When** normalized, **Then** the intended retail price can be compared to the Stripe billed amount for the same commercial dimensions.
3. **Given** a manual price override not present in the official price list, **When** normalized, **Then** it participates in pricing comparison with an explicit source marker distinguishing it from catalogue-derived pricing.

---

### User Story 3 - Maintain Canonical Product Mapping (Priority: P1)

As a reconciliation operator, I need a canonical **ProductMapping** that links offer/SKU keys, normalized product names, Stripe product and price IDs per term/frequency, supplier naming variants, CSP classification, and mapping confidence so that the same Microsoft product can be recognized regardless of how Giacom or Stripe labels it.

**Why this priority**: Mapping gaps and ambiguities are a primary cause of false reconciliation results. A first-class mapping entity prevents ad-hoc string matching.

**Independent Test**: Given two supplier product name variants and one Stripe product, a single ProductMapping resolves both names to the same offer/SKU keys and Stripe catalogue entries.

**Acceptance Scenarios**:

1. **Given** a ProductMapping with offer ID and SKU ID, **When** a supplier line uses a known naming variant, **Then** the mapping resolves to the correct normalized product and Stripe product ID.
2. **Given** a ProductMapping with Stripe price IDs keyed by term and frequency, **When** comparing billing frequency, **Then** the expected Stripe price for that term/frequency is unambiguous.
3. **Given** a mapping with low confidence or multiple candidate matches, **When** reconciliation runs, **Then** a mapping-missing or mapping-ambiguous mismatch is produced rather than silently matching.

---

### User Story 4 - Execute and Record Reconciliation Runs (Priority: P2)

As a reconciliation operator, I need a **ReconciliationRun** that captures inputs (snapshots from all four domains), matching results, mismatches, and proposed corrective actions so that each analysis is reproducible and idempotent for the same input snapshot.

**Why this priority**: Billing accuracy requires traceability. Operators must explain why a discrepancy was flagged and what change was proposed.

**Independent Test**: Given a fixed set of normalized inputs, two runs with the same run identifier inputs produce identical mismatch and proposed-change outputs.

**Acceptance Scenarios**:

1. **Given** normalized data from all four domains for a billing period, **When** a ReconciliationRun executes, **Then** it records run metadata, input references, matched entity groups, and all detected mismatches.
2. **Given** matched entities across domains, **When** quantity differs between Microsoft subscription truth and Stripe, **Then** a quantity mismatch is recorded with both values and entity references.
3. **Given** a mismatch, **When** a corrective action is derived, **Then** a ProposedChange is attached with action type, target entities, and values needed for later idempotent application.

---

### User Story 5 - Classify Mismatches and Propose Actions (Priority: P2)

As a reconciliation operator, I need mismatches categorized by type (missing in Stripe, quantity mismatch, billing frequency mismatch, price mismatch, catalogue missing, mapping missing/ambiguous) with corresponding **ProposedChange** actions so I can review and approve the right corrective step.

**Why this priority**: Different mismatch types require different operator responses. Clear classification reduces reconciliation errors.

**Independent Test**: For each mismatch type, the domain model can represent at least one example mismatch and a valid ProposedChange without requiring UI or external APIs.

**Acceptance Scenarios**:

1. **Given** a Microsoft subscription line with no corresponding Stripe subscription item, **When** reconciliation completes, **Then** a "missing in Stripe" mismatch and a "create missing item" ProposedChange are produced.
2. **Given** Stripe quantity differs from subscription truth quantity, **When** reconciliation completes, **Then** an "update quantity" ProposedChange specifies the target quantity from subscription truth.
3. **Given** Stripe uses a price ID that does not match the expected term/frequency from ProductMapping, **When** reconciliation completes, **Then** a "billing frequency mismatch" or "switch price" ProposedChange is produced as appropriate.
4. **Given** expected RRP from pricing domain differs from Stripe billed amount, **When** reconciliation completes, **Then** a "price mismatch" records both amounts and commercial dimensions.
5. **Given** no Stripe product or price exists for a mapped offer/SKU combination, **When** reconciliation completes, **Then** a "catalogue missing" mismatch is recorded and an optional "create/update catalogue entries" ProposedChange may be attached.

---

### Edge Cases

- Supplier PDF contains pro-rated adjustment lines alongside recurring lines for the same product and period — charge type distinguishes them; recurring totals must not double-count adjustments.
- Subscription Management report includes suspended or cancelled subscriptions — status must be modeled so reconciliation can exclude or flag non-active lines according to business rules.
- Price list entry marked EndOfSale — pricing comparison must still use last known RRP but flag status for operator review.
- Manual price override conflicts with price list for the same offer/SKU/term/frequency — domain must represent precedence (manual override wins) and record override source.
- Stripe metadata missing Mex ID, offer ID, or SKU ID — mapping-missing mismatch rather than incorrect auto-match.
- Same product appears under multiple supplier naming variants in one billing period — ProductMapping supplier naming variants resolve to one canonical product.
- Duplicate supplier reference identifiers across lines — idempotency keys prevent duplicate normalization on re-import.
- Customer has multiple Stripe subscriptions — matching scoped by customer + subscription + item, not customer alone.
- Non-CSP products (non-Microsoft-365) present in supplier billing — ProductMapping classification (CSP vs non-CSP) allows scoped reconciliation to Microsoft 365 only.

## Requirements *(mandatory)*

### Functional Requirements

#### Raw Import Layer

- **FR-001**: Domain model MUST define separate raw import structures for each source: Giacom billing PDF lines, Giacom Subscription Management report rows, ResellerPricingVsRRP.csv rows (plus manual price entries), and Stripe customer/subscription/catalogue data.
- **FR-002**: Raw import structures MUST preserve source values without premature normalization (e.g., product names as written, raw date strings where parsing is deferred).
- **FR-003**: Each raw import record MUST carry a source-specific stable identifier or composite key sufficient for idempotent re-import (e.g., supplier reference ID, Stripe object ID, file row index with file checksum reference).

#### Supplier Cost Domain (Giacom PDFs)

- **FR-004**: Normalized supplier cost line MUST include: customer/sub-account Mex ID, product name, quantity, charge type (Recurring or ProRatedAdjustment), billing period start and end, line cost amount, and supplier reference identifiers.
- **FR-005**: Charge type MUST distinguish recurring charges from pro-rated adjustments so reconciliation can treat them differently.

#### Microsoft Subscription Truth Domain

- **FR-006**: Normalized Microsoft subscription line MUST include: customer identity (name), Mex ID, tenant ID, offer ID, SKU ID (primary product identifiers), licence count, term, billing frequency, renewal date, status, and supplier subscription/reference ID when present.
- **FR-007**: Subscription truth domain MUST scope to Microsoft 365 subscriptions as reported by Giacom Subscription Management.

#### Intended Retail Pricing Domain

- **FR-008**: Normalized price list entry MUST include: offer ID, SKU ID, term, billing frequency, wholesale (cost) price, RRP, margin amount, margin percentage, and product status (Active, EndOfSale, and other statuses as provided by source).
- **FR-009**: Domain model MUST support manual price entries for products not in the official price list, with explicit marking as manual override and the same commercial key dimensions (offer ID, SKU ID, term, frequency) where known.
- **FR-010**: When both price list and manual override exist for the same commercial key, manual override MUST take precedence for intended retail price resolution.

#### Stripe Billing State Domain (Source of Truth)

- **FR-011**: Normalized Stripe billing state MUST model: customer, subscription, subscription items, Stripe product, Stripe price, quantity, billing interval, unit amount, and metadata fields used for cross-domain mapping (Mex ID, offer ID, SKU ID, supplier references).
- **FR-012**: Stripe domain MUST be treated as authoritative for customer billing state during reconciliation output (proposed changes target Stripe alignment).

#### Shared Value Objects and Identity

- **FR-013**: Domain model MUST define shared value objects for: MexId, TenantId, OfferId, SkuId, BillingPeriod, Term, BillingFrequency, Money/CurrencyAmount, and CommercialKey (offer + SKU + term + frequency).
- **FR-014**: Normalized entities MUST be immutable after creation; corrections produce new instances rather than mutating shared state.
- **FR-015**: Domain model MUST separate raw import types from normalized domain types — normalization is a distinct conceptual operation, not embedded in import structures.

#### Canonical Product Mapping

- **FR-016**: ProductMapping MUST include: offer ID and SKU ID keys, normalized product name, Stripe product ID, Stripe price IDs indexed by term and frequency, collection of supplier naming variants, classification (CSP vs non-CSP), mapping confidence level, and mapping source (manual, inferred, imported).
- **FR-017**: ProductMapping MUST support zero-to-many supplier naming variants per canonical product.
- **FR-018**: Mapping confidence MUST allow reconciliation to flag ambiguous or low-confidence matches without applying them silently.

#### Reconciliation Domain

- **FR-019**: ReconciliationRun MUST capture: unique run identifier, execution timestamp, billing period scope, references to input snapshots from all four domains, collection of matched entity groups, collection of mismatches, and collection of proposed changes.
- **FR-020**: Matched entity group MUST link zero or one normalized entity from each relevant domain (supplier cost, subscription truth, intended pricing, Stripe billing) under a shared match key with match confidence.
- **FR-021**: Mismatch MUST be typed with at least these categories: MissingInStripe, QuantityMismatch, BillingFrequencyMismatch, PriceMismatch, CatalogueMissing, MappingMissing, MappingAmbiguous.
- **FR-022**: Each mismatch MUST reference the involved normalized entities, expected vs actual values where applicable, and human-readable context sufficient for operator review.
- **FR-023**: ProposedChange MUST support action types: UpdateQuantity, SwitchPrice, CreateMissingItem, CreateOrUpdateCatalogueEntry (optional/catalogue maintenance).
- **FR-024**: Each ProposedChange MUST include: action type, target entity references, proposed values, idempotency key derived from run ID + mismatch ID + action type, and dependency ordering hint when multiple changes apply to the same subscription.
- **FR-025**: Reconciliation for identical input snapshots MUST produce deterministic mismatch and proposed-change sets (same inputs → same outputs).

#### Scope Boundaries

- **FR-026**: Domain model MUST NOT include UI views, persistence mappings, database schemas, HTTP/API contracts, or Stripe API client types.
- **FR-027**: Domain model MUST use naming conventions suitable for C# record and value-object implementations (PascalCase entity names, explicit enum suffixes where applicable).

### Key Entities *(include if feature involves data)*

#### Raw Import Layer

- **RawGiacomBillingLine**: Single line from pre/post billing PDF — Mex ID, raw product name, quantity, raw charge type text, period text, cost text, supplier reference fields, source document reference.
- **RawSubscriptionManagementRow**: Row from Giacom Subscription Management report — customer name, Mex ID, tenant ID, offer ID, SKU ID, licences, term, frequency, renewal date, status, supplier subscription ID.
- **RawPriceListRow**: Row from ResellerPricingVsRRP.csv — offer ID, SKU ID, term, frequency, wholesale, RRP, margin, margin %, status.
- **RawManualPriceEntry**: Operator-entered price not in list — same commercial dimensions as price list row plus entry reason and effective date.
- **RawStripeCustomer**, **RawStripeSubscription**, **RawStripeSubscriptionItem**, **RawStripeProduct**, **RawStripePrice**: Stripe objects with IDs, amounts, intervals, quantities, and metadata dictionary.

#### Normalized Domain Entities

- **CustomerIdentity**: MexId (required), optional display name, optional tenant ID, optional Stripe customer ID.
- **SupplierCostLine**: CustomerIdentity, product name (as normalized), quantity, ChargeType enum, BillingPeriod, Money line cost, supplier reference IDs, link to source RawGiacomBillingLine ID.
- **MicrosoftSubscriptionLine**: CustomerIdentity, OfferId, SkuId, licence count, Term, BillingFrequency, renewal date, SubscriptionStatus enum, supplier subscription reference, link to source row ID.
- **IntendedPrice**: CommercialKey, wholesale Money, RRP Money, margin Money, margin percentage, PriceListStatus enum, PriceSource enum (Catalogue, ManualOverride), link to source row ID.
- **StripeBillingItem**: CustomerIdentity, Stripe subscription ID, subscription item ID, Stripe product ID, Stripe price ID, quantity, BillingFrequency, Money unit amount, mapping metadata (MexId, OfferId, SkuId, references), link to source Stripe object IDs.

#### Mapping

- **ProductMapping**: CommercialKey root (offer + SKU), normalized product name, Stripe product ID, dictionary of Term+BillingFrequency → Stripe price ID, supplier naming variants list, ProductClassification enum (Csp, NonCsp), MappingConfidence enum, MappingSource enum, unique mapping ID.
- **SupplierNameVariant**: Raw name string, optional ProductMapping ID reference.

#### Reconciliation

- **ReconciliationRun**: RunId (unique), executed at, BillingPeriod scope, ReconciliationInputs, list of EntityMatchGroup, list of Mismatch, list of ProposedChange.
- **ReconciliationInputs**: Snapshot references or embedded collections for supplier cost lines, Microsoft subscription lines, intended prices, Stripe billing items, and product mappings applicable to the run.
- **EntityMatchGroup**: MatchGroupId, optional SupplierCostLine, optional MicrosoftSubscriptionLine, optional IntendedPrice, optional StripeBillingItem, MatchConfidence, match key (CommercialKey + CustomerIdentity).
- **Mismatch**: MismatchId, MismatchType enum, involved entity references, expected value, actual value, description, severity.
- **ProposedChange**: ChangeId, idempotency key, MismatchId reference, ProposedActionType enum, target Stripe entity references, proposed field values, optional catalogue creation payload, execution order index.

#### Enumerations

- **ChargeType**: Recurring, ProRatedAdjustment
- **SubscriptionStatus**: Active, Suspended, Cancelled, Pending, Unknown
- **PriceListStatus**: Active, EndOfSale, Unknown
- **PriceSource**: Catalogue, ManualOverride
- **ProductClassification**: Csp, NonCsp
- **MappingConfidence**: High, Medium, Low, Unmapped
- **MappingSource**: Manual, Imported, Inferred
- **MismatchType**: MissingInStripe, QuantityMismatch, BillingFrequencyMismatch, PriceMismatch, CatalogueMissing, MappingMissing, MappingAmbiguous
- **ProposedActionType**: UpdateQuantity, SwitchPrice, CreateMissingItem, CreateOrUpdateCatalogueEntry

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Domain model covers 100% of fields enumerated in the feature description across all four data domains, canonical mapping, and reconciliation concepts — verifiable by checklist against source field list.
- **SC-002**: Every normalized entity type includes a stable identifier or composite key enabling idempotent re-processing of the same import data without duplicate reconciliation artifacts.
- **SC-003**: All seven mismatch types and four proposed action types are representable with at least one concrete example scenario documented in acceptance tests (no untyped "other" bucket required).
- **SC-004**: Raw import and normalized entity types are distinct for every external source — zero shared mutable types between layers.
- **SC-005**: A developer can implement reconciliation engine logic using only the domain types without referencing UI, persistence, or API concerns — verifiable by plan-phase architecture review.
- **SC-006**: Determinism requirement (FR-025) is testable: given two identical ReconciliationInputs instances, output mismatch sets are equivalent by MismatchType and entity reference.

## Assumptions

- Microsoft 365 scope follows Giacom Subscription Management report coverage; non-M365 supplier lines may exist but reconciliation matching prioritizes CSP-classified products.
- Money amounts use a single currency per reseller (GBP assumed for UK Giacom resellers); multi-currency support is out of scope for v1 domain model.
- Stripe is authoritative for customer billing state; subscription truth and supplier cost inform expected state, not override Stripe without explicit ProposedChange approval (per constitution).
- Manual price overrides are operator-maintained and relatively few; no versioning history required in domain model v1 beyond source marker and effective date.
- Idempotency keys use RunId + MismatchId + action type composite; exact algorithm deferred to implementation plan.
- Giacom Mex ID is the primary customer correlation key across supplier and Stripe metadata; tenant ID supplements Microsoft subscription truth matching.
- Offer ID + SKU ID + Term + BillingFrequency form the canonical commercial key for product and pricing alignment.
- Normalization rules (date parsing, name trimming, enum mapping from raw text) are defined in the implementation plan; domain model defines target types only.
- C# record immutability and PascalCase naming are implementation conventions agreed for this project; the spec describes conceptual entities independent of syntax.

## Dependencies

- BillDrift Constitution v1.0.0 — billing accuracy, determinism, and human approval principles constrain how ProposedChange will later be applied (approval workflow out of scope here but informs action modeling).
- Future ingestion features will produce Raw* types; this domain model is their normalization target.
- ProductMapping may initially be seeded manually or from CSV before automated inference exists.
