# Feature Specification: Giacom Supplier Billing PDF Ingestion

**Feature Branch**: `002-giacom-pdf-ingestion`

**Created**: 2026-07-01

**Status**: Draft

**Input**: User description: "Design the ingestion pipeline for supplier billing PDFs (Giacom pre-billing and post-billing reports). Extract structured supplier cost billing lines from semi-structured PDFs, transform to structured objects, handle partial extraction with logging, and output lines ready for Offer/SKU mapping. PDFs are emailed monthly; format may change slightly; not all products are Microsoft CSP."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Import Monthly Supplier Billing PDF (Priority: P1)

As a billing operator, I need to submit a Giacom pre-billing or post-billing PDF and receive a complete collection of supplier cost lines so that I can reconcile supplier charges against subscription truth and Stripe billing without manual spreadsheet entry.

**Why this priority**: Supplier cost data is one of four reconciliation domains. Without reliable PDF ingestion, drift analysis cannot begin.

**Independent Test**: Given a representative pre-billing or post-billing PDF fixture, the pipeline produces structured supplier cost lines with customer, product, quantity, charge type, billing period, line cost, and supplier reference identifiers for every parseable line in the document.

**Acceptance Scenarios**:

1. **Given** a valid Giacom pre-billing PDF for a billing period, **When** the operator submits it for ingestion, **Then** the system extracts all customer blocks and product lines and returns a collection of supplier cost lines with source fidelity preserved (names and identifiers as written on the PDF).
2. **Given** a valid Giacom post-billing PDF for the same period, **When** the operator submits it for ingestion, **Then** the system applies the same extraction pipeline with report-type detection and produces comparable supplier cost line output.
3. **Given** a PDF containing multiple customers, **When** ingestion completes, **Then** each line is associated with the correct customer name and sub-account identifier (Mex ID) from its customer block.

---

### User Story 2 - Handle Format Variation and Multi-Line Product Entries (Priority: P1)

As a billing operator, I need the ingestion pipeline to tolerate minor formatting differences across monthly PDFs and correctly assemble product lines that span multiple visual rows so that I do not lose charges when Giacom changes layout slightly or wraps long product names.

**Why this priority**: PDF format drift is expected monthly. Fragile parsing would force manual rework and undermine reconciliation trust.

**Independent Test**: Given two PDF fixtures with slightly different layout (column spacing, header wording, line wrapping), the pipeline extracts the same logical line count and field values for equivalent charges.

**Acceptance Scenarios**:

1. **Given** a PDF where product names wrap across two visual rows, **When** ingestion runs, **Then** the product name is captured as a single concatenated value matching the full name as intended on the invoice.
2. **Given** a PDF with slightly different column alignment or header labels compared to a prior month, **When** ingestion runs, **Then** the pipeline still identifies customer blocks and product line boundaries without operator intervention.
3. **Given** a PDF containing both recurring and pro-rated adjustment lines for the same product and period, **When** ingestion runs, **Then** each line retains its distinct charge type and is not merged or double-counted.

---

### User Story 3 - Recover from Partial Extraction Failures (Priority: P2)

As a billing operator, I need ingestion to continue when individual lines or blocks are malformed, with clear logging of what was skipped, so that a few bad rows do not block import of hundreds of valid charges.

**Why this priority**: Real-world PDFs contain edge cases. Partial success with visibility is preferable to all-or-nothing failure.

**Independent Test**: Given a PDF fixture with one unparseable line among many valid lines, ingestion returns all valid lines plus a structured log of skipped items; the import is marked partially successful, not failed.

**Acceptance Scenarios**:

1. **Given** a product line missing a parseable quantity, **When** ingestion runs, **Then** that line is skipped, a log entry describes the failure location and reason, and all other valid lines are included in the output.
2. **Given** a customer block where the Mex ID cannot be extracted, **When** ingestion runs, **Then** all lines in that block are skipped with a block-level log entry, and lines from other customer blocks are still imported.
3. **Given** an entirely unreadable PDF (no customer blocks detected), **When** ingestion runs, **Then** the import fails with a summary error and no supplier cost lines are emitted.

---

### User Story 4 - Prepare Lines for Downstream Product Mapping (Priority: P2)

As a reconciliation developer, I need ingested supplier cost lines normalized enough for identifier correlation but with product names preserved exactly as written so that downstream Offer/SKU mapping can resolve products using subscription truth, price lists, and canonical mapping tables.

**Why this priority**: Ingestion ends at structured supplier cost lines; mapping is a separate concern. Output must hand off cleanly without premature product resolution.

**Independent Test**: Given ingested lines from a PDF containing both Microsoft CSP and non-CSP products, output includes all lines with normalized Mex ID and preserved raw product names; no Offer ID or SKU ID is invented when absent from the PDF.

**Acceptance Scenarios**:

