# Feature Specification: Retail Pricing and Pricing Strategy Ingestion

**Feature Branch**: `010-retail-pricing-ingestion`

**Created**: 2026-07-03

**Status**: Draft

**Input**: User description: "Design ingestion for retail pricing and pricing strategy rules. Inputs: ResellerPricingVsRRP.csv and manual price overrides for products not present in the list. Extract pricing by Offer ID + SKU ID, Term, Frequency, wholesale and RRP, margin and margin %, product status, and Platform (NCE/Legacy). Default pricing strategy charges RRP from the list; items missing from the list support manual RRP entries classified as Non-CSP / bespoke. Output normalised pricing reference objects suitable for validating Stripe catalogue prices and margin calculations against supplier costs."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Import Intended Retail Pricing from Giacom Price List (Priority: P1)

As a billing operator, I need to submit the Giacom `ResellerPricingVsRRP.csv` price list and receive a complete collection of intended retail pricing records so that reconciliation can compare what customers should be charged (RRP) against Stripe subscription prices and catalogue entries without maintaining a separate pricing spreadsheet.

**Why this priority**: Intended retail pricing is one of four reconciliation domains. Without reliable ingestion of the wholesale price list, price mismatches and catalogue gaps cannot be detected and margin analysis lacks a retail reference.

**Independent Test**: Given a representative `ResellerPricingVsRRP.csv` fixture containing multiple offer/SKU combinations across terms and billing frequencies, the pipeline produces one intended pricing record per qualifying row with commercial keys, wholesale cost, RRP, margin fields, product status, and platform classification.

**Acceptance Scenarios**:

1. **Given** a valid `ResellerPricingVsRRP.csv` for the current export period, **When** the operator submits it for ingestion, **Then** the system extracts all qualifying rows and returns intended pricing records with source fidelity preserved (identifiers and amounts as written in the export).
2. **Given** a row with offer ID, SKU ID, term, billing frequency, wholesale price, RRP, margin, margin percentage, and product status, **When** ingestion completes, **Then** each value is captured on the output record in a form suitable for downstream comparison with Stripe prices and supplier cost lines.
3. **Given** a row with a platform indicator (NCE or Legacy), **When** ingestion completes, **Then** the platform classification is present on the output record.

---

### User Story 2 - Apply Default Pricing Strategy (Charge Catalogue RRP) (Priority: P1)

As a billing operator, I need catalogue-sourced prices to establish RRP as the default intended retail charge for each commercial product key so that reconciliation treats the price list as the authoritative retail reference unless a manual override exists.

**Why this priority**: The pricing strategy defines what "correct" customer pricing means during reconciliation. Without an explicit default-to-RRP rule, price mismatch detection would lack a consistent expected value.

**Independent Test**: Given a fixture with catalogue rows only (no manual overrides), each output record's effective intended retail price equals the catalogue RRP for that commercial key, and the price source is marked as catalogue-sourced.

**Acceptance Scenarios**:

1. **Given** a price list row with a parseable RRP and no manual override for the same commercial key, **When** pricing resolution completes, **Then** the effective intended retail price equals the catalogue RRP.
2. **Given** multiple catalogue rows sharing the same offer ID and SKU ID but different term or billing frequency, **When** pricing resolution completes, **Then** each distinct commercial key retains its own RRP without cross-contamination between term/frequency variants.
3. **Given** a catalogue row marked End of Sale, **When** pricing resolution completes, **Then** the RRP is still used as the intended retail price and the end-of-sale status is preserved for operator visibility.

---

### User Story 3 - Support Manual RRP Overrides for Products Missing from the Price List (Priority: P1)

As a billing operator, I need to enter manual RRP values for products that do not appear in `ResellerPricingVsRRP.csv` so that bespoke or non-catalogue offerings still have an intended retail price for Stripe validation and margin review.

**Why this priority**: Not every billable product appears in the standard Giacom price list. Without manual overrides, reconciliation would report false catalogue gaps and miss price drift for bespoke lines.

**Independent Test**: Given a manual override entry for a commercial key absent from the price list fixture, ingestion produces an intended pricing record sourced from the manual entry with the operator-provided RRP and classification as non-CSP / bespoke.

**Acceptance Scenarios**:

