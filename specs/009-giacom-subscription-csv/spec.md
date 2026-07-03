# Feature Specification: Giacom Subscription Management CSV Ingestion

**Feature Branch**: `009-giacom-subscription-csv`

**Created**: 2026-07-03

**Status**: Draft

**Input**: User description: "Design ingestion for the Giacom Subscription Management report (Microsoft 365 only). Input: SubscriptionManagementReport.csv. Extract customer, product, subscription, pricing, and lifecycle fields. Normalise Offer ID + SKU ID as primary product keys and Mex ID as primary customer key. Output subscription truth records for reconciliation against Stripe. Scope limited to Microsoft 365 / CSP-style products; tolerate blank columns and varying product names."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Import Microsoft Subscription Truth from Giacom Report (Priority: P1)

As a billing operator, I need to submit a Giacom Subscription Management report CSV and receive a complete collection of Microsoft subscription truth records so that I can reconcile what Giacom says each customer is licensed for against Stripe billing without maintaining a separate spreadsheet of active subscriptions.

**Why this priority**: Subscription truth is one of four reconciliation domains. Without reliable ingestion of the Subscription Management report, quantity and product matching against Stripe cannot reflect the supplier's authoritative subscription state.

**Independent Test**: Given a representative `SubscriptionManagementReport.csv` fixture containing multiple customers and Microsoft 365 products, the pipeline produces one subscription truth record per qualifying row with customer identity, commercial product keys, licence count, term, billing frequency, renewal date, status, and supplier subscription reference.

**Acceptance Scenarios**:

1. **Given** a valid Subscription Management report CSV for the current export period, **When** the operator submits it for ingestion, **Then** the system extracts all qualifying Microsoft 365 / CSP rows and returns subscription truth records with source fidelity preserved (names and identifiers as written in the export).
2. **Given** a report containing multiple customers, **When** ingestion completes, **Then** each record is associated with the correct customer name, Mex ID, and tenant ID (when present) from its source row.
3. **Given** a row with offer ID, SKU ID, licence count, term, billing frequency, renewal date, and subscription status, **When** ingestion completes, **Then** each value is captured on the output record in a form suitable for downstream comparison with Stripe subscription items.

---

### User Story 2 - Scope Ingestion to Microsoft 365 CSP Products Only (Priority: P1)

As a billing operator, I need non-Microsoft and non-CSP products (such as Exclaimer or other add-on services) excluded from subscription truth output so that reconciliation focuses on Microsoft 365 subscriptions that map to Stripe billing and does not produce false gaps for out-of-scope products.

**Why this priority**: The report mixes product families. Including out-of-scope rows would inflate mismatch counts and distract operators from billable Microsoft 365 drift.

**Independent Test**: Given a fixture containing both Microsoft 365 rows and Exclaimer (or other non-CSP) rows, ingestion emits only Microsoft 365 / CSP-style rows and logs a summary count of excluded rows with reason.

**Acceptance Scenarios**:

1. **Given** a row whose service or product type indicates a non-Microsoft or non-CSP product, **When** ingestion runs, **Then** that row is excluded from subscription truth output and recorded in the exclusion summary.
2. **Given** a row clearly identifiable as Microsoft 365 or CSP (via service, product, or product type columns), **When** ingestion runs, **Then** the row is included in subscription truth output.
3. **Given** a row with ambiguous product classification (e.g., blank service column but Microsoft product name), **When** ingestion runs, **Then** the row is included when product naming patterns indicate Microsoft 365, or excluded with a logged warning when classification cannot be determined confidently.

---

### User Story 3 - Normalise Customer and Product Identity for Cross-Domain Matching (Priority: P1)

As a reconciliation operator, I need customer identity anchored on Mex ID and product identity anchored on offer ID plus SKU ID so that subscription truth records align with Stripe metadata and supplier billing using the same correlation keys used elsewhere in BillDrift.

**Why this priority**: Reconciliation matches entities by Mex ID and commercial product keys. Inconsistent normalisation at ingestion causes false mapping failures and duplicate customer identities.

**Independent Test**: Given rows with Mex ID, offer ID, and SKU ID in mixed casing and with surrounding whitespace, ingestion produces normalised customer and product keys while retaining original raw values for traceability.

**Acceptance Scenarios**:

1. **Given** a row with Mex ID in inconsistent casing or with surrounding whitespace, **When** transformation completes, **Then** the customer identity uses a normalised Mex ID as the primary key while the original value remains traceable.
2. **Given** a row with offer ID and SKU ID populated, **When** transformation completes, **Then** the commercial product key is derived from normalised offer ID and SKU ID without inventing values when either is absent.
3. **Given** a row where customer name spelling differs from other rows sharing the same Mex ID, **When** transformation completes, **Then** customer identity is keyed by Mex ID and the display name from that row is preserved without merging or overwriting names from other rows at ingestion time.

---

### User Story 4 - Capture Extended Subscription Lifecycle and Pricing Fields (Priority: P2)

