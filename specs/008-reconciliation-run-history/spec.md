# Feature Specification: Reconciliation Run History & Audit

**Feature Branch**: `008-reconciliation-run-history`

**Created**: 2026-07-02

**Status**: Draft

**Input**: User description: "Design a history and audit model for reconciliation runs. Store each run and track input snapshots, canonical mapping version, results, exceptions, proposed actions with approval status, and execution results. Enable month-to-month comparison, drift trends, and pricing drift trends. Focus on traceability and long-term visibility."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Persist a Complete Reconciliation Run Record (Priority: P1)

As a billing operator, I need every reconciliation run stored as an immutable historical record that captures what inputs were used, which product-mapping rules applied, what the engine found, and what was proposed—so that I can answer "what did we know and decide at the time?" months later without re-ingesting source files.

**Why this priority**: Without durable run records, all downstream comparison and trend analysis is impossible. This is the foundation for traceability and audit.

**Independent Test**: Given a completed reconciliation run with representative inputs from all four source domains, the system persists one run record containing normalized input snapshots, mapping version identity, full results, exceptions, and proposed actions; retrieving that run by identifier reproduces the same outcome summary without re-executing reconciliation.

**Acceptance Scenarios**:

1. **Given** a reconciliation run completes with supplier cost lines, subscription truth lines, intended retail pricing (standard list plus manual overrides), and Stripe billing/catalogue data, **When** the run is finalized, **Then** a historical run record is created with immutable snapshots of each input domain, source file identities (name, upload timestamp, content fingerprint), and the billing period scope of the run.
2. **Given** a run uses a specific canonical product-mapping version, **When** the run record is stored, **Then** the mapping version identifier and effective date are recorded so future reviewers know which mapping rules were in force.
3. **Given** a completed run with mismatches and proposed corrective actions, **When** the run record is retrieved, **Then** it includes match groups, categorized exceptions, and all proposed actions exactly as produced at run time.
4. **Given** the same inputs and mapping version are re-run later, **When** a new run is stored, **Then** it receives a distinct run identity and timestamp; prior run records remain unchanged.
5. **Given** a run where one input domain was empty or unavailable, **When** the run is stored, **Then** the record explicitly marks which input snapshots are present versus absent rather than implying completeness.

---

### User Story 2 - Browse and Inspect Run History (Priority: P1)

As a billing operator or reviewer, I need to browse past reconciliation runs, filter by billing period and date, and open any run to inspect its inputs, results, and linked decisions—so that I can investigate historical billing drift without relying on spreadsheets or memory.

**Why this priority**: Stored data delivers value only when operators can find and read it. Inspection is the primary day-to-day audit workflow.

**Independent Test**: Given at least six stored runs spanning three billing periods, an operator can list runs in reverse chronological order, filter to one billing period, open a run, and view input snapshot summaries, exception counts, proposal counts, and approval status breakdown without re-running reconciliation.

**Acceptance Scenarios**:

1. **Given** multiple stored runs, **When** the operator opens run history, **Then** runs are listed with run date, billing period scope, input snapshot presence indicators, total exception count, and proposal count.
2. **Given** a selected run, **When** the operator views run detail, **Then** they see input snapshot metadata, mapping version, reconciliation summary metrics (matched groups, issue counts by category), and links to exceptions and proposals from that run.
3. **Given** a run whose proposals were ingested into the approval workflow, **When** run detail is viewed, **Then** each proposal shows its current approval status (pending, approved, rejected, stale, historical) as of query time without altering stored run results.
4. **Given** a run with no exceptions, **When** it appears in history, **Then** it is retained and visible as a "clean run" with zero-issue summary rather than being discarded.
5. **Given** an operator searching for a specific customer, **When** they filter run results within a run, **Then** they can locate that customer's match groups and exceptions from the stored run snapshot.

---

### User Story 3 - Compare Runs Month-to-Month (Priority: P2)