1. **Given** a manual RRP entry for offer ID, SKU ID, term, and billing frequency not present in the uploaded price list, **When** pricing ingestion completes, **Then** an intended pricing record is emitted with the manual RRP, marked as manually sourced, and classified as non-CSP / bespoke.
2. **Given** both a catalogue row and a manual override for the same commercial key within one ingestion run, **When** pricing resolution completes, **Then** the manual override RRP takes precedence as the effective intended retail price.
3. **Given** a manual override with a reason and effective date, **When** ingestion completes, **Then** those attributes are preserved on the output record for audit and pricing drift tracking.

---

### User Story 4 - Normalise Commercial Keys for Cross-Domain Matching (Priority: P1)

As a reconciliation operator, I need product identity anchored on offer ID plus SKU ID together with term and billing frequency so that intended pricing records align with Microsoft subscription truth, Stripe catalogue prices, and supplier cost lines using the same correlation keys used elsewhere in BillDrift.

**Why this priority**: Reconciliation matches prices by commercial key. Inconsistent normalisation at ingestion causes false mapping failures and duplicate pricing entries.

**Independent Test**: Given rows with offer ID, SKU ID, term, and frequency in mixed casing and with surrounding whitespace, ingestion produces normalised commercial keys while retaining original raw values for traceability.

**Acceptance Scenarios**:

1. **Given** a row with offer ID and SKU ID in inconsistent casing or with surrounding whitespace, **When** transformation completes, **Then** the commercial key uses normalised offer ID and SKU ID while the original values remain traceable.
2. **Given** term values such as "Monthly", "Annual", or "Triennial" and frequency values "Monthly" or "Annual" in varied casing, **When** transformation completes, **Then** term and billing frequency are mapped to consistent enumerated values on the commercial key.
3. **Given** a row where offer ID or SKU ID is absent, **When** ingestion runs, **Then** the row is skipped with a logged warning and no commercial key is invented.

---

### User Story 5 - Enable Stripe Catalogue Validation and Margin Analysis (Priority: P2)

As a billing operator, I need intended pricing records to carry wholesale cost, RRP, margin amount, and margin percentage so that reconciliation can validate Stripe catalogue unit amounts against intended RRP and compute margin against supplier cost billing.

**Why this priority**: Price mismatch detection compares Stripe amounts to intended RRP; margin analysis compares wholesale to supplier PDF costs. Omitting margin fields forces operators to re-open the source file.

**Independent Test**: Given a fixture with wholesale, RRP, margin, and margin percentage populated, output records include all monetary fields in a consistent numeric form; given a fixture where margin fields are blank but wholesale and RRP are present, margin may be derived or left absent without failing the row.

**Acceptance Scenarios**:

1. **Given** a row with wholesale and RRP populated, **When** ingestion completes, **Then** both amounts are present in a consistent monetary form suitable for comparison with supplier cost lines and Stripe unit prices.
2. **Given** a row with margin and margin percentage populated in the source, **When** ingestion completes, **Then** both values are captured on the output record.
3. **Given** a row where margin fields are blank but wholesale and RRP are present, **When** ingestion completes, **Then** the record is still emitted and margin fields remain absent rather than inventing values.

---

### User Story 6 - Tolerate Format Variation and Partial Row Failures (Priority: P2)

As a billing operator, I need ingestion to tolerate minor column changes across monthly price list exports and continue when individual rows are malformed, with clear logging, so that a few bad rows do not block import of hundreds of valid prices.

**Why this priority**: Giacom price list exports evolve and real files contain edge-case rows. Partial success with visibility matches established ingestion patterns for other BillDrift sources.

**Independent Test**: Given a fixture with one unparseable monetary field among many valid rows and a variant export with reordered optional columns, ingestion returns all valid rows plus structured skip/warning entries; the import is marked partially successful when applicable.

**Acceptance Scenarios**:

1. **Given** a row with an unparseable wholesale or RRP value, **When** ingestion runs, **Then** that row is skipped, a log entry describes the failure reason and row location, and all other valid rows are included.
2. **Given** an export where optional columns (margin, platform, status) are reordered or use synonymous header labels, **When** ingestion runs, **Then** mandatory fields are still extracted using header detection rather than fixed column positions.
3. **Given** an entirely unreadable file (missing expected headers or not a CSV), **When** ingestion runs, **Then** the import fails with a summary error and no intended pricing records are emitted.

---

### Edge Cases

