# Feature Specification: Stripe Billing CSV Ingestion

**Feature Branch**: `003-stripe-csv-ingestion`

**Created**: 2026-07-02

**Status**: Draft

**Input**: User description: "Design the ingestion process for Stripe billing data used for reconciliation. Stripe is the source of truth for customer billing. Inputs (manual MVP): subscriptions.csv export (All Columns), products.csv export, prices.csv export. Extract subscription items, catalogue products and prices, normalise to domain entities, filter active subscriptions by default with optional inactive inclusion, parse mapping metadata, output clean collections ready for mapping and reconciliation."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Import Customer Billing State from Stripe Subscriptions Export (Priority: P1)

As a billing operator, I need to submit a Stripe subscriptions CSV export and receive a complete collection of subscription billing rows (one per subscription item) so that I can reconcile customer billing against supplier costs and Microsoft subscription truth without manual spreadsheet manipulation.

**Why this priority**: Stripe is the source of truth for customer billing. Without reliable subscription ingestion, reconciliation cannot compare billed quantities, prices, and intervals against other domains.

**Independent Test**: Given a representative subscriptions CSV fixture with multiple customers, multi-item subscriptions, and mapping metadata, the pipeline produces one output row per subscription item with customer identity, subscription identifiers, product and price references, quantity, billing interval, unit amount, status, and parsed mapping metadata.

**Acceptance Scenarios**:

1. **Given** a valid subscriptions CSV export containing multiple subscription items under one subscription, **When** the operator submits it for ingestion, **Then** the system emits one billing row per subscription item with shared subscription and customer identifiers.
2. **Given** a subscription item row with product name, product ID, and price ID columns populated, **When** ingestion completes, **Then** each output row carries all three catalogue references without loss or substitution.
3. **Given** a subscriptions export with quantity, billing interval, unit amount, and subscription status, **When** ingestion completes, **Then** each output row includes typed quantity, interval, amount, and status values suitable for downstream comparison.

---

### User Story 2 - Import Stripe Product and Price Catalogue (Priority: P1)

As a billing operator, I need to submit Stripe products and prices CSV exports alongside subscriptions so that reconciliation can resolve catalogue entries, validate price IDs on subscription items, and detect missing or mismatched catalogue data.

**Why this priority**: Subscription rows reference product and price IDs. Catalogue ingestion is required to interpret billed amounts, intervals, and product names during mapping and mismatch detection.

**Independent Test**: Given products and prices CSV fixtures, ingestion produces separate product and price collections with identifiers, names, amounts, currency, intervals, descriptions, and metadata fields preserved from export.

**Acceptance Scenarios**:

1. **Given** a products CSV export, **When** ingestion runs, **Then** each product record includes product ID, product name, and all metadata fields from the export without discarding unknown keys.
2. **Given** a prices CSV export, **When** ingestion runs, **Then** each price record includes price ID, linked product ID, unit amount, currency, billing interval, and description as exported.
3. **Given** subscription items referencing product and price IDs present in the catalogue exports, **When** all three files are ingested together, **Then** every referenced product ID and price ID can be resolved from the catalogue output collections.

---

### User Story 3 - Filter Subscriptions by Status for Reconciliation (Priority: P2)

As a billing operator, I need active subscriptions included by default and the ability to include inactive or cancelled subscriptions for diagnostics so that routine reconciliation focuses on billable state without losing visibility into historical or lapsed subscriptions when needed.

**Why this priority**: Reconciliation against current billing should not be polluted by cancelled subscriptions, but operators still need to investigate drift on recently ended or inactive accounts.

**Independent Test**: Given a fixture with mixed subscription statuses, default ingestion returns only active-status rows; when the operator opts in to include inactive statuses, all subscription items are returned with status preserved.

**Acceptance Scenarios**:

1. **Given** a subscriptions CSV containing both active and canceled subscriptions, **When** ingestion runs with default settings, **Then** only subscription items whose parent subscription is in an active billable status are included in the output.
2. **Given** the same fixture, **When** the operator enables inclusion of inactive subscriptions, **Then** canceled and other non-active subscription items are included with their status clearly preserved.
3. **Given** filtered-out inactive rows, **When** ingestion completes, **Then** a summary count indicates how many rows were excluded by status filter without treating exclusion as an error.

---

### User Story 4 - Parse and Flag Mapping Metadata (Priority: P2)

As a reconciliation operator, I need Stripe metadata fields (Mex ID, offer ID, SKU ID, supplier references) extracted and normalised on each subscription item so that cross-domain matching can proceed and missing or inconsistent metadata is surfaced before reconciliation runs.

