# Stripe CSV Ingestion Pipeline Contract

**Feature**: `003-stripe-csv-ingestion`  
**Consumer**: `BillDrift.Api` / future upload UI → `BillDrift.Infrastructure`  
**Producer output**: `BillDrift.Domain.Import.Stripe.*` via `StripeCsvIngestionResult`

## Purpose

Defines the public boundary for Stripe billing CSV ingestion. Callers pass one to three CSV streams (subscriptions required; products and prices optional); the ingester returns structured raw import records plus diagnostic logs. Normalization to `StripeBillingItem` is **out of scope** — handled by `IStripeBillingNormalizer` per [001 normalization contract](../../001-billing-domain-model/contracts/normalization.md).

## Interface (Application Layer)

```csharp
namespace BillDrift.Application.Import;

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

## Pipeline Stages

Executed sequentially by `StripeBillingCsvIngester`:

| Stage | Responsibility | Failure mode |
|-------|----------------|--------------|
| 1. **Intake** | Validate streams non-empty, size ≤ limit, UTF-8 readable | File fail |
| 2. **Header Detection** | Match required columns via alias map per file kind | File fail if mandatory missing |
| 3. **Row Parsing** | CsvHelper → `Parsed*Row` DTOs | Row skip |
| 4. **Catalogue Assembly** | Map products + prices CSV to raw catalogue records | Row skip |
| 5. **Subscription Assembly** | Map subscription rows → customers, subscriptions, items | Row skip |
| 6. **Metadata Normalisation** | Extract Mex/Offer/SKU/supplier refs; warn on gaps | Warning only |
| 7. **Status Filtering** | Apply active/inactive filter (R6) | Count excluded rows |
| 8. **Catalogue Cross-Check** | Warn when item references unknown product/price ID in bundle | Warning only |
| 9. **Validation** | Stripe ID format, quantity bounds | Row skip |
| 10. **Output Assembly** | Build `StripeCsvIngestionResult` + `RawImportId` keys | — |
| 11. **Logging** | Append `IngestionLogEntry` for skips/warnings | — |

## Input Guarantees (Caller)

| Requirement | Rule |
|-------------|------|
| Subscriptions file | Required unless explicitly testing catalogue-only (not supported in MVP — subscriptions required) |
| Products / prices | Optional; warnings when missing and references unresolved |
| Stream | Readable; ingester buffers each file for hash + parse |
| Format | Stripe dashboard CSV with header row |
| Encoding | UTF-8 (BOM tolerated) |

## Output Guarantees (Ingester)

| Requirement | Rule |
|-------------|------|
| `SourceDocumentId` (per file) | SHA-256 hex of CSV bytes |
| `BundleId` | SHA-256 hex of sorted concatenated file hashes |
| `RawImportId` on items/products/prices | `ImportSourceKind.StripeExport` + file hash + line key |
| Stripe IDs | Preserved exactly as exported (`sub_`, `si_`, `prod_`, `price_`, `cus_`) |
| Metadata | Full dictionary preserved; typed extraction for known keys |
| Mapping keys | MUST NOT invent Mex/Offer/SKU when absent |
| Determinism | Same bytes + options → identical records and IDs (SC-004) |
| Partial failure | Valid rows emitted when siblings skipped (SC-003) |
| Status filter | Excluded inactive rows absent from output; count in summary |

## Line Key Resolution

```
IF SubscriptionItemId present AND non-empty
  SourceLineKey = SubscriptionItemId
ELSE IF SubscriptionId present (single-item fallback)
  SourceLineKey = SubscriptionId
ELSE
  SourceLineKey = "{rowNumber}"
```

Products use `ProductId`; prices use `PriceId` with same fallback pattern.

## Dependency Injection Registration

```csharp
namespace BillDrift.Infrastructure.Import.Stripe;

public static class StripeImportServiceCollectionExtensions
{
    public static IServiceCollection AddStripeBillingCsvIngestion(this IServiceCollection services)
    {
        services.AddSingleton<IStripeBillingCsvIngester, StripeBillingCsvIngester>();
        return services;
    }
}
```

## Exception Policy

| Condition | Behavior |
|-----------|----------|
| Parse/validation failures | Return `StripeCsvIngestionResult` with `Failure` or `PartialSuccess` |
| `cancellationToken` cancelled | Throw `OperationCanceledException` |
| Null request or null subscriptions stream | Throw `ArgumentNullException` (programmer error) |

Ingester MUST NOT throw `NormalizationException` — normalization is a separate stage.

## Handoff to Normalization

```text
StripeCsvIngestionResult (grouped by CustomerId)
  → foreach customer:
      IStripeBillingNormalizer.Normalize(customer, subs, items, products, prices)
  → StripeBillingItem[] (Application layer, separate implementation)
```

Ingestion log entries are **not** passed to normalizer; operators review via future UI.

## Performance Contract

| Metric | Target |
|--------|--------|
| 1,000 subscription items + catalogue | < 1 minute (SC-001) |
| Typical monthly bundle | < 10 seconds (design target) |
| Memory | Each file buffered once for hash; row DTOs not retained after mapping |

## Security Contract

- Cap per-file size (default 10 MB).
- Log snippets capped at 200 characters.
- No Stripe API keys required for CSV MVP.
- Do not log full customer email or payment fields unless required for debugging — prefer IDs in log messages.

## Catalogue-Only Reference Warnings

When products/prices files are omitted:

- Ingestion succeeds for subscriptions.
- Each item with `ProductId` / `PriceId` receives `CatalogueReferenceUnresolved` warning if cross-check requested internally — suppressed when catalogue file kind not supplied (FR-005).

When products/prices supplied:

- Warn on IDs not found in parsed catalogue collections (FR-033 edge case).
