# Feature Specification: Reconciliation Change Approval Workflow

**Feature Branch**: `007-reconciliation-approval-workflow`

**Created**: 2026-07-02

**Status**: Draft

**Input**: User description: "Design a human-in-the-loop approval workflow for applying reconciliation changes. Stripe is the source of truth for customer billing. No automatic modifications without approval."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Review and Decide on Proposed Subscription Corrections (Priority: P1)

As a billing operator, I need to review proposed subscription corrective actions from reconciliation results—create missing billing items, update quantities, switch prices, and flag items for manual investigation—so I can approve or reject each change with full context before anything is applied to Stripe.

**Why this priority**: Subscription billing corrections are the highest-value, highest-risk changes. Operators must gate every bill-impacting proposal through explicit human review before any downstream application.

**Independent Test**: Given a reconciliation run with mixed subscription proposals (missing item, quantity mismatch, price switch, manual investigation flag) for one customer, the approval workflow presents each proposal with prior vs proposed values, match status, and decision controls; approved items form an exportable changeset suitable for manual application first and automated application later.

**Acceptance Scenarios**:

1. **Given** a reconciliation result with a missing Stripe subscription item for an active subscription truth line, **When** the operator opens the approval queue for that customer, **Then** the proposal shows action type "create missing subscription item", customer and product identity, expected quantity and price from truth, and current Stripe state (none or absent).
2. **Given** a quantity mismatch between subscription truth and a matched Stripe item, **When** the operator reviews the proposal, **Then** the detail shows expected quantity, actual quantity, delta, and proposed new quantity with explicit approval controls.
3. **Given** a billing frequency or retail price mismatch with an alternate correct price in catalogue, **When** a price-switch proposal is presented, **Then** the operator sees prior price interval and amount versus proposed price with match group context.
4. **Given** a reconciliation issue that cannot be safely auto-corrected (ambiguous mapping, conflicting evidence, or policy block), **When** the engine proposes "flag for manual investigation", **Then** the approval item is presented as non-actionable with investigation reason and no bill-impacting apply option.

---

### User Story 2 - Approve, Reject, and Track Decision State (Priority: P1)

As a billing operator, I need to move each proposed change through a clear lifecycle from pending review to approved or rejected, with my identity and timestamp recorded, so that no change is applied without an explicit decision and every decision is auditable.

**Why this priority**: The approval state machine is the core safety control. Without enforced pending → approved/rejected transitions, operators cannot trust that Stripe will never be modified without consent.

**Independent Test**: Given a set of pending proposals, an operator can approve one, reject another with reason, and leave a third pending; the system records state, actor, and timestamp for each decision; approved items are distinguishable from rejected and pending in all views and exports.

**Acceptance Scenarios**:

1. **Given** multiple pending proposals for one customer, **When** the operator approves one subscription correction, **Then** that item transitions to approved, records approver identity and timestamp, and remains in approved state until superseded by a newer reconciliation run.
2. **Given** a pending catalogue fix the operator cannot safely approve, **When** they reject it with a mandatory reason, **Then** the item transitions to rejected, the reason is persisted, and the item is excluded from any approved changeset export.
3. **Given** a previously approved item, **When** a new reconciliation run produces a superseding proposal for the same customer-product group, **Then** the prior approval is marked historical and the new proposal appears as pending for re-review without applying anything automatically.
4. **Given** an attempt to apply or export a rejected or pending item, **When** downstream processing runs, **Then** the operation is blocked and the operator sees which decision state prevented the action.

---

### User Story 3 - Review Optional Catalogue Fixes with Conflict Safeguards (Priority: P2)

As a billing operator, I need catalogue proposals—creating missing products or prices, or flagging duplicates and conflicts—presented separately from subscription corrections but under the same approval discipline, so that pricing correctness is achievable without conflating catalogue setup with live subscription changes.

**Why this priority**: Catalogue fixes unlock correct price-switch proposals but are lower urgency than direct subscription billing gaps. They still require human control because duplicate or conflicting catalogue entries affect many customers.

**Independent Test**: Given reconciliation output with both subscription and catalogue proposals, the operator sees catalogue items grouped and labelled distinctly, can approve a "create missing price" proposal independently of subscription items, and duplicate/conflict flags are never auto-approved for application.

**Acceptance Scenarios**:

