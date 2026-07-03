# Research: Retail Pricing and Pricing Strategy Ingestion

**Feature**: `010-retail-pricing-ingestion`  
**Date**: 2026-07-03

## R1: CSV Parsing Library

**Decision**: Reuse **CsvHelper** (already referenced by Stripe and Subscription Management ingestion).

**Rationale**:
- Proven header-mapped export parsing in 003 and 009.
- No new dependency; constitution VI (pragmatic simplicity).
- `ResellerPricingVsRRP.csv` is a standard comma-separated export with quoted fields.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Manual split | Fragile on product names with commas |
| Excel parser | Input is CSV per FR-001 |
| Sylvan.Data.Csv | Adds dependency with no advantage |

## R2: Source File Identity

**Decision**: `SourceDocumentId` = lowercase hex **SHA-256 hash of raw CSV bytes** for catalogue uploads. Manual overrides use a synthetic document ID `{ingestionId}/manual-overrides` with per-entry line keys.

**Rationale**:
- Identical re-upload → identical document ID (deterministic re-import).
- Consistent with 002/003/009 patterns.
- `ImportSourceKind.GiacomPriceList` for catalogue rows; `ImportSourceKind.ManualPriceEntry` for overrides.

**Line key**: `SourceLineKey` = `{rowNumber}` (1-based data row index after header).

## R3: Header Alias Registry

**Decision**: Central **`ResellerPricingCsvHeaderMap`** with case-insensitive alias lists per logical field (mirror `SubscriptionManagementCsvHeaderMap`).

**Rationale**:
- FR-004 requires column reordering and synonym tolerance.
- Contract document lists canonical aliases; production CSV sample required before implementation lock-in.

**Mandatory logical fields**: `OfferId`, `SkuId`, `Term`, `Frequency`, `Wholesale`, `Rrp`  
**Optional logical fields**: `Margin`, `MarginPercent`, `Status`, `Platform`, `Currency`

## R4: Term and Frequency Mapping

**Decision**: Extend term mapping to include **Triennial** as a new `Term` enum value (`Term.Triennial`). Map common aliases:

| Raw (case-insensitive) | `Term` |
|------------------------|--------|
| `Monthly`, `1 Month`, `P1M` | `Monthly` or `P1M` (normalizer maps P1M → Monthly for `CommercialKey`) |
| `Annual`, `1 Year`, `P1Y`, `Yearly` | `Annual` |
| `Triennial`, `3 Year`, `36 Month`, `P3Y` | `Triennial` |
| Unrecognised non-blank | Row skipped + `TermUnparseable` log |

| Raw | `BillingFrequency` |
|-----|-------------------|
| `Monthly`, `Month` | `Monthly` |
| `Annual`, `Yearly`, `Year` | `Annual` |
| Unrecognised | Row skipped + `FrequencyUnparseable` log |

**Rationale**:
- Spec FR-006 explicitly lists Triennial; current `Term` enum lacks it.
- `CommercialKey` uses normalized `Term` + `BillingFrequency` — both dimensions preserved independently per spec edge case.

## R5: Platform Classification (NCE / Legacy)

**Decision**: New **`PricingPlatform`** enum (`Nce`, `Legacy`, `Unknown`). Parse via **`PlatformClassifier`** from optional `Platform` column:

| Raw patterns | `PricingPlatform` |
|--------------|-------------------|
| `NCE`, `New Commerce`, `New Commerce Experience` | `Nce` |
| `Legacy`, `Legacy CSP`, `Old Commerce` | `Legacy` |
| Blank / absent column | `Unknown` |
| Unrecognised non-blank | `Unknown` + warning log |

**Rationale**:
- Spec FR-010; blank must not block ingestion.
- Stored on `IntendedPrice` via new `PricingPlatformFacts` VO for downstream display and classification signals.

## R6: Product Status Mapping

**Decision**: Map `StatusRaw` to existing **`PriceListStatus`** enum:

| Raw patterns | `PriceListStatus` |
|--------------|-------------------|
| `Active`, `Available` | `Active` |
| `End of Sale`, `EndOfSale`, `EOS`, `Discontinued` | `EndOfSale` |
| Blank | `Active` (assume active when selling) with info log |
| Unrecognised | `Unknown` + warning |

**Rationale**:
- Aligns with 001 normalization contract.
- End-of-sale rows retain RRP per FR-018.

## R7: Monetary Parsing

**Decision**: Parse wholesale, RRP, and margin as **GBP `Money`** via shared decimal parser (UK culture, strip `£`, commas). Optional `Currency` column: when present and not GBP, row skipped with `UnsupportedCurrency` log (v1 UK reseller scope).

**Rationale**:
- Spec assumption: GBP default for UK resellers.
- Margin percent parsed as decimal 0–100; preserve source when present; do not derive when absent (FR-028).

## R8: Pricing Strategy Resolution

