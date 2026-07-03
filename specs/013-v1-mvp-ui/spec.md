# Feature Specification: V1 MVP Operator UI

**Feature Branch**: `013-v1-mvp-ui`

**Created**: 2026-07-03

**Status**: Draft

**Input**: User description: "The codebase backend is now at V1 for BillDrift. The UI needs to be available for the various use cases and workflows that the backend currently supports. Scope excludes new Application-layer features/functionality (the domain/business logic is frozen), but DOES include adding API endpoints that expose existing Application-layer capabilities so the UI can access them. For example, Giacom billing PDF ingestion logic exists in the Application layer but has no API endpoint; adding that endpoint (and the UI for it) is in scope. Highlight only mismatches that would require new Application-layer features/functionality — those remain out of scope and are not planned."

## Overview

BillDrift keeps Stripe (customer billing source of truth) in sync with supplier cost reality (Giacom billing PDFs), Microsoft 365 subscription truth (Giacom Subscription Management report), and intended retail pricing (Giacom price list). Operators detect mismatches, review margin anomalies, and produce a human-approved change set before drift becomes revenue leakage or customer over/under-billing.

The **Application layer** (domain and business logic for V1 ingestion, reconciliation, exception surfacing, classification, catalogue validation, approval workflow, and run history) is functionally complete and is **frozen for this feature** — no new Application-layer features or functionality are in scope. However, several of these capabilities exist only as Application-layer services with **no HTTP/API endpoint**, so the UI cannot reach them, and some navigation routes are dead or partially implemented.

This feature delivers the **operator UI** needed to perform the MVP workflows end-to-end. Its scope spans two layers:

- **API layer (in scope)**: Add or extend HTTP endpoints that expose *existing* Application-layer capabilities (e.g. Giacom PDF ingestion, Stripe CSV ingestion, reconciliation orchestration, exception surfacing) so the UI can consume them. These endpoints are thin adapters over existing services — they orchestrate and expose, they do not introduce new domain logic.
- **Web/UI layer (in scope)**: Build the Blazor operator screens for every MVP workflow, wiring them to the API.

The **Application layer is out of scope**. Any capability that would require *new* Application-layer features or functionality (new domain services, new business rules, new persistence models) is documented in **Application-Layer Capability Notes** — flagged for awareness, not planned for this feature.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Upload and Review Data Sources (Priority: P1)

An operator preparing for reconciliation uploads the available source files (Giacom billing PDF, Stripe CSV, Subscription Management CSV, retail pricing CSV) through the application, sees confirmation of successful parsing, and can review what was ingested (record counts, run identifiers, validation errors) before proceeding. Where an ingestion capability exists in the Application layer but lacks an API endpoint, this feature adds the endpoint so the UI can drive it.

**Why this priority**: Reconciliation cannot begin without current inputs. Upload is the first step in every billing cycle and the foundation for all downstream workflows.

**Independent Test**: Can be fully tested by uploading valid and invalid source files (PDF and CSV) and verifying the operator sees success summaries, error messages, and a list of recent import runs without running reconciliation.

**Acceptance Scenarios**:

1. **Given** an operator on the ingestion area, **When** they upload a valid Giacom Subscription Management CSV, **Then** the system confirms successful import with record count, import run identifier, and timestamp.
2. **Given** an operator on the ingestion area, **When** they upload a valid ResellerPricingVsRRP.csv (with optional manual pricing overrides), **Then** the system confirms successful import with resolved pricing record count and any override notes.
3. **Given** an operator on the ingestion area, **When** they upload a valid Giacom billing PDF, **Then** the system parses it via the existing Application-layer ingester (exposed through a new API endpoint) and confirms successful import with parsed cost-line count and run identifier.
4. **Given** an operator on the ingestion area, **When** they upload valid Stripe CSV files (subscriptions, and/or products + prices), **Then** the system parses and persists them via the existing Application-layer ingester (exposed through a new API endpoint) and confirms successful import.
5. **Given** an operator uploads a file with invalid format or missing required columns, **When** parsing fails, **Then** the system shows a clear error explaining what is wrong and what to fix, without exposing internal technical details.
6. **Given** prior import runs exist, **When** the operator views import history, **Then** they see a list of recent runs per source type with status, date, and record counts.

