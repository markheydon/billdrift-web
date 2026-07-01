# Implementation Plan: Giacom Supplier Billing PDF Ingestion

**Branch**: `002-giacom-pdf-ingestion` | **Date**: 2026-07-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-giacom-pdf-ingestion/spec.md`

## Summary

Implement a Giacom pre-billing and post-billing PDF ingestion pipeline that extracts supplier cost lines from semi-structured PDFs and emits `RawGiacomBillingLine` records for downstream normalization. Parsing lives in `BillDrift.Infrastructure` using positional text extraction (PdfPig); the public contract lives in `BillDrift.Application.Import`. The pipeline tolerates partial extraction failures, logs skipped blocks/lines, and produces deterministic idempotency keys. No Offer/SKU mapping, normalization implementation, UI, blob storage, or email automation in this feature.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: UglyToad.PdfPig (PDF text + bounding boxes); BCL only in Application contract layer  
**Storage**: N/A — in-memory stream input; blob upload deferred to infrastructure feature  
**Testing**: xUnit + FluentAssertions; PDF fixture regression suite in `BillDrift.Infrastructure.Tests`  
**Target Platform**: Azure (Aspire solution); parser runs anywhere .NET 10 runs  
**Project Type**: Modular .NET Aspire solution — Infrastructure parser + Application import contract  
**Performance Goals**: Parse 500+ lines / 50+ customers in <2 minutes (SC-001); typical monthly PDF <30s  
**Constraints**: Deterministic output for identical PDF bytes (SC-004); open-source dependency only; parser failures isolated per line/block (constitution IV)  
**Scale/Scope**: Single-tenant reseller; 2 report variants (pre/post billing); monthly batch; format drift tolerance without per-month templates

### Dependency on 001-billing-domain-model

| Artifact | Usage |
|----------|-------|
| `RawGiacomBillingLine` | Pipeline output line type |
| `RawImportId` | Line idempotency key (`GiacomBillingPdf` + document ID + line key) |
| `ImportSourceKind.GiacomBillingPdf` | Source kind on every emitted line |
| `contracts/normalization.md` | Parser input guarantees (stable ID, non-empty `SourceDocumentId`) |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Code Quality & Maintainability | ✅ PASS | Parser isolated in `BillDrift.Infrastructure.Import.Giacom`; typed Application contract; stages are single-responsibility |
| II. Testing Standards | ✅ PASS | Integration tests with representative PDF fixtures per format variant; regression required before merge |
| III. Consistent User Experience | ✅ N/A | No UI; log reason codes and outcome status align with future operator terminology (`partial success`, `skipped line`) |
| IV. Security by Design | ✅ PASS | PDF size/page limits at intake; no secrets in logs; password-protected PDFs rejected at document level |
| V. Billing Accuracy & Human Control | ✅ PASS | Extraction only — no Stripe writes; supplier lines explicitly non-authoritative; partial import with visible skips |

### Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I | ✅ PASS | Pipeline stages documented in contract; internal parse types not leaked across assembly boundary |
| II | ✅ PASS | quickstart.md defines fixture-based validation; golden-file comparison for determinism |
| III | ✅ N/A | — |
| IV | ✅ PASS | `IngestionLogEntry` excludes full document content; snippets capped |
| V | ✅ PASS | Re-import produces identical `RawImportId` keys; no silent line drops (FR-023) |

**Gate result**: PASS — proceed to `/speckit-tasks`.

## Project Structure

### Documentation (this feature)

```text
specs/002-giacom-pdf-ingestion/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── pdf-ingestion-pipeline.md
│   └── giacom-block-grammar.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
BillDrift.sln
src/
├── BillDrift.Application/
│   └── Import/
│       ├── IGiacomBillingPdfIngester.cs      # ★ Public ingestion contract
│       ├── GiacomPdfIngestionResult.cs       # ★ Result + log types
│       └── GiacomReportType.cs
├── BillDrift.Infrastructure/
│   └── Import/
│       └── Giacom/
│           ├── GiacomBillingPdfIngester.cs   # ★ Pipeline orchestrator
│           ├── PdfTextExtractor.cs
│           ├── ReportClassifier.cs
│           ├── CustomerBlockSegmenter.cs
│           ├── ProductLineParser.cs
│           ├── ProductNameMerger.cs
│           ├── RawGiacomBillingLineMapper.cs
│           └── Internal/                     # Parse-only types (not public)
│               ├── PdfWord.cs
│               ├── PdfTextLine.cs
│               ├── CustomerBlock.cs
│               └── ParsedProductLine.cs
└── BillDrift.Domain/                         # Existing — no changes required

