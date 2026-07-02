# Feature Specification: Billing Reconciliation Engine

**Feature Branch**: `004-reconciliation-engine`

**Created**: 2026-07-02

**Status**: Draft

**Input**: User description: "Design the reconciliation engine that aligns supplier cost + subscription truth + retail pricing to Stripe billing. Stripe is the source of truth for customer billing."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reconcile Microsoft Subscription Truth Against Stripe Billing (Priority: P1)

As a billing operator, I need each active Microsoft subscription truth line matched to the corresponding Stripe subscription item and validated for product mapping, licence quantity, billing frequency, and retail price so that I can detect where customer billing in Stripe diverges from what should be billed.

**Why this priority**: Subscription truth represents what licences are active and should be billed. Comparing it to Stripe is the core revenue-protection workflow for a Microsoft 365 reseller.

**Independent Test**: Given normalized subscription truth lines and Stripe billing items for one customer with a known offer/SKU, the engine produces a reconciliation result showing matched entities, match status, and any quantity, frequency, or price issues without requiring supplier PDF or catalogue-only inputs.

**Acceptance Scenarios**:

1. **Given** an active subscription truth line with offer ID, SKU ID, licence count, term, and billing frequency, and a Stripe subscription item with matching product metadata and correct quantity, price, and interval, **When** reconciliation runs, **Then** the result shows a matched group with no issues and no corrective actions required.
2. **Given** an active subscription truth line with no corresponding Stripe subscription item for the same customer and commercial product, **When** reconciliation runs, **Then** the result flags a "missing in Stripe" issue and proposes creating the missing billing item.
3. **Given** a matched subscription truth line and Stripe item where licence count differs, **When** reconciliation runs, **Then** the result flags a quantity mismatch showing expected (truth) and actual (Stripe) values and proposes updating Stripe quantity to match truth.
4. **Given** a matched pair where Stripe uses a price with a different billing interval than the subscription truth term/frequency, **When** reconciliation runs, **Then** the result flags a billing frequency mismatch and proposes switching to the correct price where one exists.
5. **Given** a matched pair where the Stripe unit amount does not match the intended retail price for that offer/SKU/term/frequency, **When** reconciliation runs, **Then** the result flags a price mismatch with both amounts shown and proposes switching price when an alternate correct price exists in the catalogue.

---

### User Story 2 - Build and Maintain Canonical Product Mapping (Priority: P1)

As a billing operator, I need the engine to resolve products across supplier cost lines, subscription truth, intended pricing, and Stripe using offer ID and SKU ID as primary keys, Stripe product metadata where present, Mex ID for customer association, and name matching only as a last resort so that reconciliation is accurate and auditable.

**Why this priority**: Incorrect or silent product matching causes false positives and missed drift. Mapping strategy directly affects trust in every reconciliation outcome.

**Independent Test**: Given inputs where the same Microsoft product appears under different supplier names and in Stripe with offer/SKU metadata, the engine resolves all records to one canonical product identity with documented match confidence; ambiguous or missing mappings produce explicit issues rather than guessed matches.

**Acceptance Scenarios**:

1. **Given** subscription truth and Stripe items sharing the same offer ID and SKU ID for a customer, **When** reconciliation runs, **Then** they are matched with high confidence without relying on product name similarity.
2. **Given** a Stripe product with offer ID and SKU ID in metadata, **When** matching to intended retail pricing and subscription truth, **When** reconciliation runs, **Then** the metadata keys are used as the primary match path.
3. **Given** supplier or subscription records for the same customer identified by Mex ID, **When** reconciliation groups results, **Then** all related lines are scoped to that customer identity.
4. **Given** a supplier product name with no offer/SKU and no existing mapping, **When** reconciliation runs, **Then** the engine attempts fuzzy name matching only as a fallback; if multiple candidates exist, a mapping-ambiguous issue is raised; if none exist, a mapping-missing issue is raised.
5. **Given** a new successful match path (e.g., operator confirms a name variant), **When** mapping is recorded, **Then** future runs prefer the canonical mapping over fuzzy fallback for that variant.

---

### User Story 3 - Reconcile Supplier Cost Lines to Known Products and Customers (Priority: P2)

As a billing operator, I need supplier-billed cost lines from PDF ingestion mapped to known products and customers, with anomalies and non-CSP lines clearly highlighted, so that I can verify supplier charges align with subscription and billing state and spot lines requiring manual attention.