---

### User Story 2 - Run Reconciliation and Review Exceptions (Priority: P1)

An operator initiates a reconciliation run using ingested data snapshots, reviews surfaced exceptions grouped by type (missing in Stripe, quantity mismatch, billing frequency mismatch, price mismatch, missing catalogue item, mapping/metadata mismatch, non-CSP items), and understands proposed corrective actions before any approval.

**Why this priority**: Exception review is the core value proposition — detecting drift between supplier truth, cost reality, intended pricing, and Stripe.

**Independent Test**: Can be fully tested by triggering a reconciliation (via the reconciliation orchestration endpoint added in this feature, which wraps the existing reconciliation engine and exception-surfacing services), viewing the exception dashboard with filters, and verifying each exception shows what differs and why it was flagged.

**Acceptance Scenarios**:

1. **Given** required input snapshots are available, **When** the operator starts a reconciliation run, **Then** the system shows run progress and, on completion, a summary with mismatch counts by category.
2. **Given** a completed reconciliation run, **When** the operator opens the exception dashboard, **Then** they see exceptions grouped or filterable by type (missing in Stripe, quantity mismatch, frequency mismatch, price mismatch, missing catalogue, mapping mismatch, non-CSP).
3. **Given** an exception in the list, **When** the operator views its detail, **Then** they see the customer, product, expected vs actual values (quantity, price, frequency, period), and the business rule that fired.
4. **Given** a reconciliation run with zero exceptions, **When** the operator views results, **Then** the system clearly indicates a clean run and offers to archive or proceed to history.

---

### User Story 3 - Manage Product Mapping and Classification (Priority: P2)

An operator maintains the canonical mapping between Offer ID/SKU, Stripe product and price identifiers, Giacom naming variants, and product classification (Microsoft CSP vs non-CSP), and applies classification overrides for items that need manual rules.

**Why this priority**: Accurate mapping is critical for reconciliation quality; misclassified or unmapped items produce false exceptions and block approval.

**Independent Test**: Can be fully tested by viewing/editing mappings and classification rules, applying overrides, and verifying changes are reflected in subsequent reconciliation views.

**Acceptance Scenarios**:

1. **Given** an operator on the mapping area, **When** they view the mapping table, **Then** they see Offer ID/SKU, associated Stripe product and price identifiers, Giacom name variants, and CSP vs non-CSP classification.
2. **Given** an unmapped or ambiguous product, **When** the operator adds or updates a mapping entry, **Then** the entry is saved and available for the next reconciliation run.
3. **Given** a non-CSP item (e.g. Exclaimer) flagged for manual handling, **When** the operator applies a classification override, **Then** the override is recorded and the item is routed to the appropriate exception/approval category.
4. **Given** classification configuration (internal Mex IDs, product category rules), **When** the operator updates rules, **Then** changes apply to future reconciliation runs.

---

### User Story 4 - Review and Approve Proposed Changes (Priority: P1)

An operator reviews proposed corrective actions (create missing subscription item, update quantity, switch to correct price, optional catalogue fixes), approves or rejects items individually or in bulk, and never triggers automatic Stripe changes.

**Why this priority**: Human approval before billing changes is a constitutional requirement and the final gate before export.

**Independent Test**: Can be fully tested by loading a run's approval queue, approving/rejecting proposals across tabs (Subscription, Catalogue, Investigation), and verifying queue state and audit trail update correctly.

**Acceptance Scenarios**:

1. **Given** a completed reconciliation run with proposals, **When** the operator opens the approval queue for that run, **Then** they see proposals grouped by customer and filterable by tab (Subscription, Catalogue, Investigation).
2. **Given** a pending proposal, **When** the operator views it, **Then** they see what differs, the proposed corrective action, and impact description before approving or rejecting.
3. **Given** multiple pending proposals, **When** the operator uses bulk approve with preview, **Then** they see a summary of all items to be approved and must confirm before applying.
4. **Given** the operator rejects a proposal, **When** they provide a reason, **Then** the proposal is marked rejected and the reason appears in the audit trail.
5. **Given** proposals exist but have not been ingested into the queue, **When** the operator triggers proposal ingestion from the reconciliation results, **Then** proposals appear in the approval queue.

