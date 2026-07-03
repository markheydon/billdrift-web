# Contract: Giacom PDF Import API Endpoints

**Feature**: `013-v1-mvp-ui`  
**Project**: `BillDrift.Api`  
**Date**: 2026-07-03

## Purpose

Expose existing `IGiacomBillingPdfIngester` + `IGiacomBillingNormalizer` through HTTP, following the Subscription Management import pattern (009).

---

## Routes

Base: `/api/imports/giacom-pdf`

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/` | Upload and ingest a Giacom billing PDF |
| `GET` | `/` | List recent PDF ingestion runs |
| `GET` | `/{ingestionId:guid}` | Get run detail |
| `GET` | `/{ingestionId:guid}/supplier-cost` | Get normalized supplier cost lines |

---

## POST `/`

**Request**: `multipart/form-data` with field `file` (PDF)

**Validation**:
- File required, non-empty
- Max size 20 MB (matches 002 intake)
- Content type `application/pdf` preferred; validate magic bytes

**Behaviour**:
1. `GiacomPdfIngestionService.IngestAndPersistAsync(stream, fileName)`
2. Parse via `IGiacomBillingPdfIngester`
3. Normalize via `IGiacomBillingNormalizer`
4. Persist source + normalized supplier cost lines to blob store
5. Index run in `IIngestionRunIndexStore`

**Response** `200 OK`: `GiacomPdfIngestionRun`

**Errors**:

| Status | Condition |
|--------|-----------|
| 400 | Missing file |
| 413 | File too large |
| 422 | Parse failed (zero valid lines) — include `FailureReason` and diagnostic summary |

---

## GET `/`

**Query**: `take` (optional, default 20)

**Response** `200 OK`: `GiacomPdfIngestionRun[]` (most recent first)

---

## GET `/{ingestionId}`

**Response** `200 OK`: `GiacomPdfIngestionRun`  
**Response** `404`: Run not found

---

## GET `/{ingestionId}/supplier-cost`

**Response** `200 OK`: `SupplierCostLine[]`  
**Response** `404`: Run or normalized data not found

---

## Service Registration

```csharp
// Program.cs
builder.Services.AddGiacomBillingPdfIngestion(); // existing
builder.Services.AddScoped<IGiacomPdfIngestionService, GiacomPdfIngestionService>(); // new orchestrator

app.MapGiacomPdfImportEndpoints();
```

---

## Dependencies

| Service | Role |
|---------|------|
| `IGiacomBillingPdfIngester` | Parse PDF (002) |
| `IGiacomBillingNormalizer` | Normalize to `SupplierCostLine` (001) |
| `IIngestionBlobStore` | Persist source + results (extend if needed) |
| `IIngestionRunIndexStore` | Run index (009 pattern) |

---

## Notes

- No new domain parsing rules — ingester behaviour unchanged.
- `SourceDocumentId` fingerprint stable per 002 contract.
- Operator context from `IOperatorContext` for audit (future).
