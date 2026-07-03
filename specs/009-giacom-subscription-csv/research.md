# Research: Giacom Subscription Management CSV Ingestion

**Feature**: `009-giacom-subscription-csv`  
**Date**: 2026-07-03

## R1: CSV Parsing Library

**Decision**: Reuse **CsvHelper** (already referenced by Stripe CSV ingestion in `BillDrift.Infrastructure`).

**Rationale**:
- MIT license; proven in 003 for header-mapped dashboard exports.
- Handles quoted fields, embedded commas, UTF-8 BOM — typical Giacom CSV characteristics.
- No new dependency; constitution VI (pragmatic simplicity).

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Manual split | Fragile on quoted product names |
| Excel parser | Input is CSV per FR-001 |
| Sylvan.Data.Csv | Adds dependency with no advantage |

## R2: Source File Identity

**Decision**: `SourceDocumentId` = lowercase hex **SHA-256 hash of raw CSV bytes**.

**Rationale**:
- Identical re-upload → identical document ID (SC-004).
- Consistent with Giacom PDF (002 R2) and Stripe CSV (003 R2).
- `ImportSourceKind.GiacomSubscriptionManagement` on every `RawImportId`.

**Line key**: `SourceLineKey` = `{rowNumber}` (1-based data row index after header). Supplier subscription ID is stored as a field but not used as line key (may duplicate across history rows or be absent).

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Supplier subscription ID as line key | Not always unique or present per row |
| Filename only | Breaks idempotency |

## R3: Header Alias Registry

**Decision**: Central **`SubscriptionManagementCsvHeaderMap`** with case-insensitive alias lists per logical field (mirror `StripeCsvHeaderMap`).

**Rationale**:
- FR-002 requires column reordering and synonym tolerance.
- Fixture authors and operators can extend aliases without parser changes.
- Contract document lists canonical aliases; production CSV sample required before implementation lock-in.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Fixed column indexes | Breaks on export layout drift |
| Per-month parser variants | High maintenance |

## R4: Microsoft 365 / CSP Product Scope Filter

**Decision**: Two-tier **`ProductScopeClassifier`**:

1. **Hard exclude** when `Service`, `Product Type`, or `Product` matches known non-CSP patterns (e.g., `Exclaimer`, `Non-CSP`, `Third Party`, `Add-on` — configurable deny list in contract).
2. **Hard include** when any of: `Service` contains `Microsoft` / `Office 365` / `Microsoft 365`; `Product Type` contains `CSP` / `NCE`; `Product` matches Microsoft 365 naming patterns (`Microsoft 365`, `Office 365`, `Exchange Online`, `SharePoint`, `Teams`, `Defender`, `Entra`, `Azure AD`, `Dynamics 365 Business`).
3. **Ambiguous** (blank service, Microsoft-like product name): **include with warning** log (`ProductScopeAmbiguous`); **exclude with warning** only when deny-list token matches.

**Rationale**:
- Spec US2 requires Exclaimer exclusion without false negatives on M365 rows with blank service column.
- Operators can review ambiguous warnings in ingestion summary (SC-002).
- Classification rules externalized in contract for fixture testing.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Include all rows | Violates FR-013 |
| Exclude all ambiguous | Loses valid M365 rows with sparse columns |
| Offer ID prefix only | Offer IDs not human-auditable for scope |

## R5: Boolean Flag Normalisation (NCE / Trial)

**Decision**: Parse flag columns via **`BooleanFlagParser`** accepting: `Y`/`N`, `Yes`/`No`, `True`/`False`, `1`/`0`, case-insensitive. Blank → absent (`null`). Unrecognised non-blank → warning + treat as absent.

**Rationale**:
- Spec edge case: varied representations.
- Downstream reconciliation compares lifecycle context; false precision worse than absent + warning.

## R6: Mex ID and Commercial Key Normalisation

**Decision**: Align with 002 R10 and 003 metadata rules:
- **Mex ID**: trim whitespace; preserve casing in `MexIdRaw`; normalised `MexId` value object applies trim + uppercase for correlation (consistent with PDF ingestion).
- **Offer ID / SKU ID**: trim whitespace; preserve raw; normalised `OfferId`/`SkuId` value objects apply trim + consistent casing per 001 normalization contract.
- Missing Offer ID or SKU ID: emit raw row with warning; normalizer skips row or produces line without `CommercialKeyRoot` — **ingestion emits raw row**; **normalization** fails individual rows when commercial key required (documented in pipeline contract).

**Rationale**:
- FR-015/FR-016/FR-017 alignment.
- Separation of ingestion (faithful capture) vs normalization (strict domain validation).

## R7: Extended Raw and Normalized Fields

**Decision**: Extend domain types minimally:

| Layer | Change |
|-------|--------|
| `RawSubscriptionManagementRow` | Add optional raw string fields for service, product name, product type, NCE/trial flags, price/ERP, end-of-term action, cancellable-until, migration-to-NCE, assigned licences |
| `MicrosoftSubscriptionLine` | Add optional `SubscriptionLifecycleFacts` value object (NCE, trial, end-of-term action, cancellable-until, migration-to-NCE, assigned licence count, wholesale/ERP prices when present) |
| `ProductDisplayFacts` | New small VO on normalized line: service, product name, product type as written (for operator display, not matching keys) |

**Rationale**:
- 001 raw type predates spec lifecycle columns; extension preserves source fidelity.
- Lifecycle facts grouped in VO avoids bloating `MicrosoftSubscriptionLine` constructor with 10+ optionals.
- Product display names preserved for reconciliation UI without affecting commercial key matching.

## R8: Normalizer Implementation Scope

**Decision**: Implement **`SubscriptionManagementNormalizer`** in `BillDrift.Application.Normalization` as part of this feature (not deferred).

**Rationale**:
- Spec FR-024 requires output ready for Stripe reconciliation — that is `MicrosoftSubscriptionLine`, not raw rows alone.
- 001 stubbed interface; implementation is bounded and testable with CSV fixtures.
- Status/term/frequency mapping reuses 001 normalization contract tables.

## R9: Azure Storage for Ingestion Runs

**Decision**: Hybrid **Table + Blob** for ingestion persistence (mirror 008 run-history pattern):

| Store | Content |
|-------|---------|
| **Azure Blob** (`ingestion-uploads` container) | Original CSV bytes at `{ingestionId}/source/SubscriptionManagementReport.csv`; result JSON at `{ingestionId}/result/manifest.json` + `raw-rows.json` + `subscription-truth.json` |
| **Azure Table** (`ingestionruns` table) | Queryable index: ingestion ID, source kind, filename, content hash, uploaded at, status, summary counts, blob paths |

**Clients**: Aspire-injected `BlobServiceClient` and `TableServiceClient` only — `builder.AddAzureBlobServiceClient("blobs")` / `AddAzureTableServiceClient("tables")` already in `BillDrift.Api/Program.cs`.

**Rationale**:
- User constraint: v1 uses Blob + Table exclusively; no SQL.
- User constraint: no manual connection string construction.
- 008 `InputSnapshotMetadata` consumes fingerprints from ingestion — table index enables operator upload history and API listing.
- Parser tests remain stream-based (no Azure required); Azurite integration tests for stores.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Parser-only (no persistence) | User explicitly requires Azure storage; 008 needs upload metadata |
| SQL for ingestion index | Violates user constraint |
| Manual `BlobServiceClient` from env | Violates Aspire DI rule |
| Blob-only | Listing uploads requires full container scan |

## R10: API Upload Surface

**Decision**: Add **`POST /api/imports/subscription-management`** multipart upload endpoint in `BillDrift.Api` orchestrating: store CSV blob → parse → normalize → store result blobs → write table index row.

**Rationale**:
- Completes operator workflow (manual MVP per constitution).
- Web calls API only (008 constraint); no storage clients in `BillDrift.Web`.
- Thin endpoint delegates to `ISubscriptionManagementIngestionService` in Application.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Blazor direct ingester call | No upload persistence; bypasses audit trail |
| Separate microservice | Violates pragmatic simplicity |

## R11: Ingestion Failure Reason Codes

**Decision**: Extend shared `IngestionFailureReason` enum with:

| Code | Use |
|------|-----|
| `ProductOutOfScope` | Row excluded by CSP scope filter (informational; counted separately from skip) |
| `ProductScopeAmbiguous` | Warning on ambiguous classification |
| `LicenceCountUnparseable` | Row skip |
| `PriceUnparseable` | Row skip when price column present but invalid |
| `CommercialKeyMissing` | Warning when offer or SKU absent |
| `DateUnparseable` | Warning for optional date fields |

**Rationale**:
- Reuse shared `IngestionLogEntry` model from 002/003.
- SC-005 requires log summaries for missing commercial keys.

## R12: Date and Money Parsing

**Decision**:
- **Dates** (`RenewalDate`, `CancellableUntil`): UK culture default (`dd/MM/yyyy`, `dd-MMM-yyyy`); ISO `yyyy-MM-dd` fallback.
- **Money** (`Price`, `ERP`): Parse decimal with `£` symbol stripped; GBP assumed per spec assumptions; unparseable → row skip if column non-empty (FR-020 edge case for price).

**Rationale**:
- Consistent with Giacom PDF date parsing (002) and UK reseller context.

## Fixture Dependency

**BLOCKER for implementation lock-in**: Obtain at least one sanitized production `SubscriptionManagementReport.csv` plus one column-variant export before finalising header alias list. Until then, contract aliases are **provisional** from spec field names and operator input.