**Why this priority**: Supplier cost confirms what the reseller is charged. Unmapped or anomalous cost lines indicate margin risk or data quality problems even when Stripe billing appears correct.

**Independent Test**: Given normalized supplier cost lines for a billing period, the engine attaches each mappable line to a reconciliation result group, flags unmapped lines, and distinguishes CSP from non-CSP products requiring manual mapping.

**Acceptance Scenarios**:

1. **Given** a supplier cost line with Mex ID and resolvable product mapping, **When** reconciliation runs, **Then** the line appears in the matching customer/product reconciliation result alongside truth, pricing, and Stripe data where available.
2. **Given** a supplier cost line that cannot be mapped to a known product or customer, **When** reconciliation runs, **Then** the result flags a mapping issue with enough source detail for the operator to resolve manually.
3. **Given** a supplier cost line classified as non-CSP, **When** reconciliation runs, **Then** the result flags it as requiring manual mapping review rather than auto-matching via CSP product rules.
4. **Given** pro-rated adjustment charge types on supplier lines, **When** reconciliation aggregates quantities, **Then** adjustments do not inflate recurring licence counts used for quantity comparison.
5. **Given** supplier cost lines with no corresponding subscription truth or Stripe item, **When** reconciliation runs, **Then** the result still surfaces the cost line with mapping status and issues so the operator can investigate orphaned charges.

---

### User Story 4 - Reconcile Stripe Catalogue Against Intended Retail Pricing (Priority: P2)

As a billing operator, I need the engine to verify that required Stripe products and prices exist for mapped offer/SKU combinations and that Stripe prices match intended retail pricing so that catalogue gaps and stale prices are caught before they affect customer billing.

**Why this priority**: Subscription items reference catalogue prices. Missing or incorrect catalogue entries cause recurring price and frequency mismatches across customers.

**Independent Test**: Given intended retail pricing and Stripe product/price catalogue data, the engine reports catalogue gaps and price mismatches per offer/SKU/term/frequency without requiring live subscription data.

**Acceptance Scenarios**:

1. **Given** a mapped offer/SKU with intended retail pricing but no corresponding Stripe product or price, **When** reconciliation runs, **Then** the result flags a catalogue-missing issue and may propose creating or updating catalogue entries.
2. **Given** a Stripe price for a mapped offer/SKU/term/frequency whose unit amount differs from intended retail pricing beyond configured tolerance, **When** reconciliation runs, **Then** the result flags a price mismatch at the catalogue level.
3. **Given** manual retail price overrides for a customer or product, **When** reconciliation compares prices, **Then** the override takes precedence over the standard price list for that comparison.
4. **Given** a complete catalogue where all mapped products have correct prices for all required term/frequency combinations, **When** catalogue reconciliation runs, **Then** no catalogue-missing or catalogue price issues are reported for those products.

---

### User Story 5 - Review Explainable Reconciliation Results and Proposed Actions (Priority: P1)

As a billing operator, I need each reconciliation result for a customer and product to show all contributing data sources, match status, categorized issues, and proposed corrective actions in plain language so that I can understand and approve changes without re-running manual spreadsheet analysis.

**Why this priority**: Correctness without explainability does not meet operator needs. The engine must make every flagged issue and proposed action traceable to source data.

**Independent Test**: Given a fixture with at least one issue of each mismatch category, the operator can read each reconciliation result and identify which source values differ, why the issue was raised, and what change is proposed—without inspecting raw input files.

**Acceptance Scenarios**:

1. **Given** a completed reconciliation run, **When** the operator views a customer/product result, **Then** it shows the current Stripe billing item (if any), subscription truth item (if any), supplier cost line(s) (if any), pricing reference (intended retail price), overall match status, and a list of issues.
2. **Given** a detected mismatch, **When** the operator reviews the issue, **Then** the description states what was expected, what was found, and which matching rule or comparison failed.
3. **Given** a mismatch with a supported corrective action, **When** reconciliation completes, **Then** a proposed action is attached with action type, target, and proposed values suitable for later operator approval.
4. **Given** mapping-missing or mapping-ambiguous issues, **When** reconciliation completes, **Then** no bill-impacting corrective action is proposed until mapping is resolved manually.
5. **Given** the same input snapshot run twice, **When** both runs complete, **Then** results, issue categories, and proposed actions are identical (deterministic output).

---

### Edge Cases