---

### User Story 5 - Export Approved Changeset (Priority: P2)

An operator exports the approved changeset as a file ready for manual application to Stripe (API write deferred to a future phase).

**Why this priority**: Export is the deliverable that closes the reconciliation cycle and enables operators to apply approved changes safely outside the tool.

**Independent Test**: Can be fully tested by approving a subset of proposals, exporting the changeset, and downloading a file containing only approved items with sufficient detail for manual Stripe application.

**Acceptance Scenarios**:

1. **Given** one or more approved proposals, **When** the operator requests export, **Then** the system generates a changeset containing only approved items.
2. **Given** an export is ready, **When** the operator downloads it, **Then** the file includes customer, product, action type, and field-level change details sufficient for manual Stripe application.
3. **Given** no approved proposals exist, **When** the operator attempts export, **Then** the system explains that nothing is available to export.

---

### User Story 6 - Run Catalogue Reconciliation (Priority: P2)

An operator validates that Stripe products and prices exist for mapped products and align to intended RRP, flags missing prices (monthly vs annual gaps), and optionally ingests catalogue fix proposals into the approval queue.

**Why this priority**: Catalogue/pricing drift is a distinct workflow from subscription reconciliation and prevents silent margin erosion.

**Independent Test**: Can be fully tested by triggering a catalogue reconciliation run, reviewing missing/misaligned catalogue items, and ingesting fix proposals into the approval queue.

**Acceptance Scenarios**:

1. **Given** product mappings and pricing data are available, **When** the operator starts catalogue reconciliation, **Then** the system shows results listing missing Stripe products/prices and price amount mismatches vs intended RRP.
2. **Given** catalogue reconciliation results, **When** the operator views detail for an item, **Then** they see expected RRP, actual Stripe price, and whether monthly/annual variants are missing.
3. **Given** catalogue fix proposals, **When** the operator ingests them into the approval queue, **Then** proposals appear under the Catalogue tab for review.

---

### User Story 7 - View Margin Anomalies (Priority: P2)

An operator reviews margin (RRP minus cost, margin percentage) across reconciled lines and identifies negative, low, or unexpected margins.

**Why this priority**: Margin view was moved into MVP scope; it surfaces revenue leakage that subscription/price mismatch alone may not reveal.

**Independent Test**: Can be fully tested by viewing margin data for a reconciliation run and verifying negative/low margins are visually highlighted with cost, RRP, and percentage.

**Acceptance Scenarios**:

1. **Given** a reconciliation run with cost and RRP data, **When** the operator opens the margin view, **Then** they see per-line margin amount and margin percentage.
2. **Given** lines with negative or below-threshold margin, **When** displayed, **Then** they are visually distinguished from healthy margins.
3. **Given** a line with missing cost or RRP, **When** displayed, **Then** the system indicates which value is unavailable rather than showing a misleading margin.

---

### User Story 8 - Browse Run History and Trends (Priority: P3)

An operator browses archived reconciliation runs, compares runs over time, views input snapshots and audit trails, and identifies recurring drift patterns.

**Why this priority**: History supports operational continuity across billing cycles and validates that prior approvals resolved issues.

**Independent Test**: Can be fully tested by listing runs with filters, opening run detail with all tabs, comparing two runs, and viewing drift/pricing trends.

**Acceptance Scenarios**:

1. **Given** archived runs exist, **When** the operator opens run history, **Then** they see runs with date, billing period, mismatch count, proposal count, input presence indicators, and clean-run status.
2. **Given** a selected run, **When** the operator views detail, **Then** they see Summary, Inputs, Exceptions, Proposals (with approval status), and Audit tabs.
3. **Given** two runs to compare, **When** the operator selects them from run pickers (not manual ID entry), **Then** they see input changes, new/resolved/persisting exceptions, and mapping version warnings.
4. **Given** multiple historical runs, **When** the operator views drift trends, **Then** they see recurring exception patterns and pricing drift over time.

---

### User Story 9 - Workflow Home and Navigation (Priority: P3)

An operator lands on a home page that orients them to the current billing cycle workflow (upload → reconcile → review → approve → export) and can navigate to all primary areas without dead links.

**Why this priority**: Coherent navigation reduces operator confusion and aligns with the constitution's UX consistency principle.

