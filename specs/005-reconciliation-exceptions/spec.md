# Feature Specification: Reconciliation Exception Surfacing

**Feature Branch**: `005-reconciliation-exceptions`

**Created**: 2026-07-02

**Status**: Draft

**Input**: User description: "Define the model and logic for surfacing reconciliation exceptions to the user. Stripe is the source of truth for customer billing. The system should identify and group exceptions across Truth vs Stripe, Supplier cost vs mapped products, and Pricing vs Stripe catalogue. Focus on clarity, prioritisation, and avoiding false positives. Output: UI-ready view model (not UI itself)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Review Prioritised Exceptions for a Customer (Priority: P1)

As a billing operator, I need reconciliation exceptions grouped and ordered by customer, severity, and whether immediate action is required so that I can work through the highest-impact billing problems first without scanning an unstructured list of raw mismatches.

**Why this priority**: Operators reconcile customer-by-customer. Without prioritised grouping, high-severity revenue gaps are buried among low-priority catalogue warnings.

**Independent Test**: Given a reconciliation run with mixed exception types across three customers, the surfaced view model returns customer groups ordered by highest severity present, with each group containing exceptions sorted by severity then action urgency, and a run-level summary showing counts by category and severity.

**Acceptance Scenarios**:

1. **Given** a reconciliation run with one customer having a missing billing item (Error) and another having only catalogue warnings (Warning), **When** exceptions are surfaced, **Then** the customer with the Error appears before the customer with only Warnings in the default view order.
2. **Given** exceptions for a single customer spanning missing billing, quantity mismatch, and mapping ambiguity, **When** surfaced, **Then** all exceptions appear under one customer group with summary counts by severity and category.
3. **Given** an exception marked as requiring action now, **When** the operator views the customer group summary, **Then** the count of action-required exceptions is visible without opening individual exception detail.
4. **Given** the same reconciliation run surfaced twice, **When** both view models are compared, **Then** grouping, ordering, counts, and exception identities are identical.

---

### User Story 2 - Understand Each Exception with Clear Explanation and Evidence (Priority: P1)

As a billing operator, I need every surfaced exception to show its type, severity, affected customer and product, a plain-language explanation, and supporting evidence from source data so that I can trust the finding and resolve it without re-opening raw ingestion files.

**Why this priority**: Clarity and evidence are the primary defences against false positives. Operators will ignore the tool if exceptions are vague or unsubstantiated.

**Independent Test**: Given a fixture with at least one example of each exception category, each surfaced exception includes all required model fields and evidence sufficient to verify the finding against source values.

**Acceptance Scenarios**:

1. **Given** a quantity mismatch exception, **When** surfaced, **Then** the explanation states expected versus actual licence counts, the evidence includes labelled values from subscription truth and Stripe, and the product and customer are identified.
2. **Given** a mapping-ambiguous exception with multiple candidate products, **When** surfaced, **Then** the explanation lists each candidate with distinguishing attributes and no single candidate is presented as confirmed.
3. **Given** a catalogue price mismatch, **When** surfaced, **Then** evidence shows the intended retail price, the Stripe price amount, billing frequency, and currency.
4. **Given** a non-CSP supplier line exception, **When** surfaced, **Then** the explanation states that manual mapping or pricing rules are required and no bill-impacting corrective action is implied as ready to apply.

---

### User Story 3 - Work Through Exception Categories Across Reconciliation Domains (Priority: P1)

As a billing operator, I need exceptions categorised consistently across truth-vs-Stripe billing alignment, supplier-cost-to-product mapping, and pricing-vs-catalogue checks so that I can filter and triage by the kind of problem I am solving.

**Why this priority**: Different exception categories require different resolution workflows (fix Stripe billing vs fix mapping vs fix catalogue). Consistent categorisation enables focused triage.

**Independent Test**: Given reconciliation output covering all six exception category families, the view model assigns each exception exactly one primary category and supports filtering the run summary by category.

**Acceptance Scenarios**:

1. **Given** subscription truth with no matching Stripe item, **When** surfaced, **Then** the exception category is Missing Billing Item with reconciliation domain Truth vs Stripe.
2. **Given** an active Stripe subscription item with no matching subscription truth line, **When** surfaced, **Then** the exception category is Orphaned Billing Item with reconciliation domain Truth vs Stripe.
3. **Given** a supplier cost line that cannot be mapped to a known product, **When** surfaced, **Then** the exception category is Mapping Issue with reconciliation domain Supplier Cost vs Mapped Products.
4. **Given** intended retail pricing with no corresponding Stripe price for the required term and frequency, **When** surfaced, **Then** the exception category is Stripe Price Missing under Pricing & Catalogue.
5. **Given** a non-CSP supplier line, **When** surfaced, **Then** the exception category is Non-CSP Requires Manual Review regardless of other match attempts.