As a billing operator, I need to compare two reconciliation runs—typically consecutive billing periods—to see what changed in inputs, exceptions, and proposals—so that I can distinguish new drift from carry-over issues and verify whether prior corrections took effect.

**Why this priority**: Month-to-month comparison is the most common audit question after basic run inspection. It turns isolated snapshots into actionable change analysis.

**Independent Test**: Given stored runs for January and February billing periods, the operator selects both runs and receives a structured comparison showing input deltas (new/removed/changed line counts per domain), exception deltas (new, resolved, persisting), and proposal deltas—without requiring manual diff of raw files.

**Acceptance Scenarios**:

1. **Given** two stored runs for adjacent billing periods with the same customer base, **When** month-to-month comparison is requested, **Then** the comparison report shows counts and lists of exceptions that are new, resolved since the earlier run, and persisting across both runs.
2. **Given** two runs where subscription truth quantities changed for a customer-product pair, **When** comparison runs, **Then** the report highlights the prior and current normalized values from each run's input snapshot.
3. **Given** two runs where the canonical mapping version differs, **When** comparison runs, **Then** the report flags the mapping version change and distinguishes mapping-driven differences from data-driven differences where possible.
4. **Given** two runs where approval decisions were made between them, **When** comparison includes proposal status, **Then** persisting exceptions that still have approved-but-not-yet-applied proposals are visibly distinguished from unresolved drift.
5. **Given** runs more than twelve months apart, **When** comparison is requested, **Then** the system still produces a comparison using stored snapshots without requiring source files to still exist locally.

---

### User Story 4 - Identify Recurring Drift Trends (Priority: P2)

As a billing operator, I need to see which mismatches repeat across multiple runs for the same customer, product, or issue category—so that I can prioritize root-cause fixes over repeatedly reviewing the same unresolved drift.

**Why this priority**: Recurring mismatches indicate systemic mapping, pricing, or process gaps. Trend visibility reduces operator fatigue and focuses effort.

**Independent Test**: Given five stored runs where the same quantity mismatch appears in four of them for one customer-product pair, the drift trend view ranks that issue as recurring, shows first seen and last seen run dates, and lists intervening run outcomes.

**Acceptance Scenarios**:

1. **Given** multiple stored runs, **When** drift trend analysis runs over a selected time window, **Then** the system surfaces mismatches that appear in more than one run, grouped by stable identity (customer, commercial product key, issue category).
2. **Given** a recurring mismatch, **When** the operator opens its trend detail, **Then** they see occurrence count, first and last run dates, and whether associated proposals were approved, rejected, or left pending across runs.
3. **Given** a mismatch that appeared once and was absent in subsequent runs, **When** trend analysis runs, **Then** it is classified as resolved or transient rather than recurring.
4. **Given** recurring mapping-missing issues, **When** trends are displayed, **Then** they are grouped separately from recurring quantity or price mismatches so operators can distinguish data-quality problems from billing drift.
5. **Given** a customer who churned (no longer present in newer runs), **When** trend analysis runs, **Then** recurring issues for that customer show a "last seen" indicator without implying ongoing drift.

---

### User Story 5 - Track Pricing Drift Between Intended Retail Pricing and Stripe Catalogue (Priority: P2)

As a billing operator, I need to see how intended retail pricing and the Stripe product/price catalogue evolved across runs—so that I can detect when RRP changes were not reflected in Stripe, when manual overrides shifted effective pricing, or when catalogue gaps persist over time.

**Why this priority**: Pricing drift affects margin and customer billing at scale. Separating RRP-vs-Stripe catalogue trends from subscription-level mismatches clarifies where catalogue maintenance is failing.

**Independent Test**: Given four stored runs where intended retail pricing changed for one offer/SKU and Stripe catalogue prices lagged for two runs before aligning, the pricing drift view shows timeline entries for RRP amount changes, Stripe price amount changes, manual override events, and catalogue-missing flags per commercial key.