- What happens when a customer has multiple Stripe subscription items for the same offer/SKU (duplicate billing)? The engine flags ambiguous or duplicate matches rather than silently merging them.
- What happens when subscription truth shows active licences but Stripe subscription is canceled? The engine reports missing or inactive billing state according to configured scope (active-only by default).
- What happens when intended retail pricing has no entry for an offer/SKU present in subscription truth? The engine flags a pricing reference gap without inventing a price.
- What happens when Stripe product metadata is partial (offer ID present, SKU ID missing)? The engine does not treat the match as high-confidence; it falls back through mapping rules and may flag mapping ambiguity.
- What happens when supplier cost lines span a billing period boundary? Lines are evaluated within the reconciliation run scope period; out-of-scope lines are excluded from the run summary.
- What happens when manual price overrides conflict with the standard price list for the same key? Manual overrides win; the pricing reference source is visible in the result.
- What happens when fuzzy name matching yields a single low-confidence candidate? The match is recorded with low confidence and surfaced for operator review; no bill-impacting action is auto-proposed on low-confidence matches alone.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST accept normalized inputs from four domains: supplier cost lines, Microsoft subscription truth lines, intended retail pricing (standard price list plus manual overrides), and Stripe billing state (subscription items plus product/price catalogue).
- **FR-002**: The system MUST treat Stripe as the authoritative source for current customer billing state; subscription truth and intended pricing define expected billing, not automatic overrides.
- **FR-003**: The system MUST associate all reconciliation results to customers primarily by Mex ID, with customer display name and tenant ID as supplementary identity attributes.
- **FR-004**: The system MUST use offer ID and SKU ID as the primary product identifiers when matching across subscription truth, intended pricing, Stripe catalogue metadata, and product mappings.
- **FR-005**: The system MUST use Stripe product metadata fields for offer ID and SKU ID when present before attempting any other product match strategy.
- **FR-006**: The system MUST use fuzzy product name matching only when offer/SKU and existing canonical mappings cannot resolve a product, and MUST record match confidence for any name-based match.
- **FR-007**: The system MUST maintain and consult a canonical product mapping that links offer/SKU keys, supplier name variants, Stripe product and price references per term/frequency, and product classification (CSP vs non-CSP).
- **FR-008**: For each active subscription truth line within run scope, the system MUST attempt to find a corresponding Stripe subscription item for the same customer and product, validate product mapping, validate licence quantity, validate billing frequency, and validate that the Stripe price matches intended retail pricing for the correct term and frequency.
- **FR-009**: For each supplier cost line within run scope, the system MUST attempt to map the line to a known customer and product; unmapped lines MUST produce a mapping issue; non-CSP lines MUST be flagged for manual mapping review.
- **FR-010**: The system MUST verify that required Stripe products and prices exist for mapped offer/SKU combinations needed by active subscription truth and MUST compare Stripe catalogue prices to intended retail pricing.
- **FR-011**: The system MUST produce a reconciliation result per customer and product grouping containing: current Stripe billing item (if matched), subscription truth item (if matched), supplier cost line(s) (if matched), pricing reference (intended retail price), overall match status, categorized issues, and proposed corrective actions (where applicable).
- **FR-012**: The system MUST categorize issues using at minimum: missing in Stripe, quantity mismatch, billing frequency mismatch, price mismatch, catalogue missing (product or price), mapping missing, mapping ambiguous, and non-CSP line requiring manual mapping.
- **FR-013**: The system MUST attach proposed corrective actions for supported issue types, including at minimum: create missing Stripe item, update quantity, switch price, and create or update catalogue entries; mapping issues MUST NOT auto-propose bill-impacting actions.
- **FR-014**: Each proposed corrective action MUST include enough target and value detail for an operator to review and later apply idempotently without re-deriving context from raw inputs.
- **FR-015**: The system MUST produce deterministic results: identical input snapshots and run options MUST yield equivalent issue sets, match groupings, and proposed actions.
- **FR-016**: The system MUST exclude pro-rated supplier adjustment lines from recurring licence quantity totals used in quantity comparison.
- **FR-017**: The system MUST resolve intended retail pricing such that manual overrides take precedence over standard price list entries for the same commercial key.
- **FR-018**: The system MUST support configurable run options including: scope billing period, include or exclude inactive subscriptions (default: active only), include or exclude non-CSP products in automated matching (default: flag only), price comparison tolerance, and whether to propose catalogue maintenance actions.
- **FR-019**: The system MUST order reconciliation output consistently by customer Mex ID, product commercial key, and issue category so operators and downstream review tools receive predictable results.
- **FR-020**: The system MUST NOT apply any corrective action to Stripe or supplier systems during reconciliation; output is advisory until operator approval in a separate workflow.
- **FR-021**: Every issue and proposed action MUST include an operator-facing explanation describing expected value, actual value, and the rule that detected the discrepancy.