1. **Given** a supplier line with product name "Microsoft 365 Business Premium (NCE)", **When** transformation completes, **Then** the product name field contains the full string as written and no canonical product key is assigned at ingestion time.
2. **Given** a Mex ID extracted with inconsistent casing or whitespace, **When** transformation completes, **Then** the sub-account identifier is normalized to a consistent form while the original raw value remains traceable.
3. **Given** ingested lines from one billing period, **When** passed to downstream mapping, **Then** each line includes stable source identifiers (document reference, line key) sufficient for idempotent re-import.

---

### Edge Cases

- PDF contains zero product lines (cover sheet or summary only) — import completes with empty line collection and informational log, not treated as error.
- Duplicate supplier reference identifiers appear on separate lines — each line is emitted independently; deduplication is deferred to normalization using idempotency keys.
- Negative or credit amounts on pro-rated adjustment lines — line cost is captured as signed value; charge type distinguishes from recurring charges.
- Customer name appears once per block with many product lines — all lines inherit the block's customer name and Mex ID.
- Same Mex ID appears under different customer name spellings across blocks — each block preserved as extracted; customer identity reconciliation is downstream.
- Non-Microsoft products (e.g., third-party add-ons) appear alongside CSP products — all lines extracted equally; CSP classification is not applied during ingestion.
- Pre-billing PDF re-imported after post-billing PDF for same period — both imports retained with document type and source document reference; operator chooses which snapshot to reconcile.
- PDF password-protected or corrupted — import fails at document level with clear operator message before line extraction begins.

## Requirements *(mandatory)*

### Functional Requirements

#### Document Intake and Classification

- **FR-001**: System MUST accept Giacom supplier billing PDF documents as the sole input format for this ingestion pipeline.
- **FR-002**: System MUST classify each submitted document as pre-billing or post-billing report type based on document metadata (title, header labels, or structural markers) before line extraction.
- **FR-003**: System MUST assign each submitted document a stable source document reference that persists across re-imports and links every extracted line to its origin file.
- **FR-004**: System MUST record ingestion timestamp and report type alongside every extracted line for audit and replay.

#### Parsing Strategy

- **FR-005**: System MUST extract text and positional structure from PDF pages to identify repeating customer blocks (customer name header followed by one or more product lines).
- **FR-006**: System MUST extract, per customer block: customer name and sub-account identifier (Mex ID).
- **FR-007**: System MUST extract, per product line within a customer block: product name, quantity, charge type, and line cost.
- **FR-008**: System MUST extract billing period (start and end) for each product line where present on the PDF.
- **FR-009**: System MUST extract all supplier reference identifiers present on each line (e.g., order reference, subscription reference, line reference) without discarding unknown reference columns.
- **FR-010**: System MUST recognize charge type values including at minimum "Recurring" and "Pro-rated adjustment" (and common spelling variants) and preserve the raw text when classification is ambiguous.
- **FR-011**: System MUST merge visually wrapped product name rows into a single product name value before emitting the line.
- **FR-012**: System MUST tolerate minor formatting variation across monthly PDFs (column drift, header wording changes, whitespace differences) without requiring manual template updates for each month.

#### Transformation

- **FR-013**: System MUST convert each successfully parsed PDF row into a structured supplier cost line object aligned with the domain raw import contract (customer/sub-account, product name as written, quantity, charge type, billing period, line cost, supplier references).
- **FR-014**: System MUST normalize sub-account identifiers (Mex ID) by trimming whitespace and applying consistent casing rules while retaining the original extracted value for traceability.
- **FR-015**: System MUST preserve product names exactly as written on the PDF without truncation, abbreviation, or mapping to Offer/SKU at ingestion time.
- **FR-016**: System MUST parse quantity and line cost from raw text into typed numeric values during transformation, deferring only when raw text is unparseable (triggering skip behavior per FR-019).
- **FR-017**: System MUST parse billing period boundaries from raw date text when format is recognized; when unparseable, emit the line with null period fields and a warning log entry rather than skip the line (unless quantity or cost also fail).

#### Error Handling and Logging

- **FR-018**: System MUST continue ingestion when individual product lines fail validation, skipping only the affected lines.
- **FR-019**: System MUST skip all lines in a customer block when the block header (customer name or Mex ID) cannot be extracted, logging a block-level failure.
- **FR-020**: System MUST fail the entire import when no customer blocks can be identified in the document.
- **FR-021**: System MUST emit structured log entries for every skipped line or block, including: document reference, approximate location (page, block index, line index), failure reason, and raw text snippet where available.
- **FR-022**: System MUST summarize ingestion outcome as: success (all lines extracted), partial success (some lines skipped), or failure (no lines extracted).
- **FR-023**: System MUST NOT silently discard extracted data; any field that cannot be parsed MUST either preserve raw text on the output object or appear in the skip log.

#### Output and Handoff