**Acceptance Scenarios**:

1. **Given** stored runs with intended retail pricing and Stripe catalogue snapshots, **When** pricing drift analysis is requested for a commercial product key, **Then** the timeline shows intended price, effective override (if any), and Stripe catalogue price amounts across runs with change dates.
2. **Given** a run where intended retail pricing introduced a new amount but Stripe catalogue still held the old price, **When** pricing drift is viewed, **Then** the lag is flagged with the run in which the discrepancy first appeared and how many runs it persisted.
3. **Given** manual price overrides present in one run's input snapshot and absent in the next, **When** pricing drift is viewed, **Then** the override addition or removal is recorded as a distinct pricing event.
4. **Given** catalogue-missing issues recurring across runs for the same offer/SKU and billing interval, **When** pricing drift trends are shown, **Then** they appear as persistent catalogue gaps distinct from amount mismatches.
5. **Given** a clean catalogue alignment in the latest run, **When** pricing drift history is viewed, **Then** prior lag periods remain visible for audit without altering current status.

---

### User Story 6 - Link Approval and Execution Outcomes to Run History (Priority: P3)

As a billing operator or auditor, I need each run record to reflect the approval decisions made on its proposals and, when write-back is enabled in the future, the execution outcomes—so that the full lifecycle from detection through decision through application is traceable from one place.

**Why this priority**: Approval audit exists separately today; linking it to run history completes the story but depends on persisted runs and the existing approval workflow.

**Independent Test**: Given a stored run whose proposals received mixed approval decisions, opening the run shows proposal-level approval status and decision metadata; when a future execution result exists for an approved proposal, it appears on the run record without mutating stored reconciliation results.

**Acceptance Scenarios**:

1. **Given** proposals from a run were approved or rejected in the approval workflow, **When** run history detail is viewed, **Then** each proposal shows decision state, decider identity, timestamp, and rejection reason when applicable.
2. **Given** a later run superseded pending proposals from an earlier run, **When** the earlier run is viewed, **Then** superseded proposals show stale or historical status linked to the superseding run identity.
3. **Given** write-back execution is not yet enabled, **When** run records are stored, **Then** execution outcome fields are present but empty, and the model supports recording execution status, timestamp, and outcome summary when execution is added later.
4. **Given** an approved proposal with a recorded execution failure in a future release, **When** run history is viewed, **Then** the execution failure is visible alongside the approval record without altering the original proposed values from the run snapshot.
5. **Given** an exported approved changeset associated with a run, **When** run detail is viewed, **Then** export metadata (export time, exporter, entry count) is referenceable from the run record.

---

### Edge Cases