- Price list contains zero qualifying rows — import completes with an empty intended pricing collection and informational summary, not treated as error.
- Duplicate commercial keys within the same catalogue file — each row is processed independently; when duplicates exist, the last successfully processed row for that key wins for catalogue sourcing, with a warning logged for the duplicate.
- Manual override and catalogue row for the same commercial key — manual override always wins for effective intended retail price regardless of processing order.
- Product status is End of Sale but subscriptions remain active — RRP is still the intended retail price; status is flagged for operator review during reconciliation.
- Platform column absent or blank — record is emitted with platform classified as unknown; reconciliation proceeds without blocking.
- Term is Triennial but billing frequency is Annual — both dimensions are preserved independently on the commercial key as exported.
- Wholesale present but RRP blank — row is skipped with a logged failure because intended retail price cannot be established from catalogue data.
- Manual override for a product with only partial commercial key dimensions (e.g., missing SKU ID) — entry is rejected with a validation message listing required fields.
- Currency not stated in source — amounts are interpreted using the operator's configured default billing currency (assumed GBP for UK resellers).
- Margin percentage and absolute margin disagree with wholesale/RRP — source values are preserved as written; reconciliation may flag inconsistency downstream rather than correcting at ingestion.

## Requirements *(mandatory)*

### Functional Requirements

#### Input Sources

- **FR-001**: System MUST accept `ResellerPricingVsRRP.csv` as the primary catalogue input for intended retail pricing ingestion.
- **FR-002**: System MUST accept manual price override entries as a supplementary input for products absent from the catalogue file.
- **FR-003**: System MUST support combining catalogue and manual override inputs within a single ingestion run.

#### Catalogue Extraction

- **FR-004**: System MUST detect and map price list columns by header name, tolerating minor header label variation and column reordering for both mandatory and optional fields.
- **FR-005**: System MUST extract, per qualifying catalogue row, offer ID and SKU ID as the primary commercial product identifiers.
- **FR-006**: System MUST extract, per qualifying catalogue row, contract term (Monthly, Annual, Triennial) and billing frequency (Monthly, Annual).
- **FR-007**: System MUST extract, per qualifying catalogue row, wholesale (cost) price and recommended retail price (RRP).
- **FR-008**: System MUST extract, per qualifying catalogue row, margin amount and margin percentage when those columns are present.
- **FR-009**: System MUST extract, per qualifying catalogue row, product status (Active, EndOfSale, and other statuses as provided by source) and normalise to a consistent enumerated representation.
- **FR-010**: System MUST extract, per qualifying catalogue row, platform classification (NCE, Legacy) when a platform column is present.

#### Manual Override Handling

- **FR-011**: System MUST allow operators to submit manual RRP entries for commercial keys not present in the catalogue file.
- **FR-012**: Each manual override MUST include, at minimum: intended RRP, contract term, billing frequency, and either offer ID or SKU ID (both required when available from operator knowledge).
- **FR-013**: Each manual override MUST capture an entry reason and effective date for audit traceability.
- **FR-014**: Manual override entries MUST be classified as non-CSP / bespoke product pricing.
- **FR-015**: When both a catalogue row and a manual override exist for the same commercial key, the manual override MUST take precedence as the effective intended retail price.

#### Pricing Strategy

- **FR-016**: The default pricing strategy MUST use catalogue RRP as the intended retail charge for each commercial key sourced from the price list.
- **FR-017**: For commercial keys with no catalogue row, the effective intended retail price MUST come solely from a manual override; catalogue gaps without overrides MUST NOT invent prices.
- **FR-018**: End-of-sale catalogue entries MUST retain their RRP as the intended retail price; status MUST be surfaced for operator review without suppressing the price record.

#### Normalisation and Output

- **FR-019**: System MUST normalise offer ID and SKU ID by trimming whitespace and applying consistent casing rules while retaining original raw values for traceability.
- **FR-020**: System MUST normalise term and billing frequency to consistent enumerated values on the commercial key.
- **FR-021**: System MUST NOT invent offer ID, SKU ID, term, frequency, or monetary amounts when absent or unparseable in the source.
- **FR-022**: System MUST emit one intended pricing record per successfully processed catalogue row or manual override, linked to a stable source identifier for idempotent re-import.
- **FR-023**: System MUST mark each output record with its price source (catalogue or manual override).
- **FR-024**: System MUST output intended pricing records in a form ready for downstream reconciliation: validating Stripe catalogue unit amounts against intended RRP, comparing Stripe subscription prices to intended RRP, and supporting margin calculations against supplier cost lines using wholesale amounts.

