# Data Model: Stripe Billing CSV Ingestion

**Feature**: `003-stripe-csv-ingestion`  
**Date**: 2026-07-02

This document defines pipeline-specific types for Stripe CSV ingestion. Output records conform to and extend existing domain types in `BillDrift.Domain.Import.Stripe` (see `001-billing-domain-model/data-model.md`).

## Type Placement

| Layer | Namespace | Types |
|-------|-----------|-------|
| Application | `BillDrift.Application.Import` | Public contract, request, result DTOs |
| Infrastructure | `BillDrift.Infrastructure.Import.Stripe.Internal` | Parse-stage row DTOs |
| Domain | `BillDrift.Domain.Import.Stripe` | Raw Stripe records + `RawImportId` (extended) |

## Application Layer (Public)

### `StripeCsvIngestionOptions`

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `IncludeInactiveSubscriptions` | `bool` | `false` | When false, filter to active status set (R6) |
| `MaxFileSizeBytes` | `long` | `10_485_760` (10 MB) | Per-file limit at intake |

### `StripeCsvFileInput`

| Field | Type | Notes |
|-------|------|-------|
| `FileKind` | `StripeCsvFileKind` | Which export type |
| `Content` | `Stream` | Readable CSV bytes |
| `OriginalFileName` | `string?` | Audit only; not used for identity |

### `StripeCsvFileKind`

```csharp
public enum StripeCsvFileKind
{
    Subscriptions = 0,
    Products = 1,
    Prices = 2
}
```

### `StripeCsvIngestionRequest`

| Field | Type | Notes |
|-------|------|-------|
| `Files` | `IReadOnlyList<StripeCsvFileInput>` | At least subscriptions required |
| `Options` | `StripeCsvIngestionOptions` | Filtering and limits |

### `StripeCsvIngestionSummary`

| Field | Type | Notes |
|-------|------|-------|
| `SubscriptionItemsExtracted` | `int` | Emitted item rows |
| `SubscriptionItemsSkipped` | `int` | Row-level parse failures |
| `SubscriptionsFilteredByStatus` | `int` | Excluded by status filter (not an error) |
| `ProductsExtracted` | `int` | |
| `ProductsSkipped` | `int` | |
| `PricesExtracted` | `int` | |
| `PricesSkipped` | `int` | |
| `MetadataWarnings` | `int` | Missing/inconsistent mapping metadata |
| `CatalogueWarnings` | `int` | Unresolved product/price references |
| `CustomersExtracted` | `int` | Distinct customers from subscriptions file |

### `StripeCsvIngestionResult`

| Field | Type | Notes |
|-------|------|-------|
| `BundleId` | `string` | SHA-256 hex over sorted per-file hashes |
| `IngestedAt` | `DateTimeOffset` | UTC completion time |
| `Status` | `IngestionOutcomeStatus` | Shared with Giacom ingestion |
| `Customers` | `IReadOnlyList<RawStripeCustomer>` | Deduped by `CustomerId` |
| `Subscriptions` | `IReadOnlyList<RawStripeSubscription>` | Deduped by `SubscriptionId` |
| `SubscriptionItems` | `IReadOnlyList<RawStripeSubscriptionItem>` | One per parsed item row |
| `Products` | `IReadOnlyList<RawStripeProduct>` | From products CSV |
| `Prices` | `IReadOnlyList<RawStripePrice>` | From prices CSV |
| `LogEntries` | `IReadOnlyList<IngestionLogEntry>` | Skips + warnings |
| `Summary` | `StripeCsvIngestionSummary` | Roll-up counts |
| `SourceFiles` | `IReadOnlyList<StripeCsvSourceFileInfo>` | Per-file metadata |

### `StripeCsvSourceFileInfo`

| Field | Type | Notes |
|-------|------|-------|
| `FileKind` | `StripeCsvFileKind` | |
| `SourceDocumentId` | `string` | SHA-256 hex of file bytes |
| `OriginalFileName` | `string?` | |
| `RowCount` | `int` | Data rows processed (excl. header) |

### `IStripeBillingCsvIngester`

```csharp
public interface IStripeBillingCsvIngester
{
    /// <summary>
    /// Parses Stripe dashboard CSV exports into raw import records.
    /// Never throws for parse failures — inspect <see cref="StripeCsvIngestionResult.Status"/>.
    /// </summary>
    StripeCsvIngestionResult Ingest(
        StripeCsvIngestionRequest request,
        CancellationToken cancellationToken = default);
}
```

## Extended Domain Types

Existing raw records gain ingestion trace fields (implementation adds to Domain):

### `RawStripeSubscriptionItem` (extended)