**Decision**: Two-phase resolution in **`RetailPricingIngestionService`**:

1. **Parse & normalize** catalogue rows → `IntendedPrice` with `PriceSource.Catalogue`, `ProductClassification.Csp`.
2. **Parse & normalize** manual overrides → `IntendedPrice` with `PriceSource.ManualOverride`, `ProductClassification.NonCsp`.
3. **Merge** via existing **`IntendedPriceResolver`** (manual beats catalogue for same `CommercialKey`).
4. Emit **`ResolvedIntendedPrices`** collection plus per-key resolution metadata in ingestion summary.

**Rationale**:
- FR-015/FR-016 already implemented in `IntendedPriceResolver` and `IntendedPriceIndex`.
- Avoid duplicating precedence logic in parser.
- Classification on manual overrides satisfies FR-014 without separate mapping table.

## R9: Manual Override Input Channel

**Decision**: Multipart upload endpoint accepts:

1. **Required**: `ResellerPricingVsRRP.csv` file part.
2. **Optional**: `manual-overrides.json` file part **or** JSON array in form field `manualOverrides`.

Schema matches `RawManualPriceEntry` fields. Validation rejects entries missing both offer ID and SKU ID, or missing RRP/term/frequency/reason/effective date.

**Rationale**:
- FR-002/FR-003 require combined run in one operator action.
- JSON companion file supports bulk bespoke pricing without UI (API-first; Blazor deferred).
- Avoids SQL table for overrides — overrides persisted in blob `manual-overrides.json` alongside catalogue results.

## R10: Azure Persistence (Blob + Table, Aspire DI)

**Decision**: **Extend** existing `IIngestionBlobStore` / `IIngestionRunIndexStore` and Infrastructure implementations from 009 — **no new storage technology, no SQL**.

| Store | Extension |
|-------|-----------|
| Blob | Add price-list result paths: `intended-prices.json`, `resolved-prices.json`, optional `manual-overrides.json` |
| Table | PartitionKey `GiacomPriceList`; new `RetailPricingIngestionRun` index record type |

Constructors remain `AzureBlobIngestionArchiveStore(BlobServiceClient, …)` and `AzureTableIngestionRunIndexStore(TableServiceClient, …)` — **Aspire DI only**.

**Rationale**:
- User constraint: Blob + Table exclusively; DI-injected clients.
- Reuses proven manifest-last commit pattern from 009.
- Extending stores beats parallel duplicate Azure classes (constitution VI).

## R11: Normalizer Implementation

**Decision**: Implement **`PriceListNormalizer`** in `BillDrift.Application.Normalization` satisfying `IPriceListNormalizer` from 001.

Maps:
- Catalogue row → `IntendedPrice` with `PriceSource.Catalogue`, `ProductClassification.Csp`
- Manual entry → `IntendedPrice` with `PriceSource.ManualOverride`, `ProductClassification.NonCsp`
- Skips row (throws `NormalizationException` caught by pipeline) when offer ID and SKU ID both absent

**Rationale**:
- 001 stubbed interface; bounded implementation with fixture tests.
- Output is `IntendedPrice` — direct input to `ReconciliationInputs.IntendedPrices` and `IntendedPriceIndex`.

## R12: Duplicate Commercial Key Handling

**Decision**: Within catalogue file, **last row wins** for same normalized `CommercialKey` with `DuplicateCommercialKey` warning log. After merge, manual override always wins over catalogue regardless of order.

**Rationale**:
- Spec edge case and assumptions section.
- `IntendedPriceIndex.Build` already last-wins for duplicate keys in list order — orchestration orders catalogue then overrides so override appended last.

## R13: Ingestion Store Generalization vs Extension

**Decision**: **Extend** existing ingestion interfaces with price-list methods rather than introducing generic `IIngestionStore<T>` abstraction.

**Rationale**:
- Only two source kinds today; generic abstraction violates constitution VI without third consumer.
- Explicit methods keep blob path contracts readable in feature contracts.

## R14: Downstream Consumption

**Decision**: Blob payload `resolved-prices.json` feeds:

- `008` run archive `inputs/intended-pricing.json` via `InputDomainType.IntendedPricing`
- `004` reconciliation `ReconciliationInputs.IntendedPrices`
- `005` catalogue price mismatch (`StripePriceRrpMismatch` PricingVsCatalogue domain)

**Rationale**:
- Completes the fourth ingestion domain for four-domain reconciliation.
- No reconciliation engine changes required beyond wiring ingestion output.

## R15: Production CSV Sample Requirement

**Decision**: Obtain sanitized `ResellerPricingVsRRP.csv` exports (minimum 2 layout variants) before locking header alias map.

**Rationale**:
- 009 R12 lesson: header synonyms must be validated against real Giacom exports.
- Fixture `tests/fixtures/reseller-pricing-sample.json` exists for raw shape; CSV golden files needed for parser tests.