- What happens when a run is started but fails before producing results? A failed run record is stored with failure reason, partial input snapshot state, and no results—so operators know the attempt occurred.
- What happens when input source files are re-uploaded with the same name but different content? Each snapshot records a content fingerprint; duplicate filenames across runs are distinguished by fingerprint and upload timestamp.
- What happens when storage limits approach configured retention thresholds? Older runs are eligible for archival or tiered retention per policy; archived runs remain discoverable with restored-on-demand detail, and operators receive notice before any run is purged.
- What happens when mapping version is updated mid-period and a re-run occurs? Both runs are retained with their respective mapping version identifiers; comparisons flag the version change.
- What happens when a customer appears under a different Mex ID between runs? Trend analysis treats them as separate identities unless an explicit customer alias mapping exists; the spec does not require automatic identity merge.
- What happens when intended retail pricing manual overrides conflict within one run's snapshot? The stored snapshot reflects the effective winning override used by reconciliation, with visibility that an override was applied.
- What happens when approval workflow data is unavailable for an old run? Run results and input snapshots remain intact; approval status shows as unknown or not ingested rather than blocking run retrieval.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST persist one immutable historical record per reconciliation run attempt, including successful and failed runs.
- **FR-002**: Each run record MUST capture normalized input snapshots for: supplier cost lines (from PDF ingestion), Microsoft subscription truth lines (from subscription management report), intended retail pricing (standard price list plus manual overrides), and Stripe state (active subscription items plus product and price catalogue entries relevant to the run scope).
- **FR-003**: Each input snapshot MUST record source identity metadata: originating file or export name, ingestion timestamp, content fingerprint, and billing period scope covered.
- **FR-004**: Each run record MUST record the canonical product-mapping version identifier and effective date used during reconciliation.
- **FR-005**: Each run record MUST store the complete reconciliation output snapshot: match groups, categorized exceptions, and proposed corrective actions exactly as produced at run time.
- **FR-006**: Run records MUST be immutable after finalization; corrections require a new run rather than editing stored history.
- **FR-007**: The system MUST assign each run a unique identifier and record run start time, completion time, billing period scope, and operator or initiator identity when available.
- **FR-008**: The system MUST provide a browsable run history ordered by run date with filters for billing period, date range, and run outcome (success, failed, clean, with exceptions).
- **FR-009**: The system MUST allow retrieval of a single run's full detail including all input snapshot summaries, mapping version, results, exceptions, and proposals.
- **FR-010**: The system MUST support comparison of exactly two stored runs and produce structured delta reports for exceptions and input changes.
- **FR-011**: Comparison MUST classify exceptions as new, resolved, or persisting relative to the earlier and later run using stable mismatch identity (customer, commercial product key, issue category, and distinguishing attributes).
- **FR-012**: The system MUST support drift trend analysis over a selectable time window, surfacing mismatches that recur across multiple runs with occurrence count and first/last seen dates.
- **FR-013**: The system MUST support pricing drift analysis that tracks intended retail pricing amounts, manual overrides, and Stripe catalogue price amounts per commercial product key across runs over time.
- **FR-014**: Pricing drift analysis MUST distinguish amount mismatches, catalogue-missing conditions, and override additions or removals as separate event types.
- **FR-015**: Each run record MUST link to approval status for its proposals by reference to the approval workflow, reflecting current decision state without mutating stored run results.
- **FR-016**: The run history model MUST include reserved fields for future write-back execution outcomes (status, timestamp, outcome summary, error detail) per proposal or per approved changeset entry.
- **FR-017**: The system MUST retain run records according to configured retention policy with a default minimum of twenty-four months of online availability.
- **FR-018**: Archived or tiered runs MUST remain discoverable in history lists with clear archived status; full detail retrieval MUST remain possible within policy limits.
- **FR-019**: Run history views MUST present exception and proposal counts consistently with reconciliation and approval terminology used elsewhere in the product.
- **FR-020**: The system MUST record audit events when run records are created, compared, or exported for external review.

### Key Entities *(include if feature involves data)*