As a billing operator, I need optional report columns—NCE and trial flags, end-of-term action, cancellable-until date, migration-to-NCE indicator, assigned licence count, and price/ERP columns when present—captured on subscription truth records so that lifecycle and pricing drift can be investigated during reconciliation without re-opening the source CSV.

**Why this priority**: These fields explain why quantities or prices differ between Giacom truth and Stripe (trials, pending cancellations, NCE migrations, partial assignment). Omitting them forces operators back to manual CSV review.

**Independent Test**: Given a fixture with all optional columns populated on some rows and blank on others, ingestion captures present values on output records and represents absent columns as empty without failing the row.

**Acceptance Scenarios**:

1. **Given** a row with NCE flag, trial flag, end-of-term action, and cancellable-until date populated, **When** ingestion completes, **Then** each value is present on the subscription truth record.
2. **Given** a row with price and ERP columns populated, **When** ingestion completes, **Then** monetary values are captured in a consistent numeric form suitable for comparison, or the row is skipped with a log entry when a price value is present but unparseable.
3. **Given** a row with migration-to-NCE and assigned-licence columns blank, **When** ingestion completes, **Then** the record is still emitted with those fields absent and no default values invented.

---

### User Story 5 - Tolerate Format Variation and Partial Row Failures (Priority: P2)

As a billing operator, I need ingestion to tolerate minor column changes across monthly exports and continue when individual rows are malformed, with clear logging, so that a few bad rows do not block import of hundreds of valid subscriptions.

**Why this priority**: Giacom exports evolve and real reports contain edge-case rows. Partial success with visibility matches established ingestion patterns for Giacom PDF and Stripe CSV sources.

**Independent Test**: Given a fixture with one unparseable row among many valid rows and a variant export with reordered optional columns, ingestion returns all valid rows plus structured skip/warning entries; the import is marked partially successful when applicable.

**Acceptance Scenarios**:

1. **Given** a row with unparseable licence count, **When** ingestion runs, **Then** that row is skipped, a log entry describes the failure reason and row location, and all other valid rows are included.
2. **Given** an export where optional columns are reordered or use synonymous header labels, **When** ingestion runs, **Then** mandatory fields are still extracted using header detection rather than fixed column positions.
3. **Given** a row missing offer ID or SKU ID, **When** ingestion runs, **Then** the row is still emitted when other mandatory fields are present, a warning log entry flags the missing commercial key, and no identifiers are invented.
4. **Given** a row missing Mex ID, **When** ingestion runs, **Then** that row is skipped with a row-level failure log because customer identity cannot be established.

---

### Edge Cases

- Report contains zero qualifying Microsoft 365 rows (empty or all non-CSP products) — import completes with empty subscription truth collection and informational summary, not treated as error.
- Duplicate supplier subscription IDs on separate rows — each row is emitted independently; deduplication is deferred to downstream processing using stable source keys.
- Same Mex ID appears under different customer name spellings — each row preserved as extracted; customer name reconciliation is downstream.
- Product name varies for the same offer ID + SKU ID pair — product name preserved as written per row; commercial key normalisation uses offer ID and SKU ID only.
- Licence count is zero on a non-cancelled row — row is emitted with zero licences and a warning log entry for operator review.
- Renewal date blank on an active subscription — row is emitted with absent renewal date; date reconciliation deferred to mismatch detection.
- Trial or NCE flag columns use varied representations (yes/no, true/false, Y/N) — flags are normalised to a consistent boolean or enumerated representation on output.
- Assigned licence count differs from total licence count — both values are preserved when present; quantity reconciliation rules apply downstream.
- Entirely unreadable file (missing expected headers or not a CSV) — import fails with a summary error and no subscription truth records are emitted.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST accept `SubscriptionManagementReport.csv` as the sole input file for this ingestion source in the initial release.
- **FR-002**: System MUST detect and map report columns by header name, tolerating minor header label variation and column reordering for both mandatory and optional fields.
- **FR-003**: System MUST extract, per qualifying row, customer name, Mex ID, and tenant ID (when present in the export).
- **FR-004**: System MUST extract, per qualifying row, service, product name, and product type as written in the export.
- **FR-005**: System MUST extract, per qualifying row, offer ID and SKU ID as the primary commercial product identifiers.
- **FR-006**: System MUST extract, per qualifying row, NCE flag and trial flag when those columns are present.
- **FR-007**: System MUST extract, per qualifying row, licence count and subscription status.
- **FR-008**: System MUST extract, per qualifying row, supplier subscription ID (subscription reference from Giacom).
- **FR-009**: System MUST extract, per qualifying row, contract term and billing frequency.
- **FR-010**: System MUST extract, per qualifying row, renewal date and end-of-term action when those columns are present.
- **FR-011**: System MUST extract price and ERP column values when those columns are present in the export.
- **FR-012**: System MUST extract cancellable-until date, migration-to-NCE indicator, and assigned licence count when those columns are present.
- **FR-013**: System MUST restrict subscription truth output to Microsoft 365 and CSP-style products, excluding non-Microsoft and non-CSP products such as Exclaimer from output.
- **FR-014**: System MUST log excluded out-of-scope rows with reason and row location without treating exclusion as an ingestion error.
- **FR-015**: System MUST normalise Mex ID as the primary customer identity key by trimming whitespace and applying consistent casing rules while retaining the original extracted value for traceability.
- **FR-016**: System MUST normalise offer ID and SKU ID as the primary commercial product key pair by trimming whitespace and applying consistent casing rules while retaining original raw values for traceability.
- **FR-017**: System MUST NOT invent Mex ID, offer ID, SKU ID, or supplier subscription references when absent from the export.
- **FR-018**: System MUST emit one subscription truth record per successfully processed qualifying row, linked to a stable source row identifier for idempotent re-import.
- **FR-019**: System MUST preserve product and customer names exactly as written in the export without premature canonical name resolution.
- **FR-020**: System MUST skip individual rows that lack a parseable Mex ID or unparseable mandatory numeric fields (licence count when present and non-empty), logging row-level failure reason and location.
- **FR-021**: System MUST continue processing remaining rows when individual rows fail, marking the import partially successful when at least one row succeeds and at least one row is skipped.
- **FR-022**: System MUST produce structured ingestion logs summarising total rows read, rows emitted, rows excluded by product scope, rows skipped by validation, and warnings for missing commercial keys or optional field gaps.
- **FR-023**: System MUST represent blank optional columns as absent on output records without substituting default values.
- **FR-024**: System MUST output subscription truth records in a form ready for downstream reconciliation against Stripe subscription billing (customer identity, commercial keys, quantity, term, frequency, status, supplier reference, and lifecycle fields).
- **FR-025**: System MUST fail the entire import when the file cannot be recognised as a Subscription Management report (e.g., mandatory headers missing).

