# PDF Ingestion Pipeline Contract

**Feature**: `002-giacom-pdf-ingestion`  
**Consumer**: `BillDrift.Api` / future upload UI → `BillDrift.Infrastructure`  
**Producer output**: `BillDrift.Domain.Import.RawGiacomBillingLine` via `GiacomPdfIngestionResult`

## Purpose

Defines the public boundary for Giacom billing PDF ingestion. Callers pass a PDF stream; the ingester returns structured raw import lines plus diagnostic logs. Normalization to `SupplierCostLine` is **out of scope** — handled by `IGiacomBillingNormalizer` per [001 normalization contract](../../001-billing-domain-model/contracts/normalization.md).

## Interface (Application Layer)

```csharp
namespace BillDrift.Application.Import;

public interface IGiacomBillingPdfIngester
{
    /// <summary>
    /// Parses a Giacom pre-billing or post-billing PDF and returns raw import lines.
    /// Never throws for parse failures — inspect <see cref="GiacomPdfIngestionResult.Status"/>.
    /// </summary>
    GiacomPdfIngestionResult Ingest(Stream pdfStream, CancellationToken cancellationToken = default);
}
```

## Pipeline Stages

Executed sequentially by `GiacomBillingPdfIngester`:

| Stage | Responsibility | Failure mode |
|-------|----------------|--------------|
| 1. **Intake** | Validate stream non-empty, size ≤ 20 MB, readable PDF | Document fail |
| 2. **Document Classification** | Detect pre/post billing report type | Continue with `Unknown` |
| 3. **Page Extraction** | PdfPig: words + positions per page; enforce page limit | Document fail |
| 4. **Line Grouping** | Cluster words into `PdfTextLine` by Y tolerance | Document fail if no text |
| 5. **Column Detection** | Derive column X-ranges from header labels | Warning if headers weak; best-effort |
| 6. **Block Segmentation** | Split into `CustomerBlock` by Mex ID header pattern | Document fail if zero blocks |
| 7. **Line Parsing** | Map rows to `ParsedProductLine` fields by column | Line/block skip |
| 8. **Name Merge** | Merge continuation rows into product names | — |
| 9. **Transformation** | Map to `RawGiacomBillingLine` + `RawImportId` | Line skip |
| 10. **Output Assembly** | Build `GiacomPdfIngestionResult` with summary | — |
| 11. **Logging** | Append `IngestionLogEntry` for all skips/warnings | — |

## Input Guarantees (Caller)

| Requirement | Rule |
|-------------|------|
| Stream | Seekable preferred; ingester may buffer to memory for hash + re-read |
| Format | PDF 1.x+; unencrypted |
| Content | Giacom supplier billing (pre or post) |

## Output Guarantees (Ingester)

| Requirement | Rule |
|-------------|------|
| `SourceDocumentId` | SHA-256 hex of PDF bytes, stable across re-import |
| `RawImportId` | `ImportSourceKind.GiacomBillingPdf` + document ID + line key |
| `SourceDocumentId` on each line | Non-empty; matches result document ID |
| Product names | Character-preserving copy from PDF (SC-005) |
| Offer/SKU | MUST NOT be populated — not on PDF |
| Determinism | Same bytes → identical lines and IDs (SC-004) |
| Partial failure | Valid lines emitted even when siblings skipped (SC-003) |

## Line Key Resolution

```
IF first supplier reference ID present AND non-empty
  SourceLineKey = referenceId
ELSE
  SourceLineKey = "{page}:{blockIndex}:{lineIndex}"
```

## Dependency Injection Registration

```csharp
namespace BillDrift.Infrastructure.Import.Giacom;

public static class GiacomImportServiceCollectionExtensions
{
    public static IServiceCollection AddGiacomBillingPdfIngestion(this IServiceCollection services)
    {
        services.AddSingleton<IGiacomBillingPdfIngester, GiacomBillingPdfIngester>();
        return services;
    }
}
```

## Exception Policy

| Condition | Behavior |
|-----------|----------|
| Parse/validation failures | Return `GiacomPdfIngestionResult` with `Failure` or `PartialSuccess` |
| `cancellationToken` cancelled | Throw `OperationCanceledException` |
| Null stream | Throw `ArgumentNullException` (programmer error) |

Ingester MUST NOT throw `NormalizationException` — normalization is a separate stage.

## Handoff to Normalization

```text
GiacomPdfIngestionResult.Lines
  → foreach line: IGiacomBillingNormalizer.Normalize(line)
  → SupplierCostLine (Application layer, separate implementation)
```

Ingestion result log entries are **not** passed to normalizer; operators review via future UI.

## Performance Contract

| Metric | Target |
|--------|--------|
| 500 lines / 50 customers | < 2 minutes (SC-001) |
| Typical monthly PDF | < 30 seconds (design target) |
| Memory | Stream buffered once; no full-document string retention after parse |

## Security Contract

- Reject encrypted PDFs at intake (`IngestionFailureReason.DocumentEncrypted`).
- Cap file size and page count (see data-model.md).
- Log snippets capped at 200 characters; no full document logging.

## Test Contract

Integration tests MUST verify:

1. Golden-file field equality per fixture PDF.
2. Deterministic re-parse (deep equal).
3. Partial-success fixture (valid lines + skip logs).
4. Encrypted PDF → `Failure`.
5. Report type classification for pre/post fixtures.

## Related Artifacts

- [giacom-block-grammar.md](./giacom-block-grammar.md) — segmentation and column rules
- [data-model.md](../data-model.md) — type definitions
- [../001-billing-domain-model/contracts/normalization.md](../001-billing-domain-model/contracts/normalization.md) — downstream normalizer