**Why this priority**: Mapping metadata links Stripe billing to Giacom customer and Microsoft product identifiers. Silent omission causes false "missing in Stripe" or mapping failures downstream.

**Independent Test**: Given subscription items with full, partial, and absent mapping metadata, ingestion produces normalised metadata on each row and structured warnings for missing required correlation fields without blocking import of valid rows.

**Acceptance Scenarios**:

1. **Given** a subscription item with Mex ID, offer ID, and SKU ID in metadata, **When** ingestion completes, **Then** each identifier is extracted into normalised mapping fields on the output row.
2. **Given** a subscription item with supplier reference metadata, **When** ingestion completes, **Then** all supplier reference values are captured as a list without dropping unknown reference keys.
3. **Given** a subscription item missing offer ID or SKU ID, **When** ingestion completes, **Then** the row is still emitted, metadata gaps are recorded in the ingestion log, and no identifiers are invented.
4. **Given** metadata values with inconsistent casing or surrounding whitespace, **When** ingestion completes, **Then** identifiers are normalised to a consistent form while the original raw values remain traceable.

---

### User Story 5 - Tolerate CSV Format Variation and Partial Row Failures (Priority: P2)

As a billing operator, I need ingestion to tolerate minor column changes in monthly Stripe exports and continue when individual rows are malformed, with clear logging, so that a few bad rows do not block import of hundreds of valid subscription items.

**Why this priority**: Stripe CSV exports evolve and manual exports may contain edge-case rows. Partial success with visibility matches the Giacom ingestion pattern and preserves operator trust.

**Independent Test**: Given a fixture with one unparseable row among many valid rows and a variant export with renamed optional columns, ingestion returns all valid rows plus structured skip/warning entries; the import is marked partially successful when applicable.

**Acceptance Scenarios**:

1. **Given** a subscriptions row with unparseable quantity or amount, **When** ingestion runs, **Then** that row is skipped, a log entry describes the failure reason and row location, and all other valid rows are included.
2. **Given** an export where optional columns are reordered or renamed in a backward-compatible way, **When** ingestion runs, **Then** mandatory fields are still extracted using header detection rather than fixed column positions.
3. **Given** a row referencing a product or price ID absent from the catalogue files supplied in the same run, **When** ingestion completes, **Then** the subscription row is still emitted and a warning log entry flags the unresolved catalogue reference.

---

### Edge Cases

- Subscriptions CSV contains duplicate subscription item IDs — each export row is processed independently; deduplication is deferred to downstream normalization using stable source keys.
- One subscription spans many items (multi-product bundle) — each item becomes its own output row linked to the same subscription and customer.
- Product name on subscription row differs from product name in products CSV — both values are preserved; name reconciliation is deferred to canonical product mapping.
- Metadata present on subscription row but not on linked product — item-level metadata takes precedence for mapping; product-level metadata supplements catalogue context only.
- Currency or amount formatted with locale-specific separators — amounts are parsed into consistent numeric form or skipped with log entry when unparseable.
- Empty subscriptions file or file with headers only — import completes with empty collections and informational summary, not treated as hard failure.
- Prices CSV references product IDs not present in products CSV — price records are still emitted; unresolved product links are logged as warnings.
- Subscription status is `trialing` or `past_due` — included in default active filter (still billable or recoverable); `canceled` and `incomplete_expired` excluded unless inactive inclusion is enabled.
- All three CSV files not supplied in one run — subscriptions can be ingested alone; catalogue warnings apply when product/price lookups cannot be resolved until catalogue files are provided.

## Requirements *(mandatory)*

### Functional Requirements

#### Input and Intake

- **FR-001**: System MUST accept three manual CSV inputs for this MVP: subscriptions export (all columns), products export, and prices export.
- **FR-002**: System MUST treat Stripe as the authoritative source for customer billing state ingested through this pipeline.
- **FR-003**: System MUST assign each submitted file a stable source file reference that links every extracted record to its origin export.
- **FR-004**: System MUST record ingestion timestamp alongside every output record for audit and replay.
- **FR-005**: System MUST support ingesting subscriptions CSV independently when catalogue files are not yet available, deferring catalogue cross-checks to when products and prices are supplied.

#### Subscriptions Extraction

- **FR-006**: System MUST extract, per subscriptions export row representing a subscription item: customer identifier, subscription identifier, subscription item identifier, product identifier, price identifier, and product name as shown on the export row.
- **FR-007**: System MUST extract quantity, billing interval, unit amount, and subscription status from each subscription item row where present in the export.
- **FR-008**: System MUST preserve the full metadata dictionary from each subscription item row without discarding unrecognised keys.
- **FR-009**: System MUST handle multi-product subscriptions by emitting one output billing row per subscription item row, not one row per subscription header alone.
- **FR-010**: System MUST tolerate minor CSV format variation (column order changes, optional column additions) by matching required fields via header names rather than fixed column indexes.

