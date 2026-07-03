# Data Model: Retail Pricing and Pricing Strategy Ingestion

**Feature**: `010-retail-pricing-ingestion`  
**Date**: 2026-07-03

Pipeline-specific types extend the billing domain (001). Output normalized records are `IntendedPrice` instances ready for `ReconciliationInputs.IntendedPrices`, `IntendedPriceIndex`, and run-history `InputDomainType.IntendedPricing` snapshots.

## Type Placement

| Layer | Namespace | Types |
|-------|-----------|-------|
| Application | `BillDrift.Application.Import` | Public ingester contract, request, result DTOs |
| Application | `BillDrift.Application.Import.RetailPricing` | Orchestration service for upload + persist + resolution |
| Application | `BillDrift.Application.Normalization` | `PriceListNormalizer`, existing `IntendedPriceResolver` |
| Infrastructure | `BillDrift.Infrastructure.Import.Giacom.RetailPricing` | CSV parser, header map, mappers |
| Infrastructure | `BillDrift.Infrastructure.Import.Giacom.RetailPricing.Internal` | Parse-stage row DTOs |
| Infrastructure | `BillDrift.Infrastructure.Ingestion` | Extended Azure blob + table stores |
| Domain | `BillDrift.Domain.Import` | Extended `RawPriceListRow` |
| Domain | `BillDrift.Domain.Billing` | Extended `IntendedPrice`, new `PricingPlatform` enum / facts |
| Domain | `BillDrift.Domain.Common` | Extended `Term` (+ `Triennial`) |

## Domain Extensions (001)

### `Term` enum (extended)

Add:

| Value | Meaning |
|-------|---------|
| `Triennial` | Three-year contract term |

### `PricingPlatform` enum (new)

| Value | Meaning |
|-------|---------|
| `Nce` | New Commerce Experience platform pricing |
| `Legacy` | Legacy CSP platform pricing |
| `Unknown` | Not specified or unrecognised in source |

### `RawPriceListRow` (extended)

Existing fields retained. **Add**:

| Field | Type | Notes |
|-------|------|-------|
| `PlatformRaw` | `string?` | NCE / Legacy text as exported |
| `CurrencyRaw` | `string?` | When present in export |

### `IntendedPrice` (extended)

Add optional parameters:

| Field | Type | Notes |
|-------|------|-------|
| `Platform` | `PricingPlatform` | Default `Unknown` for manual overrides |
| `Classification` | `ProductClassification` | `Csp` for catalogue; `NonCsp` for manual overrides |

`RawManualPriceEntry` — **no change** (001 shape sufficient).

## Application Layer (Public)

### `RetailPricingCsvIngestionOptions`

| Field | Type | Default |
|-------|------|---------|
| `MaxFileSizeBytes` | `long` | `10_485_760` (10 MB) |
| `MaxManualOverrides` | `int` | `500` |
| `NormalizeOutput` | `bool` | `true` |
| `DefaultCurrency` | `string` | `"GBP"` |

### `RetailPricingCsvIngestionRequest`

| Field | Type |
|-------|------|
| `CatalogueContent` | `Stream` |
| `OriginalFileName` | `string?` |
| `ManualOverrides` | `IReadOnlyList<ManualPriceOverrideRequest>?` |
| `Options` | `RetailPricingCsvIngestionOptions` |

### `ManualPriceOverrideRequest`

| Field | Type | Required |
|-------|------|----------|
| `OfferId` | `string?` | At least one of OfferId/SkuId |
| `SkuId` | `string?` | At least one of OfferId/SkuId |
| `Term` | `string` | Yes |
| `Frequency` | `string` | Yes |
| `Rrp` | `string` | Yes |
| `Wholesale` | `string?` | No |
| `Reason` | `string` | Yes |
| `EffectiveDate` | `DateOnly` | Yes |

### `RetailPricingCsvIngestionSummary`

| Field | Type |
|-------|------|
| `CatalogueRowsRead` | `int` |
| `CatalogueRowsEmitted` | `int` |
| `CatalogueRowsSkipped` | `int` |
| `ManualOverridesSubmitted` | `int` |
| `ManualOverridesAccepted` | `int` |
| `ManualOverridesRejected` | `int` |
| `DuplicateKeyWarnings` | `int` |
| `OverrideWinsCount` | `int` |
| `CatalogueOnlyCount` | `int` |
| `ResolvedPriceCount` | `int` |
| `NormalizationSkipped` | `int` |

### `RetailPricingCsvIngestionResult`