**Independent Test**: Can be fully tested by verifying all nav items resolve to working pages and the home page shows workflow status and quick links.

**Acceptance Scenarios**:

1. **Given** an operator opens the application, **When** they view the home page, **Then** they see the reconciliation workflow steps and links to each primary area.
2. **Given** the main navigation, **When** the operator clicks any item (Ingestion, Reconciliation, Approvals, Run History, Mapping), **Then** they reach a working page with no broken routes.
3. **Given** a reconciliation run with pending approvals, **When** the operator views home or run detail, **Then** they see a prominent link to the approval queue for that run.

---

### Edge Cases

- What happens when the operator uploads a duplicate file (same content as a recent import)? The system should indicate the file was already imported or show the new run alongside prior runs without silent deduplication unless the backend defines it.
- What happens when required inputs for reconciliation are missing? The system must block or warn before starting a run and list which sources are absent.
- What happens when a reconciliation run produces hundreds of exceptions? The exception dashboard must support filtering, sorting, and pagination without overwhelming the operator.
- What happens when proposals become stale after a newer reconciliation run? Stale proposals must be visually marked and not mixed with current actionable items without warning.
- What happens when the operator has no archived runs yet? Empty states must explain how to complete a first reconciliation cycle.
- What happens when export is requested while proposals are still pending? Export includes only approved items; pending items are excluded with a clear message if none are approved yet.
- What happens when classification or mapping data conflicts with ingested source naming? The UI surfaces the conflict and points the operator to mapping/classification tools.

## Requirements *(mandatory)*

### Functional Requirements

**Ingestion**

- **FR-001**: System MUST provide an operator UI to upload Giacom Subscription Management CSV files and display import results (success, record count, run identifier, errors).
- **FR-002**: System MUST provide an operator UI to upload ResellerPricingVsRRP.csv with optional manual pricing overrides and display resolved pricing results.
- **FR-003**: System MUST provide an operator UI to upload Giacom billing PDF files, using a new API endpoint that exposes the existing Application-layer PDF ingester, and display parsed cost-line results.
- **FR-004**: System MUST provide an operator UI to upload Stripe CSV files (subscriptions, and products + prices), using a new API endpoint that exposes the existing Application-layer Stripe CSV ingester, and display import results.
- **FR-005**: System MUST show import history per source type with status, timestamp, and record counts.
- **FR-006**: System MUST display clear, actionable error messages when file upload or parsing fails.

**API Enablement** *(thin adapters over existing Application-layer services — no new domain logic)*

- **FR-007**: System MUST expose an API endpoint to ingest Giacom billing PDFs by invoking the existing Application-layer PDF ingester.
- **FR-008**: System MUST expose an API endpoint to ingest Stripe CSV files (subscriptions, products, prices) by invoking the existing Application-layer Stripe ingester and persisting via the existing blob store.
- **FR-009**: System MUST expose an API endpoint to orchestrate a reconciliation run that loads available ingestion snapshots, invokes the existing reconciliation engine and exception-surfacing service, optionally persists the run, and returns a run summary with exceptions.
- **FR-010**: System MUST expose reconciliation exceptions to the UI (either via the reconciliation run endpoint response or a dedicated read endpoint) using the existing exception-surfacing output.
- **FR-011**: System MUST expose an API path for the UI to ingest reconciliation proposals into the approval queue without the operator manually assembling the full ingestion payload.
- **FR-012**: API endpoints added by this feature MUST NOT introduce new Application-layer domain logic; they orchestrate and expose existing services only.

**Reconciliation & Exceptions**

- **FR-013**: System MUST provide an operator UI to initiate a reconciliation run when required inputs are available and display run summary on completion.
- **FR-014**: System MUST display surfaced exceptions filterable by category: missing in Stripe, quantity mismatch, billing frequency mismatch, price mismatch, missing catalogue item, mapping/metadata mismatch, and non-CSP items.
- **FR-015**: Each exception MUST show customer, product, expected vs actual values, and the business reason it was flagged.
- **FR-016**: System MUST indicate clean runs (zero exceptions) distinctly from runs with issues.
- **FR-017**: System MUST provide a margin view showing RRP minus cost and margin percentage, with visual distinction for negative or low margins.