1. **Given** intended retail pricing with no matching Stripe price for the required interval, **When** the approval queue is opened, **Then** a catalogue proposal to create the missing monthly or annual price is shown with amount, currency, interval, and linked product identity.
2. **Given** a missing Stripe product required for mapping, **When** a create-product proposal is presented, **Then** the operator can approve or reject it without approving unrelated subscription items for the same customer.
3. **Given** duplicate or conflicting Stripe prices detected for the same commercial key, **When** the proposal is surfaced, **Then** it is presented as "flag for manual cleanup" with conflicting entries identified and no automatic merge or delete is proposed for approval.
4. **Given** a mix of subscription and catalogue pending items, **When** the operator approves only catalogue items, **Then** the exported approved changeset contains only catalogue actions and excludes pending subscription items.

---

### User Story 4 - Export Approved Changeset for Downstream Application (Priority: P1)

As a billing operator, I need an approved changeset export that lists only human-approved changes in a structured, reviewable form suitable for manual execution first (operator checklist or external runbook) with a defined path to automated application later, so that approved decisions translate into controlled action without re-running reconciliation logic by hand.

**Why this priority**: Approval without a reliable export recreates manual spreadsheet work. The changeset is the handoff artifact that makes approved decisions actionable and measurable.

**Independent Test**: Given three approved items across subscription and catalogue types, **When** the operator requests export, **Then** the output contains only approved items in stable order with customer grouping, action types, prior vs new values, and approval metadata; pending and rejected items are absent from the export.

**Acceptance Scenarios**:

1. **Given** two approved subscription corrections and one rejected catalogue proposal for one customer, **When** the operator exports the approved changeset, **Then** the export includes exactly the two approved subscription items with all fields needed for manual apply: customer, product, action type, prior state, proposed state, approver, and approval timestamp.
2. **Given** no approved items for a customer group, **When** export is requested, **Then** the operator is informed that nothing is approved for export and no empty changeset is silently produced.
3. **Given** an approved "create missing subscription item" and an approved "update quantity" for the same customer, **When** both are in one export, **Then** actions are ordered deterministically (catalogue before subscription where both are required, or per documented dependency order) so manual application is safe.
4. **Given** a downstream apply step (manual or future automated), **When** it consumes an export, **Then** each entry includes an idempotency identity so repeat application does not double-apply the same correction.

---

### User Story 5 - Inspect Audit History and Decision Context (Priority: P2)

As a billing operator or reviewer, I need to inspect who approved or rejected each proposal, when, and what changed between prior and proposed values, so that I can answer audit questions and train new team members without accessing raw reconciliation internals.

**Why this priority**: Transparency supports trust and compliance. Audit is required for financial tooling but is secondary to blocking unapproved apply.

**Independent Test**: Given a customer with decided proposals, **When** audit history is viewed, **Then** each entry shows decision, actor, timestamp, action type, and prior vs new value summary for every approved or rejected item.

**Acceptance Scenarios**:

1. **Given** an approved price-switch proposal, **When** audit history is viewed, **Then** entries show prior price amount and interval vs proposed price amount and interval with approver and timestamp.
2. **Given** a rejected proposal, **When** audit is viewed, **Then** rejection reason and rejector identity are visible alongside the proposal that was declined.
3. **Given** a reconciliation re-run for the same customer, **When** new proposals appear, **Then** audit history retains prior run decisions without mutating them.
4. **Given** an operator without permission to approve bill-impacting changes, **When** they view the queue, **Then** they can still read proposals and audit history but cannot approve or export.

---

### Edge Cases

