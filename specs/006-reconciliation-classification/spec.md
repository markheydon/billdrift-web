# Feature Specification: Reconciliation Item Classification

**Feature Branch**: `006-reconciliation-classification`

**Created**: 2026-07-02

**Status**: Draft

**Input**: User description: "Design a classification system for reconciliation items. Stripe is the source of truth for customer billing, but items vary in origin. Classify each item as Microsoft CSP, Non-CSP supplier item, Internal, or Custom/service. Define rules based on customer, Offer/SKU presence, product category, and manual overrides. Persist classification with override notes. Internal items suppress missing-billing alerts; Non-CSP items require manual pricing rules and mapping. Focus on flexibility and avoiding false positives."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Automatically Classify Reconciliation Items by Origin (Priority: P1)

As a billing operator, I need every reconciliation item automatically assigned one of four origin classifications—Microsoft CSP, Non-CSP supplier, Internal, or Custom/service—so that downstream reconciliation and exception surfacing apply the correct rules without me manually tagging every line.

**Why this priority**: Classification is the gate for all reconciliation behaviour. Without consistent automatic classification, internal and non-CSP lines produce false missing-billing alerts and erode operator trust.

**Independent Test**: Given a fixture set covering all four classification types across supplier cost lines, subscription truth lines, and Stripe billing items, the classification service assigns exactly one primary classification per item with a recorded rule basis and confidence indicator.

**Acceptance Scenarios**:

1. **Given** a subscription truth line with valid offer ID and SKU ID that also appears in the intended retail price list for the same commercial key, **When** classification runs, **Then** the item is classified as Microsoft CSP with rule basis indicating offer/SKU and catalogue presence.
2. **Given** a supplier cost line for a customer product with no matching subscription truth line in the same billing scope, **When** classification runs, **Then** the item is classified as Non-CSP supplier with rule basis indicating absence from subscription management truth.
3. **Given** a line whose customer Mex ID matches a configured internal customer identifier, **When** classification runs, **Then** the item is classified as Internal regardless of offer/SKU presence.
4. **Given** a Stripe billing item with no corresponding supplier cost or subscription truth line and product category marked as custom/service, **When** classification runs, **Then** the item is classified as Custom/service with rule basis indicating independence from supplier billing.
5. **Given** the same input snapshot classified twice, **When** results are compared, **Then** classification, rule basis, and confidence are identical (deterministic output).

---

### User Story 2 - Apply Manual Classification Overrides with Audit Notes (Priority: P1)

As a billing operator, I need to override an automatic classification when business context differs from rule inference, and record why I changed it, so that future reconciliation runs respect my decision and other operators can understand the rationale.

**Why this priority**: Automatic rules cannot capture every edge case. Manual overrides with notes are essential for flexibility without sacrificing traceability.

**Independent Test**: Given an automatically classified item, an operator can set a different classification, add override notes, persist the override, and a subsequent classification run returns the overridden value with override metadata visible.

**Acceptance Scenarios**:

1. **Given** an item automatically classified as Non-CSP supplier, **When** an operator overrides it to Microsoft CSP and saves notes explaining a delayed subscription management report entry, **Then** the persisted classification is Microsoft CSP and override notes are stored with operator identity and timestamp.
2. **Given** a persisted manual override on an item, **When** automatic rules would produce a different classification on the next run, **Then** the manual override takes precedence and the result indicates classification source as manual override.
3. **Given** an operator clears a manual override, **When** classification runs again, **Then** automatic rules are re-evaluated and the classification source reverts to automatic rule basis.
4. **Given** any classification change (automatic or manual), **When** the classification history is inspected, **Then** prior classification, new classification, source, notes (if any), and timestamp are available for audit.

---

### User Story 3 - Suppress False Missing-Billing Alerts for Internal Items (Priority: P1)

As a billing operator, I need internal items—licences or costs not billed to external customers—to be excluded from missing-billing exception generation so that my exception queue reflects only revenue-impacting gaps.

**Why this priority**: Internal usage is a primary source of false positives in truth-vs-Stripe reconciliation. Suppression must be classification-driven and explicit.

**Independent Test**: Given a reconciliation run where internal-classified subscription truth lines have no Stripe billing counterpart, no missing-billing exception is surfaced for those lines while external customer gaps still appear.