**Mapping & Classification**

- **FR-018**: System MUST provide an operator UI to view and manage canonical product mappings (Offer ID/SKU, Stripe product/price IDs, Giacom name variants, CSP vs non-CSP classification), within the limits of existing Application-layer capabilities (see Application-Layer Capability Notes for mapping persistence).
- **FR-019**: System MUST provide an operator UI to apply and clear per-item classification overrides.
- **FR-020**: System MUST provide an operator UI to manage classification configuration (internal Mex IDs, product category rules).

**Approval Workflow**

- **FR-021**: System MUST provide an approval queue UI grouped by customer with Subscription, Catalogue, and Investigation tabs.
- **FR-022**: Operators MUST be able to approve or reject proposals individually with mandatory rejection reason.
- **FR-023**: Operators MUST be able to bulk approve with a preview-and-confirm step.
- **FR-024**: System MUST trigger proposal ingestion from reconciliation results into the approval queue via the UI.
- **FR-025**: System MUST NEVER apply Stripe changes automatically; all writes remain export-only for manual application.
- **FR-026**: System MUST display an audit trail of approval actions (who, what, when, outcome).

**Export**

- **FR-027**: System MUST allow operators to export an approved changeset as a downloadable file.
- **FR-028**: Export MUST include only approved proposals with sufficient detail for manual Stripe application.

**Catalogue Reconciliation**

- **FR-029**: System MUST provide an operator UI to run catalogue reconciliation and view missing/misaligned Stripe products and prices vs intended RRP.
- **FR-030**: System MUST allow ingesting catalogue fix proposals into the approval queue from catalogue reconciliation results.

**Run History**

- **FR-031**: System MUST provide a run history list with filters (billing period, date range, clean runs, archived).
- **FR-032**: Run detail MUST include Summary, Inputs, Exceptions, Proposals (with approval status badges), and Audit tabs.
- **FR-033**: System MUST support run comparison via run selection (not manual identifier entry) showing input changes and exception deltas.
- **FR-034**: System MUST provide drift and pricing trend views for recurring pattern analysis.
- **FR-035**: Input snapshots MUST be viewable from run detail (filename, fingerprint, record count).

**Navigation & UX**

- **FR-036**: All primary navigation links MUST resolve to working pages; no dead routes.
- **FR-037**: Home page MUST orient operators to the upload → reconcile → review → approve → export workflow.
- **FR-038**: Error, empty, and loading states MUST be consistent and tell the operator what went wrong and what to do next.
- **FR-039**: Terminology MUST be consistent across UI surfaces: discrepancy/exception, corrective action/proposal, approval, dry run/preview where applicable.

### Key Entities

- **Import Run**: A single upload and parse attempt for a source file; attributes include source type, status, timestamp, record count, run identifier.
- **Reconciliation Run**: A completed comparison of ingested snapshots; attributes include billing period, mismatch counts by category, proposal count, archival status.
- **Exception (Discrepancy)**: A flagged mismatch between truth sources; attributes include category, customer, product, expected/actual values, business rule reference.
- **Product Mapping**: Canonical link between Offer ID/SKU, Stripe identifiers, Giacom naming variants, and CSP classification.
- **Classification Override**: Operator-applied manual classification for a specific item outside automatic rules.
- **Approval Proposal**: A proposed corrective action; attributes include action type, customer, product, field changes, status (pending/approved/rejected/stale).
- **Approved Changeset Export**: Downloadable artifact of approved proposals ready for manual Stripe application.
- **Catalogue Reconciliation Run**: Validation of Stripe catalogue vs mappings and intended pricing; attributes include missing items, price mismatches, fix proposals.
- **Archived Run**: Persisted reconciliation snapshot with input blobs, results, proposals, and audit events for historical analysis.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operators can upload every supported source type (Giacom PDF, Stripe CSV, Subscription Management CSV, retail pricing CSV) and see import confirmation within one minute per file under normal conditions.
- **SC-002**: Operators can complete the full review cycle (view exceptions → approve proposals → export changeset) for a run without leaving the application or using command-line tools.
- **SC-003**: 100% of primary navigation items resolve to functional pages with no dead routes.
- **SC-004**: Operators can identify the category and business reason for any exception within 30 seconds of opening its detail.
- **SC-005**: Operators can filter the exception dashboard to a single category and see only relevant items.
- **SC-006**: Bulk approve preview shows all items before confirmation; zero unintended approvals in user acceptance testing.
- **SC-007**: Exported changesets contain only approved items; pending and rejected items never appear in export.
- **SC-008**: Run comparison can be performed by selecting two runs from lists without manual identifier entry.
- **SC-009**: Negative and low margins are identifiable at a glance without opening individual line detail.
- **SC-010**: First-time operators can understand the workflow sequence from the home page without external documentation.

