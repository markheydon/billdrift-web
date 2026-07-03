# Contract: Azure Blob Catalogue Run Archive

**Feature**: `012-stripe-catalogue-reconciliation`  
**Storage**: Azure Blob Storage via Aspire-injected `BlobServiceClient`  
**Date**: 2026-07-03

## Storage Policy

BillDrift v1 uses Azure Blob Storage and Azure Table Storage exclusively for catalogue run persistence. **No SQL.** Clients MUST be obtained via Aspire DI — **no manual connection string construction**.

## Container

**Default name**: `catalogue-reconciliation-runs` (override via `CatalogueReconciliationStorageOptions.BlobContainerName`)

Create container idempotently on first write.

**Client registration** (`BillDrift.Api` only):

```csharp
builder.AddAzureBlobServiceClient("blobs");
```

**AppHost wiring** (existing pattern):

```csharp
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var blobs = storage.AddBlobs("blobs");
api.WithReference(blobs);
```

**Store constructor**:

```csharp
public AzureCatalogueReconciliationStore(
    BlobServiceClient blobServiceClient,
    TableServiceClient tableServiceClient,
    IOptions<CatalogueReconciliationStorageOptions> options)
```

**Prohibited**: `new BlobServiceClient(connectionString)`, reading `AzureWebJobsStorage` or similar env vars directly in Infrastructure.

---

## Path Layout

```text
{catalogueRunId}/
├── manifest.json
├── inputs/
│   ├── stripe-products.json
│   ├── stripe-prices.json
│   ├── intended-pricing.json
│   └── product-mappings.json
└── results/
    ├── exceptions.json
    ├── proposed-fixes.json
    └── summary.json
```

`catalogueRunId` = `CatalogueRunId.Value.ToString("D")`.

---

## Manifest Schema (`manifest.json`)

```json
{
  "schemaVersion": 1,
  "catalogueRunId": "00000000-0000-0000-0000-000000000000",
  "archivedAt": "2026-07-03T12:00:00Z",
  "inputReferences": {
    "stripeIngestionRunId": "00000000-0000-0000-0000-000000000001",
    "pricingIngestionRunId": "00000000-0000-0000-0000-000000000002",
    "mappingVersionId": "2026-07-03",
    "mappingContentHash": "sha256:..."
  },
  "inputs": {
    "stripeProducts": { "blobPath": "inputs/stripe-products.json", "recordCount": 120 },
    "stripePrices": { "blobPath": "inputs/stripe-prices.json", "recordCount": 240 },
    "intendedPricing": { "blobPath": "inputs/intended-pricing.json", "recordCount": 180 },
    "productMappings": { "blobPath": "inputs/product-mappings.json", "recordCount": 95 }
  },
  "results": {
    "exceptions": { "blobPath": "results/exceptions.json", "recordCount": 12 },
    "proposedFixes": { "blobPath": "results/proposed-fixes.json", "recordCount": 10 },
    "summary": { "blobPath": "results/summary.json" }
  }
}
```

---

## Serialization

- `System.Text.Json` source-generated context in `BillDrift.Infrastructure.CatalogueReconciliation`
- Camel case property names
- `Money` serialized as `{ "amountMinor": 1099, "currency": "GBP" }`

---

## In-Memory Test Store

`InMemoryCatalogueReconciliationStore` mirrors path layout in `Dictionary<string, byte[]>` for unit tests without Azurite.