**Acceptance Scenarios**:

1. **Given** a subscription truth line classified as Internal with no matching Stripe item, **When** reconciliation exceptions are surfaced, **Then** no missing-billing exception is generated for that line.
2. **Given** a subscription truth line classified as Internal that does have a matching Stripe item, **When** reconciliation runs, **Then** quantity, price, and frequency checks may still run but missing-billing suppression does not hide other legitimate mismatch types unless separately configured.
3. **Given** an item reclassified from Microsoft CSP to Internal via manual override, **When** the next reconciliation run completes, **Then** previously surfaced missing-billing exceptions for that item are no longer generated.
4. **Given** a line classified as Internal solely by customer Mex ID rule, **When** an operator reviews the classification, **Then** the rule basis clearly states which internal customer identifier matched.

---

### User Story 4 - Route Non-CSP Items to Manual Mapping and Pricing Workflows (Priority: P1)

As a billing operator, I need Non-CSP supplier items clearly flagged as requiring manual product mapping and pricing rules so that reconciliation does not silently auto-match them to Microsoft CSP catalogue entries or propose bill-impacting corrections without operator setup.

**Why this priority**: Non-CSP products (hardware, third-party software, pass-through services) follow different commercial rules. Misclassification as CSP causes incorrect auto-matching and dangerous proposed actions.

**Independent Test**: Given Non-CSP-classified supplier lines in a reconciliation run, exceptions indicate manual mapping and pricing review is required, no high-confidence CSP auto-match is applied, and no bill-impacting corrective action is proposed without operator-supplied mapping.

**Acceptance Scenarios**:

1. **Given** a supplier cost line classified as Non-CSP supplier, **When** reconciliation exceptions are surfaced, **Then** the exception category indicates non-CSP manual review and states that manual mapping and pricing rules are required.
2. **Given** a Non-CSP-classified line with ambiguous product name similarity to a CSP offer, **When** reconciliation runs, **Then** the line is not auto-matched to the CSP product via fuzzy name matching alone.
3. **Given** a Non-CSP-classified line with a persisted manual product mapping added by an operator, **When** reconciliation runs, **Then** mapping may proceed using the operator mapping but pricing still follows non-CSP manual pricing rules unless separately overridden.
4. **Given** a line that appears in supplier PDFs and subscription truth but product category is not Microsoft 365, **When** classification runs with ambiguous signals, **Then** the system classifies conservatively (preferring Non-CSP or requiring review) rather than assuming Microsoft CSP.

---

### User Story 5 - Configure Classification Rules for Operators (Priority: P2)

As a billing administrator, I need to configure internal customer identifiers, product category mappings, and rule precedence so that automatic classification adapts to my organisation without code changes.

**Why this priority**: Internal Mex IDs and product category taxonomies vary by reseller. Configurable rules reduce misclassification without per-line manual work.

**Independent Test**: Given updated configuration for internal customer identifiers and product category rules, classification results change predictably on the next run without modifying individual item overrides.

**Acceptance Scenarios**:

1. **Given** a new internal customer Mex ID added to configuration, **When** classification runs for lines belonging to that customer, **Then** those lines are classified as Internal on the next run.
2. **Given** a product category rule mapping a supplier product family to Custom/service, **When** a matching Stripe-only line is classified, **Then** it receives Custom/service classification when no supplier billing evidence exists.
3. **Given** conflicting automatic rule signals (e.g., offer/SKU present but customer is internal), **When** classification runs, **Then** rule precedence is applied consistently: manual override > internal customer > custom/service independence > non-CSP absence from truth > Microsoft CSP offer/SKU signals.
4. **Given** configuration changes, **When** classification runs, **Then** only items without manual overrides are re-evaluated; overridden items retain operator decisions.

---

### Edge Cases