- **FR-024**: System MUST produce a collection of supplier cost lines ready for downstream normalization and Offer/SKU mapping via separate data sources (subscription management report, price list, canonical product mapping).
- **FR-025**: Each output line MUST carry a stable line-level source key composed of document reference and supplier reference identifier(s), or a positional fallback when references are absent.
- **FR-026**: System MUST NOT assign Offer ID, SKU ID, or CSP classification during ingestion; those attributes are explicitly out of scope for this pipeline.
- **FR-027**: System MUST include both pre-billing and post-billing lines in the same output structure, distinguished by report type metadata.

#### Data Flow (Design)

- **FR-028**: Ingestion MUST follow a sequential pipeline: **Intake → Document Classification → Page Extraction → Block Segmentation → Line Parsing → Transformation → Validation → Output Assembly → Logging**.
- **FR-029**: Each pipeline stage MUST pass forward sufficient context (document reference, block context, raw field text) so that failures can be traced to source location.
- **FR-030**: Parsed raw field values MUST be retained through transformation so downstream normalization can audit extraction decisions.

### Key Entities *(include if feature involves data)*

- **Supplier Billing Document**: A single Giacom pre-billing or post-billing PDF submitted for ingestion; attributes include source document reference, report type (pre/post), billing period context if stated in headers, ingestion timestamp, and overall ingestion outcome.
- **Customer Block**: A contiguous section of the PDF representing one customer; attributes include customer name (as written), Mex ID (sub-account identifier), block sequence index, and parent document reference.
- **Parsed Product Line**: An individual charge row within a customer block before transformation; attributes include raw product name text, raw quantity text, raw charge type text, raw period text, raw cost text, raw supplier reference texts, and positional metadata.
- **Supplier Cost Line (ingestion output)**: Structured line ready for domain raw import; attributes include normalized Mex ID, product name as written, parsed quantity, charge type, billing period start/end, line cost amount, supplier reference identifiers, source document reference, report type, stable line key, and link to raw extracted values.
- **Ingestion Log Entry**: Record of a skipped or warned item; attributes include severity (error/warning), location, reason code, raw snippet, and parent document reference.
- **Ingestion Result**: Aggregate outcome bundling the line collection, log entries, summary counts (extracted, skipped, warned), and overall status.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operators can ingest a typical monthly Giacom billing PDF (500+ lines across 50+ customers) and receive structured output in under 2 minutes without manual data entry.
- **SC-002**: For a curated set of representative PDF fixtures (minimum: 2 pre-billing, 2 post-billing, including format variants), at least 98% of visually present charge lines are extracted with all mandatory fields (customer, Mex ID, product name, quantity, charge type, line cost).
- **SC-003**: When a fixture contains deliberately malformed lines (minimum 1% of lines), 100% of valid lines are still imported and 100% of skipped lines appear in the ingestion log with identifiable location and reason.
- **SC-004**: Re-importing the same PDF produces an identical line collection and line keys, enabling deterministic downstream normalization.
- **SC-005**: Product names match PDF text character-for-character in 100% of successfully extracted lines (no truncation or normalization of product names).
- **SC-006**: Operators can distinguish pre-billing from post-billing output and identify which document each line originated from without cross-referencing external systems.

## Assumptions

- PDFs are provided to the pipeline as files (upload or stored blob); automated email pickup is out of scope for this feature — operators retrieve monthly emailed PDFs and submit them manually or via a separate delivery mechanism.
- Currency is single-currency per document (GBP for UK Giacom resellers); multi-currency PDFs are out of scope for v1.
- One PDF corresponds to one billing period snapshot; cross-PDF aggregation is handled downstream.
- The domain raw import type (`RawGiacomBillingLine`) and normalized type (`SupplierCostLine`) defined in the billing domain model feature are the target output contracts; this feature specifies the extraction pipeline design, not the domain types themselves.
- Offer/SKU mapping, CSP classification, and reconciliation are performed by separate pipelines using subscription management reports, price lists, and canonical product mapping — not during PDF ingestion.
- Password-protected PDFs are not supported; operators must provide unencrypted files.
- "Supplier cost billing lines represent supplier charges, not authoritative subscription truth" — ingestion treats PDF content as supplier-reported data without validating against Microsoft or Stripe.

## Dependencies

- **001-billing-domain-model**: Defines `RawGiacomBillingLine`, `SupplierCostLine`, idempotency key structure, and normalization contract that this pipeline feeds.
- Representative PDF fixtures from production (or sanitized samples) are required before implementation to validate parsing strategy and regression coverage.

## Scope Boundaries

**In scope**:

- Parsing strategy for semi-structured Giacom pre-billing and post-billing PDFs
- Customer block and multi-line product entry extraction
- Transformation to structured supplier cost line objects
- Partial extraction tolerance with structured logging
- Output collection ready for downstream mapping

**Out of scope**:

- Email automation or scheduled fetch of monthly PDFs
- Offer/SKU resolution or product mapping
- Normalization to domain entities (handled by application layer normalizer)
- Reconciliation, mismatch detection, or Stripe actions
- Subscription Management report or price list ingestion
- UI design (covered by a future feature)
- Persistent storage and blob lifecycle (covered by infrastructure feature)