- What happens when reconciliation produces a proposal while an earlier pending proposal for the same customer-product-action type still exists? Newer run supersedes or marks prior pending as stale; operator must not approve stale pending items without explicit acknowledgment.
- What happens when the operator approves a subset of proposals for one customer? Export contains only approved items; rejected and pending do not block export of already-approved siblings unless business rules require dependency ordering.
- What happens when a catalogue create-product proposal is approved but a dependent create-price proposal is still pending? Export respects dependency order; downstream manual apply documentation warns that price creation must follow product creation.
- What happens when mapping is unresolved but reconciliation attached a subscription proposal? Proposal is blocked from approval or marked non-eligible until mapping confidence meets policy; operator sees investigation flag instead of approve controls where required.
- What happens when an operator attempts to approve all pending items in bulk for a customer? Bulk approve is allowed only with explicit confirmation showing count and summary of bill-impacting actions; individual review remains available.
- What happens when Stripe is the source of truth but reconciliation proposes a change that would reduce customer billing below truth? Proposal is still reviewable but flagged with revenue-impact indicator in approval queue.
- What happens when duplicate/conflict catalogue flags have no safe automated resolution? Only manual cleanup flag is available; no approve-to-apply path exists for destructive catalogue merges.
- What happens when an approved changeset is exported twice? Second export is identical in content for the same approval snapshot; re-export after new decisions produces a new changeset version without mutating prior export record.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST ingest proposed corrective actions from reconciliation output without applying any change to Stripe or supplier systems.
- **FR-002**: Each ingested proposal MUST enter `Pending` state and MUST remain in `Pending` until an operator explicitly approves or rejects it; no automatic transition to `Approved`.
- **FR-003**: The system MUST support operator decisions that transition proposals to `Approved` or `Rejected` only through explicit human action.
- **FR-004**: Decisions MUST record approver or rejector identity, decision timestamp, and rejection reason when rejecting (rejection reason required for all rejections).
- **FR-005**: The approval queue MUST present subscription proposals including: create missing subscription item, update quantity, switch subscription price, and flag for manual investigation, grouped by customer and product.
- **FR-006**: The system MUST present catalogue proposals separately, including: create missing product, create missing price (monthly/annual), and flag duplicates or conflicts for manual cleanup, when reconciliation attaches them.
- **FR-007**: Proposals flagged for manual investigation MUST NOT be approvable for bill-impacting application; they MUST be distinguishable from actionable subscription and catalogue proposals.
- **FR-008**: Duplicate or conflict catalogue findings MUST NOT expose destructive auto-merge or auto-delete as approvable actions.
- **FR-009**: Each presented proposal MUST show prior values (current Stripe/subscription truth state) and proposed values side by side for subscription and catalogue items.
- **FR-010**: The system MUST NOT apply approved changes to Stripe automatically as part of this feature; application is downstream via exported changeset (manual execution first).
- **FR-011**: The system MUST produce an approved changeset containing only `Approved` items with stable ordering, idempotency identity per action, customer and product context, action type, prior and proposed values, and approval metadata.
- **FR-012**: Pending and rejected items MUST be excluded from the approved changeset export.
- **FR-013**: When a new reconciliation run supersedes earlier pending proposals for the same customer-product scope, the system MUST mark prior pending items stale or superseded and MUST NOT allow silent approval of stale proposals without operator acknowledgment.
- **FR-014**: The system MUST persist an immutable audit trail per decision including: decision state, actor, timestamp, action type, affected customer and product, prior values, proposed values, and rejection reason when rejected.
- **FR-015**: Audit records MUST NOT be altered or deleted when reconciliation re-runs; historical decisions remain queryable for compliance review.
- **FR-016**: Mapping-unresolved or low-confidence match groups MUST NOT present approvable subscription corrections until policy criteria are met; investigation flags MUST be used where corrective action is unsafe.
- **FR-017**: Bulk approve MUST require explicit confirmation summarising count, customer scope, and action types before state transition.
- **FR-018**: Re-export of an approval snapshot MUST be deterministic and MUST NOT include rejected or pending items acquired after the snapshot unless newly approved.
- **FR-019**: The system MUST use consistent operator-facing terminology (`proposed corrective action`, `approval`, `pending`, `approved`, `rejected`, `changeset`, `manual investigation`) aligned with existing reconciliation and exception surfacing features.
- **FR-020**: Proposals with unresolved dependencies (e.g., catalogue product before price, mapping before subscription correction) MUST enforce dependency visibility in the approval queue and export ordering.
- **FR-021**: The system MUST expose proposal eligibility rules blocking approval when Stripe is source of truth but reconciliation indicates the change is unsafe or ambiguous.
- **FR-022**: Optional catalogue proposals MUST be suppressible from the default approval view when an operator is focused on subscription billing corrections only, without losing access to catalogue items.

### Key Entities *(include if feature involves data)*