### Key Entities *(include if feature involves data)*

- **Subscription Management Report Row (raw)**: A single line from `SubscriptionManagementReport.csv` preserving all extracted column values as written, plus source document reference and row number for traceability and idempotent re-import.
- **Customer Identity (normalised)**: The correlation anchor for a customer across reconciliation domains; primary key is Mex ID, with optional display name and tenant ID carried from the source row.
- **Commercial Product Key (normalised)**: The correlation anchor for a product across reconciliation domains; composed of offer ID and SKU ID as the primary key pair, with service, product name, and product type preserved from the source for display and classification.
- **Subscription Truth Record (output)**: The normalised representation of one Microsoft 365 / CSP subscription from Giacom; attributes include customer identity, commercial product key, licence count, assigned licence count (when present), term, billing frequency, renewal date, subscription status, supplier subscription ID, NCE and trial flags, end-of-term action, cancellable-until date, migration-to-NCE indicator, price and ERP values (when present), link to source row, and preserved raw field values for audit.
- **Ingestion Run Summary**: Aggregate outcome of one import attempt; includes counts of rows read, emitted, scope-excluded, and skipped, plus structured warning and error entries.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operators can import a representative Subscription Management report and receive subscription truth records for at least 98% of visually present Microsoft 365 rows with all mandatory fields (Mex ID, offer ID, SKU ID, licence count, status) correctly captured.
- **SC-002**: For a curated fixture set (minimum: 2 monthly export variants, 1 mixed-product report with non-CSP rows, 1 report with optional lifecycle columns), 100% of non-CSP product rows are excluded from output with logged exclusion reasons.
- **SC-003**: Operators can complete a full report import of up to 5,000 qualifying rows in under 2 minutes on standard operator hardware without manual intervention.
- **SC-004**: Re-importing the same file produces identical subscription truth record sets (stable source keys and field values), enabling deterministic downstream reconciliation.
- **SC-005**: Operators can identify every row with missing offer ID or SKU ID from ingestion log summaries without opening the raw CSV.
- **SC-006**: At least 95% of rows with populated optional lifecycle columns (NCE flag, trial flag, end-of-term action, cancellable-until) have those values present on output records in a reviewable form.

## Assumptions

- The report filename `SubscriptionManagementReport.csv` is the standard export name used by the operator; alternative filenames may be accepted if file content matches expected structure.
- Microsoft 365 / CSP scope is determined from service, product, and product type columns using rules established during planning; Exclaimer and similar add-ons are explicitly out of scope.
- Subscription truth represents Giacom's view of active and historical Microsoft subscriptions in the export; status filtering for reconciliation runs (e.g., active only) is a downstream concern unless otherwise configured at import time.
- Monetary values in price and ERP columns use GBP and UK date formats consistent with other Giacom exports unless the export explicitly indicates otherwise.
- Header label synonyms (e.g., "Licences" vs "Licenses", "MEX ID" vs "Mex ID") are discoverable from sample exports during planning.
- Normalisation to domain entities follows the same customer and commercial-key conventions established for Stripe CSV and Giacom PDF ingestion (Mex ID primary, offer ID + SKU ID primary for products).
- Downstream reconciliation consumes subscription truth records alongside Stripe billing items, supplier cost lines, and intended prices per the billing domain model (feature 001).
- This feature covers ingestion and normalisation only; canonical product mapping, mismatch detection, and Stripe corrective actions remain separate features.
