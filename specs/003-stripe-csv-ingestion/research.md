# Research: Stripe Billing CSV Ingestion

**Feature**: `003-stripe-csv-ingestion`  
**Date**: 2026-07-02

## R1: CSV Parsing Library

**Decision**: Use **CsvHelper** (v30+) with `ClassMap` / dynamic header mapping for Stripe dashboard exports.

**Rationale**:
- MIT license — compatible with open-source BillDrift constitution.
- Header-name-based column binding supports FR-010 (column order drift) without fixed indexes.
- Handles quoted fields, embedded commas, and UTF-8 BOM — common in Stripe exports.
- Pure .NET, widely adopted; minimal learning curve for contributors.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Manual `StreamReader` + split | Fragile on quoted commas; reinvents CsvHelper |
| Sylvan.Data.Csv | Less ecosystem documentation; no clear advantage for simple dashboard exports |
| Excel/OpenXML | Stripe exports are CSV; adds unnecessary dependency |
| Stripe API instead of CSV | Out of MVP scope; CSV first per constitution v0.1 |

## R2: Source File Identity

**Decision**: `SourceDocumentId` = lowercase hex **SHA-256 hash of raw CSV file bytes** (one hash per file in the bundle).

**Rationale**:
- Identical file re-upload produces identical document ID (SC-004).
- Independent of filename or upload path.
- Consistent with Giacom PDF ingestion (002 R2).
- Supports per-file `RawImportId` via `ImportSourceKind.StripeExport`.

**Bundle identity**: `StripeCsvIngestionResult.BundleId` = SHA-256 of concatenated sorted file hashes (`subscriptions|products|prices`) for audit grouping; individual records still reference their source file hash.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Filename only | Same content under different names breaks idempotency |
| Upload GUID | New ID every upload even for same bytes |
| Stripe export timestamp column | Not present on all rows; unreliable |

## R3: Header Alias Registry

**Decision**: Central **`StripeCsvHeaderMap`** maps logical field names to an ordered list of accepted CSV header aliases (case-insensitive). Required fields fail file-level import when no alias matches; optional fields log warnings when absent.

**Rationale**:
- Stripe dashboard export column labels vary (`Customer ID` vs `customer_id`, `Subscription Item ID` vs `item_id`).
- Adding aliases does not require parser code changes (FR-010).
- Contract document lists canonical aliases for fixture authors.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Fixed column index | Breaks when Stripe adds/reorders columns |
| Regex on every header | Harder to test; opaque for operators |
| Separate parser per export version | High maintenance monthly |

## R4: Subscriptions Export Row Granularity

**Decision**: Treat **subscriptions.csv (All Columns)** as **one row per subscription item** when `Subscription Item ID` (or alias) is present; otherwise one row per subscription with implicit single item from subscription-level product/price columns.

**Rationale**:
- Matches spec FR-009 (multi-product subscriptions → multiple output rows).
- Stripe "All Columns" subscription exports commonly expand items as separate rows in reseller workflows.
- Fallback path handles single-item subscriptions without item ID column (legacy export shape).

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Subscription-only rows | Loses multi-item bundles (FR-009) |
| Require separate subscription_items.csv | Not in MVP input list (FR-001) |
| Explode items in normalizer | Ingestion must emit raw items faithfully per 001 contract |

## R5: Metadata Column Patterns

**Decision**: **`StripeMetadataParser`** collects metadata from two Stripe export patterns:
1. **Bracket columns**: `metadata[mex_id]`, `metadata[offer_id]`, etc.
2. **Flat metadata columns**: any header matching `metadata*` or known mapping keys (`mex_id`, `MexId`, `offer_id`, `OfferId`, `sku_id`, `SkuId`).

Parsed into `IReadOnlyDictionary<string, string>` on raw records; typed fields (`MexId`, `OfferId`, `SkuId`, supplier references) extracted per [001 normalization contract](../001-billing-domain-model/contracts/normalization.md):
- Canonical keys: `mex_id`, `offer_id`, `sku_id` (lowercase)
- Legacy fallback: `MexId`, `OfferId`, `SkuId` (PascalCase)
- Supplier references: keys matching `supplier_ref`, `supplier_reference`, `giacom_ref` (configurable prefix list)