- A line appears in both supplier PDF and subscription truth but offer/SKU is missing or partial — classify as low-confidence Microsoft CSP candidate or Non-CSP based on product category and subscription truth presence; surface for review rather than high-confidence CSP auto-match.
- A customer Mex ID is removed from the internal list after items were classified Internal — items without manual override reclassify on next run; items with override notes retain Internal until override cleared.
- A Non-CSP supplier line later appears in subscription management truth for a future period — classification re-evaluates per run scope; operator may add override if timing lag is expected.
- Custom/service items exist only in Stripe with no supplier or truth counterpart — no missing-billing-in-Stripe alert from truth side; orphaned Stripe billing review may still apply per reconciliation scope rules.
- Pro-rated supplier adjustment lines share a product name with recurring CSP lines — classification applies at the reconciliation item grain (line or normalized item identity), not solely by product name string.
- Multiple classification rule signals tie — deterministic precedence order applies and confidence is downgraded with explicit multi-signal evidence recorded.
- Operator overrides classification to a type that contradicts visible source data — override is honoured with audit trail; reconciliation applies impact rules for the chosen classification.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST assign every reconciliation item exactly one primary classification from: Microsoft CSP, Non-CSP supplier, Internal, or Custom/service.
- **FR-002**: The system MUST record for each classification: classification value, classification source (automatic rule or manual override), rule basis describing which signals fired, confidence level (high, medium, low), and timestamp of last classification.
- **FR-003**: The system MUST classify items as Microsoft CSP when offer ID and SKU ID are present, the item appears in subscription management truth or intended retail price list for the matching commercial key, and product category indicates Microsoft 365 (Office 365) unless a higher-precedence rule applies.
- **FR-004**: The system MUST classify items as Non-CSP supplier when supplier cost evidence exists for the item and no corresponding subscription management truth line exists for the same customer and product within the reconciliation scope, unless manual override or internal customer rule applies.
- **FR-005**: The system MUST classify items as Internal when the customer Mex ID matches a configured internal customer identifier list, or when a manual override sets Internal classification.
- **FR-006**: The system MUST classify items as Custom/service when the item represents billing independent of supplier cost and subscription truth (e.g., professional services, one-off fees) as indicated by product category rules or absence of supplier and truth evidence with Stripe billing present, unless a higher-precedence rule applies.
- **FR-007**: The system MUST apply rule precedence in order: manual override, internal customer match, custom/service independence signals, non-CSP supplier absence from truth, Microsoft CSP offer/SKU and catalogue signals; when signals conflict at the same precedence tier, confidence MUST be downgraded and all contributing signals recorded.
- **FR-008**: The system MUST support operator manual override of classification with required or optional notes (notes required when overriding to a less conservative classification that suppresses alerts).
- **FR-009**: The system MUST persist current classification and override notes durably so they survive reconciliation run boundaries and application restarts.
- **FR-010**: The system MUST retain classification change history sufficient for audit: prior value, new value, source, notes, operator identity, and timestamp.
- **FR-011**: The system MUST expose classification on each reconciliation item consumed by the reconciliation engine and exception surfacing layers.
- **FR-012**: The system MUST suppress missing-billing exceptions for items classified as Internal when subscription truth or expected billing lines have no Stripe counterpart.
- **FR-013**: The system MUST route items classified as Non-CSP supplier to manual mapping and manual pricing review workflows; automated CSP matching and bill-impacting proposed actions MUST NOT be applied without operator-supplied mapping.
- **FR-014**: The system MUST NOT suppress quantity, price, frequency, or catalogue exceptions solely because an item is Internal when matching Stripe and truth entities exist; suppression applies specifically to missing-billing-from-truth scenarios unless configured otherwise.
- **FR-015**: The system MUST treat items classified as Custom/service as outside standard CSP catalogue reconciliation; missing supplier truth MUST NOT alone trigger missing-billing alerts for Custom/service items.
- **FR-016**: The system MUST support configurable internal customer identifiers (Mex ID list) and product category rules (at minimum distinguishing Microsoft 365 from Other) without per-item code changes.
- **FR-017**: The system MUST produce deterministic classification output for identical input snapshots, configuration, and override state.
- **FR-018**: The system MUST default to conservative classification when confidence is low—preferring Non-CSP manual review or explicit review-required state over high-confidence Microsoft CSP— to minimise false positives.
- **FR-019**: The system MUST allow operators to view classification, rule basis, confidence, and override notes alongside reconciliation results and exceptions without opening raw source files.
- **FR-020**: The system MUST re-evaluate automatic classification on each reconciliation run for items without active manual overrides when inputs or configuration change.

