# Contract: Azure Table Catalogue Run Index

**Feature**: `012-stripe-catalogue-reconciliation`  
**Storage**: Azure Table Storage via Aspire-injected `TableServiceClient`  
**Date**: 2026-07-03

## Storage Policy

BillDrift v1 uses Azure Table Storage for queryable run indexes. **No SQL database** for catalogue run listing or filtering. Client MUST be Aspire-injected — **no manual connection string construction**.

## Table

**Default name**: `cataloguereconciliationruns` (override via `CatalogueReconciliationStorageOptions.TableName`)

**Client registration** (`BillDrift.Api` only):

```csharp
builder.AddAzureTableServiceClient("tables");
```

**AppHost wiring** (existing pattern):

```csharp
var tables = storage.AddTables("tables");
api.WithReference(tables);
```

**Store constructor**: `AzureCatalogueReconciliationStore` accepts `TableServiceClient` via constructor DI alongside `BlobServiceClient` — shared class, **no manual connection strings**.

---

## Entity: `CatalogueRunIndexEntity`

| Property | Type | Notes |
|----------|------|-------|
| `PartitionKey` | `string` | Constant: `catalogue` |
| `RowKey` | `string` | `{CatalogueRunId:D}` |
| `ExecutedAt` | `DateTimeOffset` | UTC |
| `StripeIngestionRunId` | `string?` | GUID string |
| `PricingIngestionRunId` | `string?` | GUID string |
| `MappingVersionId` | `string?` | |
| `TotalExceptions` | `int` | |
| `MissingProductCount` | `int` | |
| `MissingPriceCount` | `int` | |
| `IncorrectPriceCount` | `int` | |
| `DuplicateCount` | `int` | Product + price duplicates |
| `UnmappedCount` | `int` | |
| `ActionableFixCount` | `int` | |
| `BlobManifestPath` | `string` | `{catalogueRunId}/manifest.json` |
| `Status` | `string` | `Completed` / `Failed` |

---

## Queries

| Operation | Filter |
|-----------|--------|
| List recent runs | `PartitionKey eq 'catalogue'` ORDER BY `ExecutedAt` DESC |
| Get by ID | Partition + RowKey point read |

Pagination: `$top` + continuation token; default page size 20.

---

## Registration

```csharp
// Program.cs — order matters: Aspire clients before storage extension
builder.AddAzureTableServiceClient("tables");
builder.AddAzureBlobServiceClient("blobs");
services.AddCatalogueReconciliation();
services.AddCatalogueReconciliationStorage();
```

`AddCatalogueReconciliationStorage` registers `ICatalogueReconciliationStore` → `AzureCatalogueReconciliationStore` when not in test override.

---

## Test Override

Integration tests may register `InMemoryCatalogueReconciliationStore` instead of `AzureCatalogueReconciliationStore` — no Aspire clients required for engine-only tests.