### Key Entities *(include if feature involves data)*

- **Reconciliation Run**: A single execution over a billing period scope with immutable input snapshots, run options, timestamp, and collections of reconciliation results, issues, and proposed actions.
- **Reconciliation Inputs**: Normalized supplier cost lines, subscription truth lines, intended retail prices, Stripe billing items, Stripe catalogue products/prices, and canonical product mappings for one analysis snapshot.
- **Reconciliation Result** (per customer/product): Groups matched Stripe billing item, subscription truth line, supplier cost line(s), and pricing reference with overall match status, match confidence, issues, and linked proposed actions.
- **Product Mapping**: Canonical link between offer/SKU identity, supplier naming variants, Stripe product and price references by term/frequency, CSP classification, and mapping confidence/source.
- **Customer Identity**: Mex ID with optional display name, tenant ID, and Stripe customer reference for scoping matches.
- **Commercial Key**: Offer ID, SKU ID, term, and billing frequency combination used to align pricing and catalogue entries.
- **Issue (Mismatch)**: Typed discrepancy with severity, involved entity references, expected and actual values, and human-readable description.
- **Proposed Action**: Typed corrective suggestion linked to an issue, with target references, proposed values, and idempotency identity for later approved application.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operators can identify all subscription truth lines that lack a corresponding Stripe billing item in a single reconciliation run view without manual cross-referencing of source files.
- **SC-002**: For a fixed input snapshot, repeated reconciliation runs produce identical issue counts and proposed action sets (100% determinism on regression fixtures).
- **SC-003**: At least 95% of CSP subscription truth lines with valid offer ID, SKU ID, and Stripe metadata present in fixtures are matched with high confidence without fuzzy name matching.
- **SC-004**: Every flagged issue in reconciliation output includes expected value, actual value, and a plain-language reason understandable by a billing operator without engineering knowledge.
- **SC-005**: Operators can complete review of a 50-customer reconciliation run (mixed clean and problematic records) and identify all quantity, price, and frequency discrepancies in under 30 minutes, compared to multi-hour manual spreadsheet reconciliation.
- **SC-006**: 100% of non-CSP supplier lines in scope are flagged for manual mapping review rather than silently auto-matched to CSP products.
- **SC-007**: Catalogue reconciliation identifies 100% of mapped offer/SKU/term/frequency combinations that lack a corresponding Stripe price in fixture-based acceptance tests.
- **SC-008**: Zero bill-impacting proposed actions are generated for mapping-missing, mapping-ambiguous, or non-CSP manual-review issues without operator-supplied mapping.

## Assumptions

- Normalized input collections are produced by upstream ingestion features (Giacom PDF, Subscription Management report, ResellerPricingVsRRP.csv, manual price overrides, Stripe CSV/API ingestion) before reconciliation runs.
- The canonical product mapping may be pre-seeded and extended over time; this feature consumes mappings and emits mapping issues where gaps exist but does not require a separate mapping UI in scope.
- Stripe remains the billing source of truth; proposed actions describe how to align Stripe with expected state, not how to override Stripe silently.
- Default reconciliation scope focuses on active subscriptions; inactive or canceled subscriptions are excluded unless the operator opts in.
- Price comparison uses a configurable absolute tolerance (default: zero) for retail price mismatches.
- Human approval and dry-run application of proposed actions are handled by a separate workflow outside this feature's scope, consistent with project governance.
- Single-tenant or operator-triggered runs are in scope; unattended scheduled reconciliation across many tenants may be added later but is not required for initial delivery.
- Currency is assumed consistent within a reconciliation run (e.g., GBP for UK reseller fixtures); cross-currency comparison is out of scope unless inputs declare otherwise in a future amendment.

## Dependencies

- Billing domain model defining normalized entities, mismatch types, and proposed action types (feature 001).
- Giacom PDF ingestion producing normalized supplier cost lines (feature 002).
- Stripe billing ingestion producing normalized subscription items and catalogue data (feature 003).
- Intended retail pricing ingestion from ResellerPricingVsRRP.csv and manual overrides (may be part of an existing or adjacent ingestion feature; reconciliation consumes normalized intended price entities).