#### Catalogue Extraction

- **FR-011**: System MUST extract, per products export row: product identifier, product name, and all metadata fields from the export.
- **FR-012**: System MUST extract, per prices export row: price identifier, linked product identifier, unit amount, currency, billing interval, and description.
- **FR-013**: System MUST preserve product and price metadata fields without dropping unknown keys.
- **FR-014**: System MUST retain catalogue product names exactly as exported without mapping to supplier or Offer/SKU naming during ingestion.

#### Normalisation

- **FR-015**: System MUST convert successfully parsed CSV rows into structured billing entities aligned with the billing domain normalization contract: one flattened row per subscription item for customer billing state, plus separate product and price catalogue records.
- **FR-016**: System MUST parse numeric fields (quantity, amounts) and interval enumerations from raw export text into typed values during normalisation, skipping rows only when mandatory numeric fields are unparseable.
- **FR-017**: System MUST normalise customer and mapping identifiers (Mex ID, offer ID, SKU ID) by trimming whitespace and applying consistent casing rules while retaining original raw values for traceability.
- **FR-018**: System MUST attach stable source references to every normalised output record sufficient for idempotent re-import of the same export snapshot.

#### Filtering

- **FR-019**: System MUST include only subscription items whose parent subscription status is in the configured active status set by default; default active statuses MUST include `active`, `trialing`, and `past_due`.
- **FR-020**: System MUST allow the operator to include inactive subscription statuses (including at minimum `canceled`, `unpaid`, `incomplete`, and `incomplete_expired`) for diagnostic ingestion runs.
- **FR-021**: System MUST report counts of included and excluded subscription items when status filtering is applied, without treating excluded inactive rows as errors.

#### Metadata Handling

- **FR-022**: System MUST parse known mapping metadata keys from subscription item metadata into structured fields: Mex ID, offer ID, SKU ID, and supplier reference identifiers.
- **FR-023**: System MUST retain any additional metadata key-value pairs not mapped to typed fields in a supplementary metadata collection on each subscription billing row.
- **FR-024**: System MUST identify and log missing mapping metadata (Mex ID, offer ID, or SKU ID absent when expected for reconciliation) without blocking emission of the subscription billing row.
- **FR-025**: System MUST identify inconsistent metadata (e.g., offer ID present but SKU ID absent on the same item, or Mex ID format failing validation) and record structured warning log entries.
- **FR-026**: System MUST NOT invent Offer ID, SKU ID, Mex ID, or supplier references when absent from the export metadata.

#### Output and Handoff

- **FR-027**: System MUST produce three clean output collections ready for downstream product mapping and reconciliation: subscription billing items (one per subscription item), products, and prices.
- **FR-028**: Each subscription billing output row MUST be joinable to catalogue output by product ID and price ID.
- **FR-029**: System MUST NOT perform Offer/SKU mapping, supplier name resolution, or reconciliation during ingestion; those steps are explicitly downstream.
- **FR-030**: System MUST bundle output collections with ingestion summary (record counts, filter exclusions, warning and skip counts) and structured log entries.

#### Error Handling and Logging

- **FR-031**: System MUST continue ingestion when individual CSV rows fail validation, skipping only affected rows.
- **FR-032**: System MUST fail a file-level import only when mandatory headers are missing or no parseable data rows exist in a required file.
- **FR-033**: System MUST emit structured log entries for every skipped row or metadata warning, including: source file reference, row index or identifier, reason code, and raw field snippet where available.
- **FR-034**: System MUST summarize ingestion outcome per file and overall as: success, partial success, or failure.
- **FR-035**: System MUST NOT silently discard export data; unparseable fields MUST either preserve raw text on the output object or appear in the skip/warning log.

#### Data Flow (Design)

- **FR-036**: Ingestion MUST follow a sequential pipeline: **Intake → Header Detection → Row Parsing → Catalogue Assembly → Subscription Item Assembly → Metadata Normalisation → Status Filtering → Validation → Output Assembly → Logging**.
- **FR-037**: Each pipeline stage MUST pass forward sufficient context (source file reference, row index, raw field values) so failures can be traced to source location.
- **FR-038**: Parsed raw field values MUST be retained through normalisation so downstream mapping and reconciliation can audit extraction decisions.

### Key Entities *(include if feature involves data)*