### Key Entities *(include if feature involves data)*

- **Reconciliation Item**: A normalised entity participating in reconciliation (supplier cost line, subscription truth line, Stripe billing item, or unified item identity used by match groups) that receives a classification.
- **Item Classification**: The assigned type (Microsoft CSP, Non-CSP supplier, Internal, Custom/service) with source, rule basis, confidence, and effective timestamp.
- **Classification Override**: Operator-initiated classification change with notes, operator identity, timestamp, and optional expiry or clear action.
- **Classification Rule Configuration**: Organisation settings including internal customer Mex IDs, product category mappings (Microsoft 365 vs Other), and rule precedence definitions.
- **Classification History Entry**: Audit record of classification transitions for an item.
- **Product Category**: Business grouping of a product (e.g., Microsoft 365, Other) used as a classification signal distinct from offer/SKU identity.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operators reviewing a mixed fixture reconciliation run can identify the classification of any item and the reason it was assigned within one interaction, without consulting raw ingestion files.
- **SC-002**: 100% of items classified as Internal in acceptance fixtures produce zero missing-billing exceptions when no Stripe counterpart exists.
- **SC-003**: 100% of items classified as Non-CSP supplier in acceptance fixtures surface manual mapping/pricing review and generate zero bill-impacting proposed actions without operator-supplied mapping.
- **SC-004**: Repeated classification of the same input snapshot yields identical results in 100% of regression test cases (determinism).
- **SC-005**: False positive missing-billing exceptions attributable to internal or custom/service items decrease by at least 90% compared to unclassified reconciliation on representative operator fixtures.
- **SC-006**: Operators can apply and persist a manual classification override with notes in under 1 minute per item.
- **SC-007**: At least 95% of clearly labelled Microsoft CSP fixture lines (offer/SKU present, in truth and price list, external customer) receive Microsoft CSP classification with high confidence without manual override.
- **SC-008**: Ambiguous fixture lines (partial offer/SKU, name-only match candidates) are classified with low or medium confidence and never trigger high-confidence CSP auto-match or bill-impacting proposals without operator review.

## Assumptions

- Stripe remains the source of truth for customer billing state; classification informs how reconciliation interprets gaps, not whether Stripe is overridden automatically.
- Reconciliation items are identified using existing normalised entity keys (Mex ID, commercial key, supplier references, Stripe IDs) from the billing domain model (feature 001).
- Subscription management truth and intended retail price list ingestion are available as inputs; classification consumes normalised data, not raw files.
- Internal customer identifiers are maintained by the reseller administrator; a small configurable list (typically one to five Mex IDs) is sufficient for v1.
- Product category Microsoft 365 vs Other is derived from mapping configuration, supplier product metadata, or subscription truth product family—not solely from product name substring matching.
- Manual overrides persist until explicitly cleared; there is no automatic expiry in v1 unless added in configuration later.
- UI for classification review and override may be delivered in a subsequent feature; this feature defines classification behaviour, persistence, and reconciliation integration. Operator-facing surfaces will use established project UI patterns when built.
- Classification integrates with the existing reconciliation engine (feature 004) and exception surfacing (feature 005) rather than replacing them.
- Single-tenant reseller scope; multi-tenant classification configuration is out of scope for initial delivery.
- Durably persisted storage is required for classifications and overrides; specific storage technology is deferred to implementation planning consistent with project v1 constraints.

## Dependencies

- Billing domain model with normalised entities, product mappings, and commercial keys (feature 001).
- Giacom PDF ingestion producing supplier cost lines (feature 002).
- Stripe billing ingestion (feature 003).
- Reconciliation engine consuming classified items and emitting mismatches (feature 004).
- Exception surfacing applying classification-aware suppression and non-CSP routing (feature 005).
- Configurable product mappings that may include or extend classification signals (feature 001 / ongoing mapping maintenance).

## Out of Scope

- Building operator UI screens for classification management (behaviour and data model only in this feature).
- Automated application of corrective actions to Stripe based on classification.
- Full product catalogue administration or supplier contract management.
- Machine-learning-based classification; v1 uses explicit rules and manual overrides only.
- Introducing relational database storage; persistence approach is decided in planning phase.