#### Resilience and Observability

- **FR-025**: System MUST skip individual rows with unparseable mandatory monetary fields (wholesale or RRP for catalogue rows; RRP for manual overrides) or missing required commercial key components, logging row-level failure reason and location.
- **FR-026**: System MUST continue processing remaining rows when individual rows fail, marking the import partially successful when at least one row succeeds and at least one row is skipped.
- **FR-027**: System MUST produce structured ingestion logs summarising total rows read, records emitted, rows skipped by validation, manual overrides applied, duplicate key warnings, and catalogue-vs-override resolution counts.
- **FR-028**: System MUST represent blank optional columns as absent on output records without substituting default values.
- **FR-029**: System MUST fail the entire import when the catalogue file cannot be recognised as a reseller pricing export (e.g., mandatory headers missing).

### Key Entities *(include if feature involves data)*

- **Price List Row (raw)**: A single line from `ResellerPricingVsRRP.csv` preserving all extracted column values as written, plus source document reference and row number for traceability and idempotent re-import.
- **Manual Price Override (raw)**: An operator-entered retail price for a product not in the catalogue; attributes include commercial key dimensions (offer ID, SKU ID, term, frequency), intended RRP, optional wholesale cost, entry reason, effective date, and entry timestamp.
- **Commercial Key (normalised)**: The correlation anchor for pricing across reconciliation domains; composed of normalised offer ID, SKU ID, contract term, and billing frequency.
- **Intended Pricing Record (output)**: The normalised retail pricing reference for one commercial key; attributes include commercial key, wholesale cost, intended RRP (effective retail charge), margin amount, margin percentage, product status, platform classification (NCE, Legacy, or unknown), price source (catalogue or manual override), product classification (CSP catalogue item or non-CSP / bespoke for manual overrides), link to source row or override entry, and preserved raw field values for audit.
- **Pricing Resolution Result**: The outcome of applying pricing strategy rules across catalogue and manual inputs for one ingestion run; attributes include total catalogue rows processed, manual overrides applied, override-wins count, catalogue-only count, and keys lacking any intended price.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operators can ingest a representative `ResellerPricingVsRRP.csv` (500+ rows) and receive intended pricing records for at least 99% of rows with valid mandatory fields in under 30 seconds.
- **SC-002**: 100% of manual override entries for commercial keys absent from the catalogue produce intended pricing records classified as non-CSP / bespoke with the operator-provided RRP as the effective intended retail price.
- **SC-003**: When catalogue and manual override conflict on the same commercial key, 100% of resolution outcomes use the manual override RRP in pricing regression fixtures.
- **SC-004**: Intended pricing records enable Stripe catalogue validation: in reconciliation regression fixtures, at least 95% of catalogue price mismatches are detected when Stripe unit amounts differ from intended RRP beyond tolerance.
- **SC-005**: Intended pricing records enable margin analysis: wholesale amounts from ingestion align with supplier cost comparison scenarios in at least 95% of matched commercial-key fixtures.
- **SC-006**: Partial-import scenarios (fixture with 5% malformed rows) complete with all valid rows emitted, structured skip logs for failures, and partial-success status — without aborting the entire import.

## Assumptions

- Target users are Microsoft 365 resellers using Giacom who bill customers via Stripe, consistent with the BillDrift product scope.
- Monetary amounts in `ResellerPricingVsRRP.csv` are in GBP unless a currency column is present in future export variants; the default billing currency is GBP for v1.
- Manual price overrides are submitted by authenticated billing operators through the same ingestion workflow as the catalogue file (structured entry or companion upload), not silently inferred from other data sources.
- Platform values in the price list map to NCE and Legacy; other or blank values are classified as unknown without blocking ingestion.
- Product classification for catalogue rows defaults to CSP unless the source explicitly indicates otherwise; manual overrides are always classified as non-CSP / bespoke per pricing strategy.
- Duplicate commercial keys within a single catalogue upload are rare; last-row-wins with warning is acceptable for v1; deduplication policy may be refined in a future amendment.
- This feature covers ingestion and pricing strategy resolution only; Stripe catalogue mutation, reconciliation mismatch surfacing, and UI workflows are downstream consumers defined in existing BillDrift features.
- Ingestion follows the same operator expectations as other BillDrift import sources: file validation, size limits, audit logging of ingestion runs, and immutable snapshots for reconciliation run history.