- **Stripe Export Bundle**: The set of CSV files submitted in one ingestion run (subscriptions, products, prices); attributes include per-file source references, ingestion timestamp, and overall outcome status.
- **Parsed Subscription Row**: Raw subscription item data from CSV before normalisation; attributes include customer, subscription, and item identifiers, product and price references, quantity, interval, amount, status, raw metadata dictionary, and row location.
- **Stripe Subscription Billing Item (ingestion output)**: Flattened customer billing row representing one billable subscription item; attributes include customer identity, subscription and item identifiers, product and price references, quantity, billing interval, unit amount, subscription status, normalised mapping metadata, source reference, and link to raw export values.
- **Stripe Product (ingestion output)**: Catalogue product record; attributes include product identifier, product name, metadata fields, and source reference.
- **Stripe Price (ingestion output)**: Catalogue price record; attributes include price identifier, product identifier, unit amount, currency, billing interval, description, and source reference.
- **Ingestion Log Entry**: Record of a skipped row, metadata warning, or unresolved catalogue reference; attributes include severity, location, reason code, raw snippet, and parent file reference.
- **Stripe Ingestion Result**: Aggregate outcome bundling the three output collections, log entries, filter statistics, and summary counts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operators can ingest a typical Stripe export bundle (1,000+ subscription items, 200+ products, 400+ prices) and receive structured output in under 1 minute without manual data entry.
- **SC-002**: For a curated set of representative CSV fixtures (minimum: 2 subscription exports with multi-item subscriptions, 2 product exports, 2 price exports including column-order variants), at least 99% of valid data rows are extracted with all mandatory fields (customer, subscription item, product ID, price ID, quantity, status).
- **SC-003**: When a fixture contains deliberately malformed rows (minimum 1% of rows), 100% of valid rows are still imported and 100% of skipped rows appear in the ingestion log with identifiable location and reason.
- **SC-004**: Re-importing the same export files produces identical output collections and source keys, enabling deterministic downstream normalization and reconciliation.
- **SC-005**: Default status filtering excludes 100% of canceled subscription items from output while retaining 100% of active, trialing, and past_due items in test fixtures.
- **SC-006**: Operators can identify every subscription item with missing Mex ID, offer ID, or SKU ID from ingestion log summaries without opening raw CSV files.
- **SC-007**: 100% of subscription items in test fixtures whose product and price IDs exist in supplied catalogue files successfully resolve to catalogue records in the combined output.

## Assumptions

- CSV files are provided manually by the operator (upload or file path); automated Stripe API sync is out of scope for this MVP and covered by a future feature.
- Exports use Stripe's standard CSV export format with header row; encoding is UTF-8 unless otherwise detected and reported.
- One export bundle represents a point-in-time snapshot of Stripe billing and catalogue state; cross-snapshot diffing is handled downstream.
- The billing domain model feature defines target normalization contracts (`StripeBillingItem`, catalogue entities, mapping metadata structure); this feature specifies the ingestion pipeline design feeding those contracts.
- Product naming differences between Stripe, Giacom supplier PDFs, and Offer/SKU labels are expected; ingestion preserves Stripe names and metadata without resolving naming variants.
- Currency is single-currency per export bundle for typical UK reseller operations; multi-currency handling preserves currency code per price row without conversion.
- Mapping metadata key names follow established Stripe metadata conventions used by the operator (Mex ID, offer ID, SKU ID, supplier reference keys); exact key aliases are discovered during planning from sample exports.
- Reconciliation, mismatch detection, canonical product mapping, and Stripe write actions are performed by separate pipelines — not during CSV ingestion.

## Dependencies

- **001-billing-domain-model**: Defines Stripe billing entities, mapping metadata structure, customer identity, idempotency keys, and normalization contract that this pipeline feeds.
- **002-giacom-pdf-ingestion**: Establishes ingestion patterns (partial success, structured logging, source fidelity) that this feature mirrors for the Stripe CSV source.
- Representative Stripe CSV fixtures (sanitized production samples or synthetic equivalents) are required before implementation to validate header detection, metadata parsing, and regression coverage.

## Scope Boundaries

**In scope**:

- Manual CSV ingestion for subscriptions, products, and prices exports
- Extraction and normalisation of subscription items, catalogue products, and prices
- Default active-subscription filtering with optional inactive inclusion
- Mapping metadata parsing, normalisation, and gap/inconsistency logging
- Output collections ready for product mapping and reconciliation
- Tolerance for minor CSV format variation and partial row failures

**Out of scope**:

- Stripe API integration or scheduled automatic export fetch
- Offer/SKU resolution, canonical product mapping, or CSP classification
- Reconciliation, mismatch detection, or corrective Stripe actions
- Giacom PDF, subscription management report, or price list ingestion
- UI design (covered by a future feature)
- Persistent storage and export file lifecycle (covered by infrastructure feature)
- Automated correction of missing metadata on Stripe records