---

### User Story 4 - Avoid Noise from Duplicate or Low-Confidence False Positives (Priority: P2)

As a billing operator, I need the surfacing layer to suppress duplicate exceptions, downgrade or hide low-confidence findings, and collapse related catalogue gaps so that the exception list reflects distinct actionable problems rather than repeated alarms for the same root cause.

**Why this priority**: False positives erode trust faster than missed issues. Surfacing logic must be conservative where confidence is low and deduplicate where one root cause produces multiple engine mismatches.

**Independent Test**: Given fixtures designed to produce duplicate or cascading mismatches, the surfaced exception count is lower than the raw mismatch count where deduplication rules apply, and no bill-impacting exception is surfaced for low-confidence product resolution.

**Acceptance Scenarios**:

1. **Given** a mapping-missing issue on a match group, **When** surfaced, **Then** dependent quantity, price, and frequency mismatches on the same group are suppressed and only the root mapping exception is shown.
2. **Given** a product resolution confidence of Low or None, **When** surfaced, **Then** no exception implying a ready bill-impacting corrective action is presented; mapping review exceptions may still appear.
3. **Given** multiple catalogue-missing signals for the same offer/SKU and term/frequency combination, **When** surfaced, **Then** a single consolidated catalogue exception is shown with combined evidence.
4. **Given** a Stripe item with no truth line but the subscription is canceled and inactive items are excluded from scope, **When** surfaced, **Then** no orphaned billing exception is raised for that item.

---

### User Story 5 - Consume a UI-Ready View Model Without Presentation Logic (Priority: P2)

As a product developer, I need a stable, presentation-agnostic view model that encodes grouping, sorting, labels, and summary statistics so that any future UI can render exception lists, filters, and drill-down detail without re-implementing reconciliation logic.

**Why this priority**: Separating surfacing logic from UI keeps reconciliation rules testable and allows multiple consumers (web UI, exports, notifications) to share one model.

**Independent Test**: Given a completed reconciliation run, the surfacing output is a single view model object graph with documented fields for run summary, customer groups, exceptions, and evidence items; no rendering markup or UI component assumptions are required.

**Acceptance Scenarios**:

1. **Given** a reconciliation run, **When** surfacing completes, **Then** the output includes a run-level summary with total exception count, counts by severity, counts by category, and count requiring action now.
2. **Given** customer groups in the view model, **When** inspected, **Then** each group exposes customer identity, display name, severity summary, and an ordered list of exceptions.
3. **Given** an exception in the view model, **When** inspected, **Then** it exposes type, severity, category, customer, product, explanation, evidence collection, requires-action-now flag, and optional link to a proposed corrective action identifier when one exists.
4. **Given** a consumer that only needs a flat sorted list, **When** it reads the view model, **Then** it can derive a deterministic flat ordering from the grouped structure without re-running reconciliation.

---

### Edge Cases