| Field | Type |
|-------|------|
| `IngestionId` | `Guid?` |
| `Status` | `IngestionOutcomeStatus` |
| `SourceDocumentId` | `string` |
| `IngestedAt` | `DateTimeOffset` |
| `RawCatalogueRows` | `IReadOnlyList<RawPriceListRow>` |
| `RawManualEntries` | `IReadOnlyList<RawManualPriceEntry>` |
| `CataloguePrices` | `IReadOnlyList<IntendedPrice>` |
| `ManualPrices` | `IReadOnlyList<IntendedPrice>` |
| `ResolvedPrices` | `IReadOnlyList<IntendedPrice>` |
| `ResolutionDetails` | `IReadOnlyList<PricingResolutionDetail>` |
| `Summary` | `RetailPricingCsvIngestionSummary` |
| `LogEntries` | `IReadOnlyList<IngestionLogEntry>` |

### `PricingResolutionDetail`

| Field | Type |
|-------|------|
| `CommercialKey` | `CommercialKey` |
| `WinningSource` | `PriceSource` |
| `EffectiveRrp` | `decimal` |
| `HadCatalogueEntry` | `bool` |
| `HadManualOverride` | `bool` |

### `RetailPricingIngestionRun`

Queryable index record (parallel to `SubscriptionManagementIngestionRun`):

| Field | Type |
|-------|------|
| `IngestionId` | `Guid` |
| `SourceKind` | `ImportSourceKind.GiacomPriceList` |
| `OriginalFileName` | `string?` |
| `ContentFingerprint` | `string` |
| `UploadedAt` | `DateTimeOffset` |
| `CompletedAt` | `DateTimeOffset?` |
| `Status` | `IngestionRunStatus` |
| `Summary` | `RetailPricingCsvIngestionSummary?` |
| `SourceBlobPath` | `string` |
| `ResultManifestBlobPath` | `string?` |
| `FailureReason` | `string?` |

## Infrastructure (Internal)

### `ParsedResellerPricingRow`

Intermediate DTO after CSV row read, before `RawPriceListRow` mapping:

| Field | Type |
|-------|------|
| `RowNumber` | `int` |
| `OfferId` | `string?` |
| `SkuId` | `string?` |
| `Term` | `string?` |
| `Frequency` | `string?` |
| `Wholesale` | `string?` |
| `Rrp` | `string?` |
| `Margin` | `string?` |
| `MarginPercent` | `string?` |
| `Status` | `string?` |
| `Platform` | `string?` |
| `Currency` | `string?` |

## Blob Payload Shapes

### `resolved-prices.json`

```json
{
  "records": [ /* IntendedPrice[] after merge */ ],
  "resolutionDetails": [ /* PricingResolutionDetail[] */ ],
  "logEntries": [ /* IngestionLogEntry[] */ ]
}
```

### `catalogue-prices.json`

Normalized catalogue-sourced `IntendedPrice[]` before override merge (audit).

### `manual-overrides.json`

Persisted copy of accepted `RawManualPriceEntry[]` from the upload.

## Extended `IngestionFailureReason` Values

| Code | When |
|------|------|
| `MissingMandatoryHeader` | OfferId/SkuId/Term/Frequency/Wholesale/Rrp headers not found |
| `TermUnparseable` | Term text not mapped |
| `FrequencyUnparseable` | Frequency text not mapped |
| `WholesaleUnparseable` | Wholesale not decimal |
| `RrpUnparseable` | RRP not decimal |
| `MissingCommercialKey` | Both offer ID and SKU ID blank |
| `DuplicateCommercialKey` | Second catalogue row for same key |
| `UnsupportedCurrency` | Non-GBP when currency column present |
| `ManualOverrideValidationFailed` | Override missing required fields |
| `PlatformUnrecognised` | Warning only — maps to Unknown |

## Validation Rules

| Rule | Layer |
|------|-------|
| Offer ID **or** SKU ID required (both preferred) | Parser skip |
| Wholesale + RRP required for catalogue rows | Parser skip |
| RRP required for manual overrides | Request validation |
| Term + frequency required | Parser / request validation |
| Manual override reason + effective date required | Request validation |
| GBP only in v1 | Normalizer |
| `CommercialKey` uniqueness in resolved output | Resolver + index build |

## Relationships

```text
ResellerPricingVsRRP.csv
  → RawPriceListRow[]
  → IntendedPrice[] (Catalogue, Csp)
        ↘
ManualPriceOverrideRequest[] → RawManualPriceEntry[] → IntendedPrice[] (ManualOverride, NonCsp)
        ↘
   IntendedPriceResolver merge
        ↓
   ResolvedPrices[] → Blob + ReconciliationInputs + Run History snapshot
```