## Assumptions

- Primary users are Microsoft 365 resellers and MSP operators manually reconciling Giacom supplier billing against Stripe subscriptions.
- V1 ingestion is manual file upload only (PDF and CSV); Stripe API integration and automated ingestion are out of scope.
- Stripe write operations remain export-only; operators apply approved changes manually or via a future API integration phase.
- Authentication and authorization for operator-facing pages follow existing application security patterns already enforced on API endpoints.
- **Scope boundary**: The Application layer (domain/business logic) is frozen — no new Application-layer features or functionality. Adding or extending API-layer HTTP endpoints that expose *existing* Application-layer capabilities IS in scope, as is all Web/UI work.
- API endpoints added by this feature are thin adapters over existing Application-layer services; they may orchestrate and expose but must not implement new domain rules.
- Responsive layout and accessibility follow established component patterns already adopted in partial UI implementations.
- PDF and Stripe CSV upload UIs are in scope because the underlying ingestion logic already exists in the Application layer; this feature adds the missing API endpoints to expose them.

## Application-Layer Capability Notes (Out of Scope — Frozen)

The API-endpoint gaps identified earlier (PDF upload, Stripe CSV upload, reconciliation orchestration, exception exposure, and approval-ingest convenience) are now **in scope** as API-layer work (see the API Enablement functional requirements), because the underlying Application-layer capabilities already exist.

The following remaining gaps would require **new Application-layer features or functionality** and are therefore **out of scope and not planned** for this feature. They are documented for awareness only.

| MVP expectation | Current Application-layer state | Why out of scope | UI impact |
|-----------------|--------------------------------|------------------|-----------|
| Product mapping persistence & CRUD | Mappings are supplied inline to services; there is **no Application-layer mapping store or CRUD domain logic** | Adding a persistent mapping store is new Application-layer functionality, not a thin API adapter | Mapping UI is limited to viewing/supplying mappings inline or via existing run-history input blobs; a full managed mapping catalogue is deferred |
| Stripe catalogue data available to catalogue reconciliation without inline payload | Stripe ingestion parser exists and can persist blobs, but catalogue reconciliation still expects Stripe products/prices supplied inline or from ingestion blobs; wiring beyond thin persistence may need Application changes | If linking persisted Stripe blobs into catalogue reconciliation requires new domain wiring rather than a thin adapter, it is deferred | Catalogue reconciliation may still require inline Stripe data unless the existing blob path fully satisfies it |

**Note on FR-011 (approval ingest convenience)**: If exposing a simpler proposal-ingest path requires only orchestration in the API layer (assembling the existing `ApprovalIngestionRequest` from a persisted run), it is in scope. If it requires new Application-layer logic, the UI falls back to the existing full-payload ingest contract and the simplification is deferred.

## Dependencies

- Existing Application-layer services (frozen): Giacom PDF ingester, Stripe CSV ingester, reconciliation engine, exception-surfacing service, classification, catalogue reconciliation, approval workflow, run history.
- Existing backend APIs: subscription management import, retail pricing import, classification, approval workflow, catalogue reconciliation, run history.
- New API endpoints (in scope): PDF ingestion, Stripe CSV ingestion, reconciliation orchestration, and exception/proposal exposure as needed — all as thin adapters over the frozen Application layer.
- Partial UI implementations in `BillDrift.Web`: approval queue pages, run history pages, Fluent UI layout, API clients for approval and run history.
- Prior feature specifications (001–012) defining domain model, ingestion formats, reconciliation rules, exception categories, approval contracts, and catalogue reconciliation behavior — UI and new endpoints must align with terminology and workflows defined there.