- What happens when a customer has exceptions but no display name? The view model uses Mex ID as the primary customer label with display name optional.
- What happens when an exception spans multiple supplier cost lines? Evidence lists all contributing lines with line-level identifiers; the exception remains a single surfaced item.
- What happens when Stripe metadata Mex ID disagrees with subscription truth Mex ID for the same Stripe customer? A Mex ID Mismatch mapping exception is surfaced with both values in evidence; billing alignment exceptions for that item are suppressed until identity is resolved.
- What happens when the reconciliation run has zero exceptions? The view model returns an empty exception list with a summary showing zero counts and a clear "no exceptions" state indicator for consumers.
- What happens when a proposed corrective action exists but mapping is unresolved? The exception is surfaced without a requires-action-now flag and without exposing the proposed action as ready to apply.
- What happens when the same product has both a catalogue price mismatch and a subscription-level price mismatch? Subscription-level billing exceptions are surfaced; catalogue-level exceptions are suppressed when the subscription exception already captures the same price delta for the same commercial key.
- What happens when orphaned Stripe items exist but product metadata is insufficient to classify the product? An orphaned billing exception is still surfaced with available Stripe product name and identifiers in evidence, flagged for manual review.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST accept a completed reconciliation run (match groups, mismatches, proposed changes, and input snapshot metadata) as the sole input to exception surfacing.
- **FR-002**: The system MUST produce a UI-ready exception view model containing a run summary, customer groups, and individual exceptions with no dependency on a specific UI framework or rendering technology.
- **FR-003**: Each surfaced exception MUST include: exception type, severity, customer identity, product identity (when applicable), plain-language explanation, and a structured evidence collection.
- **FR-004**: The system MUST assign each exception exactly one primary category from: Missing Billing Item, Orphaned Billing Item, Quantity or Licence Mismatch, Billing Frequency Mismatch, Product Mismatch, Stripe Product Missing, Stripe Price Missing, Stripe Price Does Not Match Intended Retail Price, Offer or SKU Ambiguous Mapping, Mex ID Mismatch, or Non-CSP Requires Manual Review.
- **FR-005**: The system MUST record the reconciliation domain for each exception as one of: Truth vs Stripe, Supplier Cost vs Mapped Products, or Pricing vs Stripe Catalogue.
- **FR-006**: The system MUST map reconciliation engine mismatch outputs to surfaced exception types using a documented, deterministic mapping table; where the engine does not yet emit a mismatch for a required category (e.g., orphaned Stripe billing items, Mex ID mismatch, product mismatch), the surfacing layer MUST derive the exception from match groups and input snapshot comparison rules defined in this feature.
- **FR-007**: The system MUST group exceptions by customer (primary key: Mex ID), with each customer group containing an ordered list of that customer's exceptions.
- **FR-008**: The system MUST order customer groups by the highest severity exception present in the group (Error before Warning before Info), then by count of exceptions requiring action now (descending), then by Mex ID (ascending) for stable tie-breaking.
- **FR-009**: The system MUST order exceptions within a customer group by severity (Error, Warning, Info), then by requires-action-now (true before false), then by category priority (Missing Billing Item and Orphaned Billing Item before mapping issues before quantity/frequency/price mismatches before catalogue issues before non-CSP manual review), then by product identifier for stable tie-breaking.
- **FR-010**: The system MUST compute a boolean requires-action-now flag per exception using: Error severity AND (a linked proposed corrective action exists that is eligible for operator review OR the category is Missing Billing Item, Orphaned Billing Item, Quantity or Licence Mismatch, Billing Frequency Mismatch, or Product Mismatch) AND no unresolved mapping or identity issue blocks the action on the same match group.
- **FR-011**: The system MUST NOT set requires-action-now to true for mapping-missing, mapping-ambiguous, Mex ID mismatch, non-CSP manual review, or catalogue-only exceptions unless the operator must fix mapping or catalogue before any billing correction can proceed—in those cases requires-action-now reflects blocking setup work, not bill-impacting apply.
- **FR-012**: The system MUST suppress duplicate or dependent exceptions when a root-cause exception on the same match group makes them redundant: mapping failures suppress quantity, frequency, price, and product mismatch exceptions on that group; Mex ID mismatch suppresses truth-vs-Stripe alignment exceptions for affected items until resolved.
- **FR-013**: The system MUST NOT surface bill-impacting exceptions (those that would expose an eligible proposed corrective action) when product match confidence is Low or None; mapping review exceptions MAY still be surfaced.
- **FR-014**: The system MUST consolidate multiple catalogue-missing or catalogue price signals for the same commercial key (offer ID, SKU ID, term, frequency) into a single surfaced exception with merged evidence.
- **FR-015**: The system MUST surface orphaned billing item exceptions when an in-scope Stripe subscription item has no corresponding in-scope subscription truth line for the same customer and commercial product, excluding canceled or inactive items when the run scope excludes them.
- **FR-016**: The system MUST surface Mex ID mismatch exceptions when the same Stripe billing item or customer record is associated with different Mex IDs across truth, supplier, and Stripe metadata sources.
- **FR-017**: The system MUST surface product mismatch exceptions when truth and Stripe items are matched to different commercial keys for the same customer despite being joined in a match group with medium or high confidence on the join path but conflicting product identities in evidence.
- **FR-018**: The system MUST subdivide catalogue issues into Stripe Product Missing (no Stripe product for mapped offer/SKU), Stripe Price Missing (product exists, required price/term/frequency absent), and Stripe Price Does Not Match Intended Retail Price (price exists but unit amount differs beyond configured tolerance).
- **FR-019**: The system MUST include in evidence labelled source snapshots sufficient for operator verification: source name (e.g., subscription truth, Stripe subscription item, supplier cost line, intended retail price, Stripe catalogue), relevant field names, and human-readable values; sensitive credentials MUST NOT appear in evidence.
- **FR-020**: The system MUST expose an optional reference to a proposed corrective action identifier on exceptions where the reconciliation run attached an eligible proposed change; the view model MUST NOT embed execution logic or approval state.
- **FR-021**: The run summary MUST include total exception count, per-severity counts, per-category counts, per-domain counts, customer count with at least one exception, and count of exceptions requiring action now.
- **FR-022**: The system MUST produce deterministic output: identical reconciliation run input produces identical view model structure, ordering, counts, and exception identities.
- **FR-023**: The system MUST treat Stripe as the authoritative source for current customer billing state in explanations (e.g., "Stripe is missing this subscription" not "truth should be deleted").
- **FR-024**: The system MUST use consistent operator-facing terminology aligned with project standards (exception, corrective action, customer, product, severity) across all explanations and summary labels.

