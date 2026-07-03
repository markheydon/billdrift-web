# Subscription Management CSV Ingestion Pipeline Contract

**Feature**: `009-giacom-subscription-csv`  
**Consumer**: `BillDrift.Api` → `BillDrift.Application.Import` → `BillDrift.Infrastructure`  
**Producer output**: `RawSubscriptionManagementRow` + `MicrosoftSubscriptionLine` via `SubscriptionManagementCsvIngestionResult`

## Purpose

Defines the public boundary for Giacom Subscription Management CSV ingestion. Callers pass a CSV stream; the ingester returns raw import rows, normalized subscription truth lines, and diagnostic logs. Azure persistence is orchestrated by `ISubscriptionManagementIngestionService` (upload workflow).

## Interface (Application Layer)

```csharp
namespace BillDrift.Application.Import;

public interface ISubscriptionManagementCsvIngester
{
    /// <summary>
    /// Parses SubscriptionManagementReport.csv into raw rows and normalized subscription truth lines.
    /// Never throws for parse failures — inspect <see cref="SubscriptionManagementCsvIngestionResult.Status"/>.
    /// </summary>
    SubscriptionManagementCsvIngestionResult Ingest(
        SubscriptionManagementCsvIngestionRequest request,
        CancellationToken cancellationToken = default);
}

public interface ISubscriptionManagementIngestionService
{
    /// <summary>
    /// Upload workflow: persist source CSV, run ingester, persist results, write table index.
    /// </summary>
    Task<SubscriptionManagementIngestionRun> IngestAndPersistAsync(
        Stream csvContent,
        string? originalFileName,
        CancellationToken cancellationToken = default);
}
```

## Pipeline Stages

Executed sequentially by `SubscriptionManagementCsvIngester`:

| Stage | Responsibility | Failure mode |
|-------|----------------|--------------|
| 1. **Intake** | Validate stream non-empty, size ≤ limit, UTF-8 readable | File fail |
| 2. **Header Detection** | Match required columns via alias map | File fail if mandatory missing |
| 3. **Row Parsing** | CsvHelper → `ParsedSubscriptionManagementRow` | Row skip |
| 4. **Scope Filter** | Exclude non-M365 / non-CSP products (R4) | Scope exclude (counted, not error) |
| 5. **Raw Mapping** | Build `RawSubscriptionManagementRow` + `RawImportId` | Row skip |
| 6. **Flag Normalisation** | Parse NCE/trial booleans (R5) | Warning on unrecognised |
| 7. **Commercial Key Check** | Warn on missing offer/SKU | Warning only |
| 8. **Normalization** | `SubscriptionManagementNormalizer` → `MicrosoftSubscriptionLine` | Per-row normalization skip |
| 9. **Output Assembly** | Build result + summary | — |
| 10. **Logging** | Append `IngestionLogEntry` entries | — |

### Persist Orchestration (`IngestAndPersistAsync`)

| Step | Responsibility |
|------|----------------|
| 1 | Generate `IngestionId`; insert table row `InProgress` |
| 2 | Upload source CSV blob |
| 3 | Run ingester on same bytes |
| 4 | Upload `raw-rows.json`, `subscription-truth.json`, `manifest.json` |
| 5 | Update table row `Completed` / `PartialSuccess` / `Failed` with summary + blob paths |

## Input Guarantees (Caller)

| Requirement | Rule |
|-------------|------|
| Format | CSV with header row; filename typically `SubscriptionManagementReport.csv` |
| Stream | Readable; ingester buffers for hash + parse |
| Encoding | UTF-8 (BOM tolerated) |
| Scope | Microsoft 365 / CSP rows expected; other products excluded |

## Output Guarantees (Ingester)

| Requirement | Rule |
|-------------|------|
| `SourceDocumentId` | SHA-256 hex of CSV bytes |
| `RawImportId` | `GiacomSubscriptionManagement` + file hash + row number |
| Mex ID | Trimmed normal form in `CustomerIdentity`; raw preserved on import row |
| Offer ID + SKU ID | Trimmed normal form in `CommercialKeyRoot` when both present |
| Product names | Preserved in `ProductDisplayFacts`; not used as match keys |
| Determinism | Same bytes + options → identical records and IDs (SC-004) |
| Partial failure | Valid rows emitted when siblings skipped (SC-003) |
| Scope exclusion | Out-of-scope rows absent from output; counted in summary |

## Mandatory Headers

File-level failure (`MandatoryHeaderMissing`) when **no alias matches** for:

| Logical field | Required |
|---------------|----------|
| `MexId` | Yes |
| `Licences` | Yes |
| `Status` | Yes |
| `OfferId` | Yes |
| `SkuId` | Yes |

Optional headers mapped when present — see [subscription-csv-header-map.md](./subscription-csv-header-map.md).

## Normalization Mapping

`SubscriptionManagementNormalizer` applies [001 normalization contract](../../001-billing-domain-model/contracts/normalization.md) plus:

| Raw field | Normalized field | Rule |
|-----------|------------------|------|
| `MexIdRaw` | `Customer.MexId` | Trim, uppercase, validate non-empty |
| `CustomerNameRaw` | `Customer.DisplayName` | Trim; optional |
| `TenantIdRaw` | `Customer.TenantId` | Trim when present |
| `OfferIdRaw`/`SkuIdRaw` | `CommercialKeyRoot` | Trim; both required for normalized line |
| `LicencesRaw` | `LicenceCount` | Parse int ≥ 0 |
| `AssignedLicencesRaw` | `Lifecycle.AssignedLicenceCount` | Parse int when present |
| `TermRaw` | `Term` | Enum map (Annual, Monthly, P1Y, P1M, etc.) |
| `FrequencyRaw` | `Frequency` | Enum map |
| `RenewalDateRaw` | `RenewalDate` | Parse `DateOnly` or null |
| `StatusRaw` | `Status` | Case-insensitive `SubscriptionStatus` map |
| `SupplierSubscriptionIdRaw` | `SupplierSubscriptionId` | Trim when present |
| `IsNceRaw`/`IsTrialRaw` | `Lifecycle.IsNce`/`IsTrial` | Boolean parser (R5) |
| `PriceRaw`/`ErpRaw` | `Lifecycle.Price`/`ErpPrice` | Parse `Money.Gbp` when present |

Rows failing normalization are omitted from `SubscriptionLines` but remain in `RawRows` when raw mapping succeeded.

## Outcome Status

| Condition | `IngestionOutcomeStatus` |
|-----------|--------------------------|
| All qualifying rows extracted, none skipped | `Success` |
| Some rows skipped/excluded but ≥1 emitted | `PartialSuccess` |
| No rows emitted or file unreadable | `Failure` |

## Storage Client Rule

Azure stores (`IIngestionBlobStore`, `IIngestionRunIndexStore`) MUST accept Aspire-injected `BlobServiceClient` and `TableServiceClient` in Infrastructure constructors. **No manual connection string construction.**

## Related Contracts

- [subscription-csv-header-map.md](./subscription-csv-header-map.md)
- [azure-blob-ingestion-archive.md](./azure-blob-ingestion-archive.md)
- [azure-table-ingestion-index.md](./azure-table-ingestion-index.md)
- [product-scope-rules.md](./product-scope-rules.md)
