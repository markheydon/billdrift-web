# Contract: Azure Table Catalogue Run Index

**Feature**: `011-stripe-catalogue-reconciliation`  
**Storage**: Azure Table Storage via Aspire-injected `TableServiceClient`  
**Date**: 2026-07-03

## Table

**Default name**: `cataloguereconciliationruns` (override via `CatalogueReconciliationStorageOptions.TableName`)

**Client registration** (API only):

```csharp
builder.AddAzureTableServiceClient("tables");
```

Store constructor: `AzureCatalogueReconciliationStore(TableServiceClient client, ...)` — shared class with blob store, **no manual connection strings**.

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
services.AddCatalogueReconciliation();
services.AddCatalogueReconciliationStorage();
```

`AddCatalogueReconciliationStorage` registers `ICatalogueReconciliationStore` → `AzureCatalogueReconciliationStore` when not in test override.