- **Proposed Corrective Action**: A reconciliation-generated suggestion with action type, target customer and product, prior state, proposed state, eligibility flags, and link to source reconciliation issue; enters the approval workflow in `Pending` state.
- **Approval Decision**: Operator transition on a proposal (`Approved` or `Rejected`) with actor, timestamp, optional rejection reason, and supersession linkage when reconciliation re-runs.
- **Approval Queue Item**: Operator-facing view of a pending proposal with decision controls, grouping key (customer), risk indicators, and dependency hints.
- **Approved Changeset**: Export artifact containing only approved actions in deterministic order with idempotency keys and full prior/proposed value detail for manual or future automated application.
- **Audit Event**: Immutable record of a decision or export event with actor, timestamp, action summary, and references to affected proposals.
- **Investigation Flag**: Non-actionable marker for ambiguous or unsafe items requiring manual resolution; not eligible for approval export as a bill-impacting change.
- **Catalogue Proposal**: Sub-type of proposed action for product/price creation or duplicate/conflict surfacing; may be shown in a separate queue section.
- **Stale Proposal**: A pending item superseded by a newer reconciliation run; MUST NOT be approvable without explicit stale acknowledgment.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operators can locate and open all pending proposals for one customer within 30 seconds from the approval entry point in standard test scenarios.
- **SC-002**: 100% of bill-impacting proposals remain in `Pending` or `Rejected` state unless an operator explicitly approves them; zero automatic transitions to `Approved` in fixture runs.
- **SC-003**: 100% of exported approved changesets contain only `Approved` items with prior and proposed values sufficient for manual execution without re-running reconciliation (validated against representative fixture set).
- **SC-004**: 100% of rejected decisions persist rejection reason and rejector identity when rejection reason is required by policy.
- **SC-005**: 100% of investigation flags and duplicate/conflict catalogue findings are excluded from approved changeset export in fixture scenarios where no safe corrective action exists.
- **SC-006**: Operators can complete approve-or-reject review of a five-item customer group with mixed subscription and catalogue proposals in under 10 minutes in usability test scenarios.
- **SC-007**: Audit history for any decided proposal is retrievable with actor, timestamp, and prior vs proposed value summary in under 1 minute.
- **SC-008**: Reconciliation re-run producing superseding proposals does not mutate historical audit records (100% immutability in regression tests).
- **SC-009**: At least 90% of pilot operators report they can explain what each approved export entry would do to Stripe billing without reading raw reconciliation output (post-training survey).
- **SC-010**: Stale pending proposals cannot be exported in approved changeset without explicit stale acknowledgment (100% blocked in fixture tests).

## Assumptions

- Proposed corrective actions are produced by the reconciliation engine (feature 004) and surfaced via the exception view model (feature 005); classification rules (feature 006) may block eligibility for approval when mapping or non-CSP policy requires it.
- Stripe is the source of truth for customer billing; reconciliation proposals describe alignment toward truth and catalogue correctness, not silent overrides of Stripe without review.
- Operators are authenticated billing staff; role-based permission to approve bill-impacting changes is enforced by existing or parallel access control (only operators with approval permission can transition to `Approved` or export changesets).
- Application to Stripe is out of scope for initial delivery: approved changeset export supports manual operator execution first; automated API apply is a documented follow-on consuming the same artifact.
- One primary approval queue entry point per reconciliation run is sufficient for v1; deep integration with external ticketing is out of scope.
- Rejection reason is mandatory for catalogue duplicate/conflict flags and investigation outcomes when marked rejected; optional for subscription corrections unless operator policy requires always-on for all rejections is acceptable as default.
- Bulk approve may be offered at customer group level with explicit confirmation; per-item approval remains available.
- Dependency ordering between catalogue and subscription actions follows reconciliation engine's proposed dependency metadata when present; otherwise catalogue-before-subscription within a customer group is the default export ordering.
- Dry-run preview of Stripe impact before approval is handled by reconciliation/exception features or a companion preview step; this feature focuses on decision state and export, not live Stripe mutation.
- Pending proposal expiry defaults to marking stale on reconciliation re-run rather than time-based auto-reject unless operator policy specifies otherwise.

## Dependencies

- **Reconciliation Engine (004)**: Source of proposed corrective actions, prior/proposed values, and idempotency identities.
- **Reconciliation Exception Surfacing (005)**: Operator navigation from exceptions to approval queue; shared customer grouping and terminology.
- **Reconciliation Classification (006)**: Eligibility gating for mapping confidence, non-CSP manual review, and internal item suppression is consistent with exception and reconciliation features.
- **Billing Domain Model (001)**: Shared entities for customer, product, subscription truth, and Stripe billing references in proposals.

## Out of Scope

- Direct Stripe API write/application (manual or automated) in this feature; only approved changeset export.
- Unattended auto-approve or auto-apply pipelines.
- Building the reconciliation UI itself (queue may expose a view model consumable by UI).
- Automatic resolution of duplicate/conflict catalogue entries without human cleanup.
- External workflow/ticketing integrations (email, Slack, PSA).
- Changes to reconciliation matching rules or classification logic (upstream producers remain separate features).