| Added field | Type | Notes |
|-------------|------|-------|
| `Id` | `RawImportId` | Idempotency key |
| `CustomerId` | `string` | From subscriptions row |
| `ProductName` | `string?` | As shown on subscription export row |
| `SubscriptionStatus` | `string` | Parent subscription status |
| `UnitAmountRaw` | `string?` | Pre-normalization amount text |
| `IntervalRaw` | `string?` | Pre-normalization interval text |
| `SourceRowNumber` | `int` | 1-based CSV data row index |

### `RawStripeProduct` (extended)

| Added field | Type | Notes |
|-------------|------|-------|
| `Id` | `RawImportId` | |
| `SourceRowNumber` | `int` | |

### `RawStripePrice` (extended)

| Added field | Type | Notes |
|-------------|------|-------|
| `Id` | `RawImportId` | |
| `Description` | `string?` | From export |
| `SourceRowNumber` | `int` | |

### `RawStripeCustomer` / `RawStripeSubscription`

No structural change beyond optional `SourceRowNumber` on first-seen row; deduplicated during ingestion assembly.

## Infrastructure Internal Types

### `ParsedSubscriptionRow`

| Field | Type | Notes |
|-------|------|-------|
| `RowNumber` | `int` | |
| `CustomerId` | `string?` | |
| `CustomerName` | `string?` | |
| `SubscriptionId` | `string?` | |
| `SubscriptionItemId` | `string?` | |
| `ProductId` | `string?` | |
| `ProductName` | `string?` | |
| `PriceId` | `string?` | |
| `QuantityRaw` | `string?` | |
| `Status` | `string?` | |
| `UnitAmountRaw` | `string?` | |
| `IntervalRaw` | `string?` | |
| `Metadata` | `Dictionary<string, string>` | Merged from all metadata columns |

### `ParsedProductRow` / `ParsedPriceRow`

Mirror CSV columns after header mapping; retain all unmapped columns in `Metadata` or `AdditionalFields` dictionary for forward compatibility.

## Extended `IngestionFailureReason` Values

Add to existing enum in `BillDrift.Application.Import`:

| Value | Severity | Scope |
|-------|----------|-------|
| `MandatoryHeaderMissing` | Error | File |
| `AmountUnparseable` | Error | Row |
| `StripeIdMissing` | Error | Row |
| `MetadataIncomplete` | Warning | Row |
| `MetadataInconsistent` | Warning | Row |
| `CatalogueReferenceUnresolved` | Warning | Row |
| `EmptyFile` | Error | File |

## `IngestionLogEntry` Location for CSV

Reuse `IngestionLocation` with conventions:

| Field | CSV mapping |
|-------|-------------|
| `PageNumber` | `0` (unused) |
| `BlockIndex` | `(int)StripeCsvFileKind` |
| `LineIndex` | CSV data row number |

`SourceDocumentId` on log entry = per-file SHA-256 hash.

## Validation Rules

| Entity | Rule |
|--------|------|
| Subscription item | `SubscriptionId`, `CustomerId`, `ProductId`, `PriceId` non-empty; `Quantity` ≥ 0 |
| Product | `ProductId`, `Name` non-empty |
| Price | `PriceId`, `ProductId`, `Currency` non-empty |
| Customer | `CustomerId` non-empty |
| Metadata | Warn if `mex_id` missing on item row; warn if `offer_id` xor `sku_id` alone |
| Status filter | Exclude row when status ∉ active set and `IncludeInactiveSubscriptions` is false |

## State: Ingestion Outcome

```text
[Start] → Parse files
  → Any file mandatory header missing? → Failure (no items)
  → Zero parseable subscription rows AND subscriptions required? → Failure
  → Any row skipped OR warnings? → PartialSuccess (if some rows emitted)
  → All rows OK? → Success
```

Empty subscriptions file with headers only → Success with empty collections + `EmptyFile` warning (informational, not Failure per spec edge case).

## Relationships

```text
StripeCsvIngestionResult
  ├── SourceFiles (1..3)
  ├── Customers (0..n) ← deduped from subscription rows
  ├── Subscriptions (0..n) ← deduped from subscription rows
  ├── SubscriptionItems (0..n) ← 1..n per multi-item subscription
  ├── Products (0..n)
  ├── Prices (0..n)
  └── LogEntries (0..n)

SubscriptionItem ──references──► ProductId, PriceId
Price ──references──► ProductId
SubscriptionItem ──join──► Customer, Subscription (by IDs)
```

## Handoff to Normalization

```text
StripeCsvIngestionResult
  → IStripeBillingNormalizer.Normalize(customer, subscriptions, items, products, prices)
  → IReadOnlyList<StripeBillingItem>
```

Ingestion does **not** invoke the normalizer; callers orchestrate both stages.