- **ReconciliationRunRecord**: Immutable aggregate for one run attempt. Attributes include run identifier, billing period scope, timestamps, initiator, mapping version reference, input snapshot references, results snapshot, status (completed, failed), and summary metrics.
- **InputSnapshot**: Point-in-time capture of one input domain used by a run. Attributes include domain type (supplier cost, subscription truth, intended retail pricing, Stripe billing/catalogue), source metadata, normalized record count, content fingerprint, and serialized normalized payload or reference to stored payload.
- **MappingVersionReference**: Identifies the canonical product-mapping rules applied. Attributes include version identifier, effective date, and optional change summary label.
- **RunResultsSnapshot**: Frozen reconciliation output. Attributes include match groups, exceptions, proposed actions, and summary counts by issue category—aligned with engine output at run time.
- **RunComparisonReport**: Derived artifact comparing two runs. Attributes include earlier and later run references, exception deltas (new, resolved, persisting), input change summaries, and mapping version change indicator.
- **DriftTrendEntry**: Aggregated view of a recurring mismatch. Attributes include stable mismatch identity, occurrence count, first seen run, last seen run, associated proposal decision summary across runs, and issue category.
- **PricingDriftTimelineEntry**: Aggregated pricing change view for one commercial product key. Attributes include commercial key, event type (RRP change, override change, Stripe price change, catalogue missing), effective amounts, run date, and persistence duration when applicable.
- **ProposalStatusLink**: Join between a stored proposed action and current approval workflow state. Attributes include proposal identifier, decision state, decider, decision timestamp, rejection reason, supersession run reference, and optional future execution outcome.
- **ExecutionOutcome** *(future-ready)*: Result of applying an approved change. Attributes include proposal reference, execution status, executed timestamp, outcome summary, and error detail when failed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operators can retrieve any stored run's full summary and input metadata within 5 seconds for a typical run covering up to 500 customer-product groups.
- **SC-002**: 100% of completed reconciliation runs produce a persisted run record with all four input domains explicitly marked present or absent.
- **SC-003**: Month-to-month comparison between two stored runs completes within 10 seconds for typical run sizes and correctly classifies at least 95% of exceptions as new, resolved, or persisting when validated against manual review samples.
- **SC-004**: Recurring drift trends surface any mismatch appearing in three or more runs within a six-month window with zero false omission in regression test fixtures.
- **SC-005**: Pricing drift timeline correctly identifies RRP amount changes and Stripe catalogue lag persisting across two or more runs in pricing regression fixtures.
- **SC-006**: Auditors can answer "who approved what for run X?" by viewing run history links to approval decisions without re-running reconciliation or opening raw source files.
- **SC-007**: Run records remain readable and comparable for at least twenty-four months after creation under default retention policy without data loss.
- **SC-008**: Operator task time to investigate a recurring mismatch drops by at least 40% versus manual spreadsheet comparison across historical exports (measured in usability validation with representative fixtures).

## Assumptions

- Reconciliation engine output structure (match groups, exceptions, proposed actions) from feature 004 remains the authoritative shape for stored results snapshots.
- Approval workflow (feature 007) remains the system of record for proposal decisions; run history links to approval state rather than duplicating decision logic.
- Input snapshots store normalized reconciliation-ready data plus source metadata, not raw PDF bytes or raw CSV text, though content fingerprints allow verifying source file integrity.
- Billing period scope aligns with monthly supplier billing cycles unless operators explicitly define a different scope per run.
- Canonical mapping version is versioned independently with monotonically identifiable releases; exact versioning scheme is defined during planning.
- Write-back execution to Stripe is out of scope for initial delivery; execution outcome fields are modeled but populated only when a future feature enables apply.
- Single-tenant reseller deployment; run history is not partitioned by tenant in v1.
- Default retention is twenty-four months online with configurable extension; indefinite retention is supported when configured.
- Customer identity for trend analysis uses Mex ID as primary key; cross-run customer merges require explicit alias mapping outside this feature's initial scope.
- Failed runs are retained for audit visibility rather than silently discarded.

## Dependencies

- **004-reconciliation-engine**: Source of `ReconciliationRun`, exceptions, and proposed actions to snapshot.
- **007-reconciliation-approval-workflow**: Source of proposal decision state and export metadata to link from run records.
- **001-billing-domain-model**: Normalized entity shapes referenced in input and results snapshots.
- **002-giacom-pdf-ingestion**, **003-stripe-csv-ingestion**: Upstream ingestion metadata (file identity, fingerprints) attached to input snapshots.

## Out of Scope

- Automatic Stripe write-back or execution orchestration (reserved fields only).
- Real-time streaming analytics or alerting on drift trends.
- Cross-tenant or multi-reseller run history partitioning.
- Automatic customer identity merge across Mex ID changes.
- Storing raw binary PDF or full raw CSV payloads as the primary snapshot format (fingerprints and normalized data are stored instead).
- Replacing the approval workflow's decision audit trail; this feature links to it and adds run-level context.