**Rationale**:
- Stripe exports metadata both as expanded columns and nested key syntax.
- Normalization contract already defines key precedence.
- Missing metadata logged, not invented (FR-024, FR-026).

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| JSON blob column only | Not present on dashboard CSV exports |
| Require metadata on products not items | Item-level metadata is authoritative per spec edge case |

## R6: Active Subscription Status Filter

**Decision**: Default **active status set** = `{ active, trialing, past_due }`. Inactive set (included when `IncludeInactiveSubscriptions = true`) = `{ canceled, unpaid, incomplete, incomplete_expired, paused }`.

**Rationale**:
- Matches spec FR-019/FR-020 and edge cases (trialing/past_due billable or recoverable).
- Operator opt-in for diagnostics without polluting routine reconciliation.
- Excluded rows increment `Summary.SubscriptionsFilteredByStatus` — not errors (FR-021).

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Active only (`active`) | Excludes trialing customers common in MSP onboarding |
| No filtering at ingestion | Violates FR-019; pushes filter burden to every consumer |

## R7: Line Key and RawImportId

**Decision**:

```
SourceLineKey =
  IF subscription item row AND SubscriptionItemId non-empty → SubscriptionItemId
  ELSE IF product row AND ProductId non-empty → ProductId
  ELSE IF price row AND PriceId non-empty → PriceId
  ELSE → "{sourceDocumentId}:{rowNumber}"
```

`RawImportId` = `ImportSourceKind.StripeExport` + `SourceDocumentId` (file hash) + `SourceLineKey`.

**Rationale**:
- Stripe IDs are stable across re-export of unchanged objects.
- Row number fallback for malformed ID rows still yields deterministic keys within a file snapshot.
- Aligns with 001 normalization batch idempotency contract.

## R8: Shared Ingestion Diagnostics

**Decision**: Extend existing **`IngestionFailureReason`** enum (Application.Import) with Stripe CSV codes rather than a parallel enum:

| New reason | Use |
|------------|-----|
| `MandatoryHeaderMissing` | Required column alias not found |
| `AmountUnparseable` | Unit amount / price not numeric |
| `StripeIdMissing` | Expected `sub_`/`si_`/`prod_`/`price_` ID empty |
| `MetadataIncomplete` | Warning: Mex/Offer/SKU gap |
| `MetadataInconsistent` | Warning: partial key set |
| `CatalogueReferenceUnresolved` | Warning: product/price ID not in bundle catalogue |
| `EmptyFile` | Headers only or zero data rows |

Reuse `IngestionLogEntry`, `IngestionOutcomeStatus`, `IngestionLogSeverity` from Giacom ingestion unchanged.

**Rationale**:
- Single operator-facing diagnostic vocabulary (constitution III terminology).
- Future UI can render one log component for all ingestion sources.

## R9: Amount and Interval Parsing

**Decision**:
- **Amounts**: Parse decimal from export; convert to smallest currency unit (`long`) using currency exponent (GBP/USD = 2). Strip currency symbols and thousands separators.
- **Intervals**: Map Stripe strings (`day`, `week`, `month`, `year`) + `Interval Count` to raw string fields on `RawStripePrice`; full `BillingFrequency`/`Term` mapping deferred to `IStripeBillingNormalizer`.

**Rationale**:
- Raw import preserves source strings; normalizer owns domain enum mapping per 001 contract.
- Ingestion validates parseability only.

## Open Questions Resolved

| Question | Resolution |
|----------|------------|
| Subscriptions CSV without catalogue files? | Supported; catalogue warnings only (FR-005) |
| Customer record source? | Extract unique customers from subscriptions CSV rows; no separate customers.csv in MVP |
| Encoding | UTF-8 default; detect BOM; log warning on replacement characters |
