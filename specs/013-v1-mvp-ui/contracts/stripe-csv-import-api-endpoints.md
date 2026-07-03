# Contract: Stripe CSV Import API Endpoints

**Feature**: `013-v1-mvp-ui`  
**Project**: `BillDrift.Api`  
**Date**: 2026-07-03

## Purpose

Expose existing `IStripeBillingCsvIngester` + `IStripeBillingNormalizer` through HTTP. Persist normalized Stripe billing items and catalogue snapshots for reconciliation (004) and catalogue reconciliation (012).

---

## Routes

Base: `/api/imports/stripe-csv`

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/` | Upload and ingest Stripe CSV bundle |
| `GET` | `/` | List recent Stripe CSV ingestion runs |
| `GET` | `/{ingestionId:guid}` | Get run detail |
| `GET` | `/{ingestionId:guid}/billing` | Get normalized Stripe billing items |
| `GET` | `/{ingestionId:guid}/catalogue` | Get Stripe products + prices snapshot |

---

## POST `/`

**Request**: `multipart/form-data`

| Field | Required | Description |
|-------|----------|-------------|
| `subscriptions` | Yes | `subscriptions.csv` |
| `products` | No | `products.csv` |
| `prices` | No | `prices.csv` |

**Validation**:
- `subscriptions` required
- Per-file size limits per `StripeCsvIngestionOptions`
- CSV content type or `.csv` extension

**Behaviour**:
1. `StripeCsvIngestionService.IngestAndPersistAsync(files)`
2. Parse via `IStripeBillingCsvIngester`
3. Normalize via `IStripeBillingNormalizer`
4. Persist source files + normalized billing items
5. Call `IIngestionBlobStore.PersistStripeCatalogueAsync` when products/prices present
6. Index run

**Response** `200 OK`: `StripeCsvIngestionRun`

**Errors**:

| Status | Condition |
|--------|-----------|
| 400 | Missing subscriptions file |
| 413 | File too large |
| 422 | Parse failed — include diagnostic summary |

---

## GET `/`

**Query**: `take` (optional, default 20)

**Response** `200 OK`: `StripeCsvIngestionRun[]`

---

## GET `/{ingestionId}`

**Response** `200 OK`: `StripeCsvIngestionRun`  
**Response** `404`: Not found

---

## GET `/{ingestionId}/billing`

**Response** `200 OK`: `StripeBillingItem[]`  
**Response** `404`: Not found

---

## GET `/{ingestionId}/catalogue`

**Response** `200 OK`:

```json
{
  "products": [ /* StripeCatalogueProduct[] */ ],
  "prices": [ /* StripeCataloguePrice[] */ ]
}
```

**Response** `404`: Not found or catalogue not uploaded

---

## Service Registration

```csharp
builder.Services.AddStripeBillingCsvIngestion(); // existing
builder.Services.AddScoped<IStripeCsvIngestionService, StripeCsvIngestionService>(); // new

app.MapStripeCsvImportEndpoints();
```

---

## Integration with Catalogue Reconciliation (012)

Catalogue reconciliation request may reference `stripeIngestionRunId` — this endpoint populates the blobs that `CatalogueReconciliationService` loads via `IIngestionBlobStore.GetStripeCatalogueProductsAsync` / `GetStripeCataloguePricesAsync`.

---

## Notes

- Subscriptions-only upload supported (matches 003).
- Deterministic output per 003 contract unchanged.
- No Stripe API calls.