### Key Entities *(include if feature involves data)*

- **ReconciliationExceptionViewModel**: Top-level output for a run. Contains run summary, ordered customer exception groups, reconciliation run identifier, and scope period. Presentation-agnostic; suitable for UI, export, or API serialization.

- **ExceptionRunSummary**: Aggregate statistics for a run: total exceptions, counts by severity, category, and domain, customers affected, exceptions requiring action now, and generation timestamp.

- **CustomerExceptionGroup**: Exceptions scoped to one customer. Contains customer identity (Mex ID required, display name and Stripe customer ID optional), per-severity counts, action-required count, and ordered exception list.

- **SurfacedException**: Single operator-facing exception. Contains stable exception identifier, type, severity, primary category, reconciliation domain, customer, product (commercial key and display label when known), explanation (plain language), evidence collection, requires-action-now flag, optional proposed action reference, and suppression metadata (whether this exception suppressed others, for audit).

- **ExceptionEvidence**: One labelled datum supporting an exception. Contains source label, field name, display value, and optional source entity reference for drill-down. Multiple evidence items form an auditable bundle per exception.

- **ExceptionCategory**: Closed set classifying the operator workflow: Missing Billing Item, Orphaned Billing Item, Quantity or Licence Mismatch, Billing Frequency Mismatch, Product Mismatch, Stripe Product Missing, Stripe Price Missing, Stripe Price Does Not Match Intended Retail Price, Offer or SKU Ambiguous Mapping, Mex ID Mismatch, Non-CSP Requires Manual Review.

- **ReconciliationDomain**: Which comparison produced the exception: Truth vs Stripe, Supplier Cost vs Mapped Products, Pricing vs Stripe Catalogue.

- **ExceptionSeverity**: Info, Warning, Error — aligned with reconciliation mismatch severity but may be adjusted by surfacing rules (e.g., downgrading duplicate-suppressed cascades is not applicable since those are hidden, not downgraded).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operators can identify the top three customers requiring immediate attention from the run summary and default customer group ordering in under 1 minute for a 50-customer run with mixed severities.
- **SC-002**: For a fixed reconciliation run input, repeated surfacing produces 100% identical exception counts, ordering, and exception identifiers on regression fixtures.
- **SC-003**: 100% of surfaced exceptions in acceptance fixtures include explanation text and at least two evidence items where two or more sources contributed to the finding.
- **SC-004**: In fixtures with deliberate duplicate engine mismatches for the same root cause, surfaced exception count is at least 30% lower than raw mismatch count while preserving the root-cause exception.
- **SC-005**: Zero bill-impacting exceptions with requires-action-now set to true appear in fixtures where product match confidence is Low or None.
- **SC-006**: Operators reviewing a categorized fixture set can correctly assign each exception to one of the six category families in a moderated usability check with at least 90% agreement without reading raw source files.
- **SC-007**: Orphaned billing item and missing billing item exceptions are both surfaced in fixtures where Stripe-only and truth-only lines exist, enabling bidirectional drift detection.
- **SC-008**: Non-CSP supplier lines are surfaced only under Non-CSP Requires Manual Review with no eligible proposed corrective action reference in 100% of non-CSP fixture cases.

## Assumptions

- Reconciliation engine output (feature 004) and domain model (feature 001) remain the authoritative source for mismatch detection; this feature adds a surfacing and presentation layer without re-implementing detection rules except for gap-filling categories not yet emitted by the engine (orphaned Stripe items, Mex ID mismatch, explicit product mismatch).
- Stripe remains the billing source of truth; exceptions describe alignment gaps relative to subscription truth, supplier mapping, and intended retail pricing—not instructions to treat non-Stripe sources as authoritative for billing state.
- Human approval and application of proposed corrective actions remain outside this feature's scope; the view model only references proposed action identifiers when present.
- Default run scope excludes inactive or canceled Stripe subscriptions and subscription truth lines unless the reconciliation request opts in; surfacing respects the same scope boundaries.
- Price tolerance for retail price comparison is inherited from the reconciliation run options; surfacing does not introduce a separate tolerance.
- Currency is consistent within a run; evidence displays amounts with currency labels from source data.
- A single reconciliation run is surfaced at a time; cross-run exception history or trend analysis is out of scope for this feature.
- UI rendering, filtering controls, and export formatting are consumers of the view model, not part of this feature.

## Dependencies

- Billing domain model defining reconciliation run, mismatches, match groups, and proposed changes (feature 001).
- Reconciliation engine producing deterministic mismatch and match group output (feature 004).
- Normalized ingestion outputs feeding reconciliation (features 002, 003, and intended pricing ingestion).
