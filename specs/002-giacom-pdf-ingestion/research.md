# Research: Giacom Supplier Billing PDF Ingestion

**Feature**: `002-giacom-pdf-ingestion`  
**Date**: 2026-07-01

## R1: PDF Text Extraction Library

**Decision**: Use **UglyToad.PdfPig** for text extraction with word-level bounding boxes.

**Rationale**:
- MIT license — compatible with open-source BillDrift constitution.
- Exposes `Letter`/`Word` positions (X, Y, width, height) needed for column-aware parsing of semi-structured tables without OCR.
- Pure .NET, no native dependencies — runs in Aspire containers and CI.
- Active community; standard choice for .NET PDF text extraction.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| iText7 / iTextSharp | AGPL/commercial licensing friction for OSS project |
| DocNET (PDFium wrapper) | Lower-level; still requires custom line/column logic; adds native binary dependency |
| Azure Document Intelligence | Cloud cost + latency; overkill for recurring known-format PDFs; offline dev harder |
| pdf2json / external CLI | Cross-process parsing breaks determinism testing; deployment complexity |

## R2: Source Document Identity

**Decision**: `SourceDocumentId` = lowercase hex **SHA-256 hash of raw PDF bytes**.

**Rationale**:
- Identical file re-upload produces identical document ID (SC-004).
- Independent of filename or upload path.
- Supports idempotent re-import per `RawImportId` contract.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Filename only | Same content under different names breaks idempotency |
| Upload GUID | New ID every upload even for same bytes |
| Billing period from header | Period text may be missing or ambiguous; not stable per file |

## R3: Line and Column Detection Strategy

**Decision**: Two-phase **positional parsing**:
1. **Line grouping**: Cluster words by Y coordinate within tolerance (±2pt default, configurable per fixture calibration).
2. **Column assignment**: Detect column X-ranges from header row labels on each page (or first page template), then assign field tokens by horizontal position.

**Rationale**:
- Giacom PDFs are table-like but not machine-readable tables; positional clustering handles column drift better than regex-on-flat-text.
- Header-anchored columns adapt to minor layout shifts without monthly template edits (FR-012).

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Flat text + regex only | Breaks when column order/spacing shifts; multi-line names impossible to reconstruct |
| Fixed coordinate templates per month | Violates FR-012; high maintenance |
| PDF table extraction (Tabula-style) | Giacom PDFs lack explicit table borders; ruled lines inconsistent |

## R4: Customer Block Segmentation

**Decision**: Segment blocks when a line matches **customer header pattern**:
- Contains Mex ID label (`MEX ID`, `Mex ID`, `Sub Account`, etc.) followed by identifier token, OR
- Matches standalone Mex ID pattern (`MEX\d+`, case-insensitive) in expected header zone.

Each block inherits customer name from adjacent header text (line above or same line before Mex ID label).

**Rationale**:
- Matches spec entity model (Customer Block → product lines).
- Mex ID is primary correlation key across BillDrift domains.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Page-per-customer | Multiple customers per page observed in reseller PDFs |
| Font-size heuristics only | Unreliable when Giacom changes styles |

## R5: Multi-Line Product Name Merge

**Decision**: Treat a visual row as **continuation of previous product line** when:
- Row has text in product-name column zone only, AND
- Quantity, charge type, and cost columns are empty or absent, AND
- Previous row started a product line (had quantity or cost).

Merge by concatenating product name text with single space separator.

**Rationale**:
- Directly addresses User Story 2 and FR-011.
- Avoids emitting orphan partial lines.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Always one row = one line | Truncates wrapped product names (fails SC-005) |
| Merge all consecutive text-only rows globally | Could swallow unrelated header/footer text |

## R6: Line-Level Idempotency Key (`SourceLineKey`)

**Decision**: Prefer **first non-empty supplier reference ID** from reference columns; fallback to `{page}:{blockIndex}:{lineIndex}` positional key.

**Rationale**:
- Aligns with 001 normalization contract: supplier reference from PDF when present.
- Positional fallback guarantees key stability for identical PDF bytes (deterministic indices).

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Positional only | Duplicate references on distinct lines would collide incorrectly if used alone |
| Hash of line content | Content hash changes if whitespace normalization differs |

## R7: Error Handling Tiers

**Decision**: Three-tier model matching spec FR-018–FR-020:

| Tier | Trigger | Action |
|------|---------|--------|
| Line skip | Unparseable quantity or line cost on otherwise identified product row | Skip line, log warning, continue |
| Block skip | Missing customer name OR Mex ID in block header | Skip all lines in block, log error |
| Document fail | Zero customer blocks detected OR PDF unreadable/encrypted | Fail import, empty line collection |

Period parse failure → emit line with null period + warning (FR-017), not skip.

**Rationale**: Maximizes usable data while preventing orphan lines without customer context.

## R8: Report Type Classification

**Decision**: Classify via **first-page text markers**:
- Pre-billing: contains `Pre-Billing`, `Pre Billing`, or `Estimate` (case-insensitive)
- Post-billing: contains `Post-Billing`, `Post Billing`, `Invoice`, or `Tax Invoice`
- Unknown: `GiacomReportType.Unknown` — still parse lines; flag in result metadata

**Rationale**: Pre/post share line structure; classification is metadata for operator filtering (SC-006).

## R9: Test Strategy

**Decision**:
- **Golden-file tests**: PDF fixture → JSON array of expected `RawGiacomBillingLine` fields.
- **Minimum fixtures**: 2 pre-billing + 2 post-billing including at least one format variant pair.
- **Synthetic bad-line fixture**: embedded corrupt row for partial-success validation (SC-003).
- **Determinism test**: parse same PDF twice, deep-equal outputs.

**Rationale**: Constitution II mandates regression fixtures per format variant; golden files catch subtle extraction drift.

## R10: Mex ID Normalization at Extraction

**Decision**: Store **raw Mex ID as extracted** in `MexIdRaw`; apply trim-only normalization when mapping. Casing preserved in raw field.

**Rationale**:
- Domain raw import preserves source fidelity (FR-030, 001 FR-002).
- `MexId.Create` in normalizer applies validation rules.

## Open Items (resolved — no NEEDS CLARIFICATION)

All technical context items resolved above. No blocking unknowns remain for Phase 1 design.
