# Giacom Block Grammar Contract

**Feature**: `002-giacom-pdf-ingestion`  
**Scope**: Parsing rules for customer block segmentation, column detection, and line field extraction from Giacom pre-billing and post-billing PDFs.

## Document Structure

Giacom supplier billing PDFs follow a repeating pattern:

```text
[Document header — report type, reseller, billing period summary]
[Optional column headers — Product | Qty | Type | Period | Cost | Ref ...]

[Customer Block 1]
  Customer: {Customer Name}     Mex ID: {MEX#####}
  {product line rows...}

[Customer Block 2]
  ...
```

Pre-billing and post-billing share this block structure; report type differs in document header text only.

## Customer Block Header Grammar

A **new customer block starts** when a `PdfTextLine` matches any of:

| Pattern | Example |
|---------|---------|
| `Mex ID` label + value | `Mex ID: MEX12345` |
| `MEX ID` label + value | `MEX ID MEX12345` |
| `Sub Account` label + value | `Sub Account: MEX12345` |
| Standalone Mex token in header zone | `MEX12345` with customer name on same or preceding line |

**Mex ID token pattern** (extraction):

```regex
MEX\d+
```

Case-insensitive match; stored as extracted (preserving matched casing).

**Customer name extraction**:
- Text on same line before Mex ID label, OR
- Text on immediately preceding non-empty line within same block start zone, OR
- Text after `Customer:` / `Customer Name:` label on same line

If Mex ID found but customer name empty → **block skip** (`CustomerNameMissing` or `MexIdMissing` if inverse).

## Product Line Row Grammar

A row is a **product line candidate** when it falls inside a customer block and is not a block header row.

### Complete line

Contains values in at least **two** of: product name zone, quantity zone, line cost zone.

| Field | Column header aliases | Parse rule |
|-------|----------------------|------------|
| Product name | `Product`, `Description`, `Product Name` | Text in product column zone |
| Quantity | `Qty`, `Quantity`, `Licences` | Integer or decimal text |
| Charge type | `Type`, `Charge Type`, `Charge` | Text; map variants below |
| Period start | `Period From`, `Start`, `From` | Date text or half of range |
| Period end | `Period To`, `End`, `To` | Date text or half of range |
| Line cost | `Cost`, `Amount`, `Line Total`, `Net` | Currency decimal |
| References | `Ref`, `Reference`, `Order Ref`, `Sub Ref` | One or more alphanumeric tokens |

**Period range**: If single column contains `dd/MM/yyyy - dd/MM/yyyy` or `dd-MM-yyyy to dd-MM-yyyy`, split into start/end.

### Continuation row (wrapped product name)

A row is a **continuation** when:
- Product name zone has text, AND
- Quantity, charge type, and cost zones are all empty, AND
- Previous row is a product line (complete or itself a continuation)

**Action**: Append product name text to previous line's `ProductNameRaw` with single space.

### Non-line rows (ignore)

- Repeated column headers within body
- Page footers (`Page X of Y`)
- Subtotal/total summary rows without per-product quantity
- Blank lines

## Charge Type Normalization (Raw Text)

Stored in `ChargeTypeRaw` as extracted. Recognized variants:

| PDF text (case-insensitive) | Stored raw | Notes |
|----------------------------|------------|-------|
| `Recurring` | `Recurring` | Default if column empty |
| `Pro-rated adjustment` | `Pro-rated adjustment` | |
| `Pro rated adjustment` | `Pro rated adjustment` | Preserve as written |
| `Prorated` | `Prorated` | |
| `Adjustment` | `Adjustment` | Ambiguous — preserved raw |

Full enum mapping to `ChargeType` happens in normalizer, not ingestion.

## Column Detection Algorithm

1. Scan first 3 pages for header row containing ≥3 known column aliases.
2. For each header word, record its X center → map to logical column name.
3. Build `ColumnDefinition` ranges: midpoint boundaries between adjacent header X centers.
4. Extend outer columns ±20pt to tolerate drift.
5. If header not found on a page, reuse column definitions from previous page.

## Block and Line Indexing

| Index | Scope | Purpose |
|-------|-------|---------|
| `PageNumber` | 1-based | Location in log entries |
| `BlockIndex` | 0-based, document order | Stable block reference |
| `LineIndex` | 0-based within block | Positional fallback line key |

Indices assigned during single forward pass; deterministic for same PDF bytes.

## Skip vs Emit Decision Matrix

| Condition | Action | Log reason |
|-----------|--------|------------|
| Quantity missing/unparseable | Skip line | `QuantityUnparseable` |
| Line cost missing/unparseable | Skip line | `LineCostUnparseable` |
| Period unparseable | **Emit line** | `PeriodUnparseable` (warning) |
| Mex ID missing in block header | Skip block | `MexIdMissing` |
| Customer name missing in block header | Skip block | `CustomerNameMissing` |
| Row structure ambiguous | Skip line | `AmbiguousLineStructure` |
| No blocks in document | Fail document | `NoCustomerBlocksFound` |

## Format Variant Tolerance

The grammar intentionally avoids absolute coordinates. Tolerance mechanisms:

- Y-cluster tolerance for line grouping (±2pt default)
- Column boundary extension (±20pt outer columns)
- Case-insensitive header alias matching
- Multiple Mex ID label variants

When Giacom introduces a new column alias, add to alias table — no structural parser rewrite required.

## Fixture Calibration

Each golden PDF fixture MAY include a sidecar `*.columns.json` during development if header detection fails:

```json
{
  "productName": { "minX": 40, "maxX": 280 },
  "quantity": { "minX": 285, "maxX": 330 }
}
```

Sidecar overrides are **test-only**; production parser uses header detection. Sidecars document expected geometry for regression debugging.

## Related Artifacts

- [pdf-ingestion-pipeline.md](./pdf-ingestion-pipeline.md) — orchestration contract
- [research.md](../research.md) — R3–R5 decisions
- [spec.md](../spec.md) — FR-005–FR-012 requirements