tests/
├── BillDrift.Infrastructure.Tests/           # ★ NEW — PDF parser integration tests
│   └── Import/
│       └── Giacom/
│           ├── GiacomBillingPdfIngesterTests.cs
│           ├── ReportClassifierTests.cs
│           ├── BlockSegmenterTests.cs
│           └── GoldenFileComparer.cs
└── fixtures/
    └── giacom-pdf/                           # ★ NEW — sanitized PDF fixtures
        ├── pre-billing-sample-a.pdf
        ├── pre-billing-sample-b.pdf
        ├── post-billing-sample-a.pdf
        ├── post-billing-sample-b.pdf
        └── expected/                         # Golden JSON outputs per fixture
            ├── pre-billing-sample-a.json
            └── ...
```

**Structure Decision**: Follow 001 architecture — parsers in Infrastructure, contracts in Application, domain types unchanged. New `BillDrift.Infrastructure.Tests` project holds PDF regression tests (constitution II requires ingestion integration tests). Internal parse types stay in Infrastructure and are not referenced by Application or Domain.

## Complexity Tracking

> No constitution violations requiring justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Phase 0: Research

**Status**: ✅ Complete — see [research.md](./research.md)

Key decisions:
- PdfPig for positional text extraction (R1)
- SHA-256 content hash as `SourceDocumentId` (R2)
- Y-cluster line grouping + header-anchored column detection (R3)
- Customer block boundaries via Mex ID label pattern (R4)
- Continuation-row merge for wrapped product names (R5)
- Positional fallback line key when supplier references absent (R6)
- Skip-line vs skip-block vs fail-document error tiers (R7)

## Phase 1: Design

**Status**: ✅ Complete

| Artifact | Path |
|----------|------|
| Data model | [data-model.md](./data-model.md) |
| Pipeline contract | [contracts/pdf-ingestion-pipeline.md](./contracts/pdf-ingestion-pipeline.md) |
| Block grammar | [contracts/giacom-block-grammar.md](./contracts/giacom-block-grammar.md) |
| Validation quickstart | [quickstart.md](./quickstart.md) |

## Phase 2: Implementation Tasks

**Status**: Pending — run `/speckit-tasks` to generate [tasks.md](./tasks.md)

Expected task groups:
1. Add PdfPig package reference to Infrastructure
2. Define Application import contract and result types
3. Implement PDF text extraction and line grouping
4. Implement report classification (pre/post)
5. Implement customer block segmentation
6. Implement product line parsing and name merging
7. Implement `RawGiacomBillingLine` mapping and idempotency keys
8. Implement pipeline orchestration with partial-failure logging
9. Add `BillDrift.Infrastructure.Tests` with PDF fixtures and golden files
10. Register DI extension for `IGiacomBillingPdfIngester`

## Out of Scope (this feature)

- Blob storage upload and document lifecycle
- Email fetch automation
- `IGiacomBillingNormalizer` implementation (Application, separate task)
- Offer/SKU mapping and CSP classification
- Blazor upload UI
- API endpoint for file upload
- OCR for scanned/image PDFs

## Next Steps

1. `/speckit-tasks` — generate dependency-ordered implementation tasks
2. Obtain sanitized production PDF fixtures (minimum 2 pre + 2 post) before parser implementation
3. `/speckit-implement` — build parser and regression tests
