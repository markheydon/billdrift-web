# Quickstart: Stripe Catalogue Reconciliation

**Feature**: `012-stripe-catalogue-reconciliation`  
**Branch**: `012-stripe-catalogue-reconciliation`

Validation guide for catalogue reconciliation engine, Azure persistence (blob + table only, Aspire DI), API, and approval integration. Implementation details live in [plan.md](./plan.md) and [contracts/](./contracts/).

---

## Prerequisites

- .NET 10 SDK
- Azurite running (Aspire AppHost or standalone) for storage integration tests
- Completed ingestion fixtures:
  - Stripe products + prices CSV (003) archived in blob store
  - Retail pricing resolved prices (010) archived in blob store
  - Product mapping JSON snapshot

```powershell
cd D:\repos\markheydon\billdrift-web
dotnet build
```

**Storage policy**: BillDrift v1 uses Azure Blob + Azure Table only. API registers `AddAzureBlobServiceClient("blobs")` and `AddAzureTableServiceClient("tables")` — no SQL, no manual `BlobServiceClient` construction.

---

## Unit Tests (no Azure)

```powershell
dotnet test tests/BillDrift.Application.Tests --filter "FullyQualifiedName~CatalogueReconciliation" --no-build
```

### Scenarios (engine only)

| # | Fixture | Expected outcome | Spec / SC |
|---|---------|------------------|-----------|
| 1 | `catalogue-clean-match.json` | Zero exceptions | SC-002 baseline |
| 2 | `catalogue-missing-product.json` | `MissingProduct` + `CreateProduct` fix | US1 |
| 3 | `catalogue-missing-price.json` | `MissingPrice` + `CreatePrice` fix | US1 |
| 4 | `catalogue-incorrect-price.json` | `IncorrectPrice` + `CreateReplacementPrice` | US1, FR-018 |
| 5 | `catalogue-duplicate-products.json` | `DuplicateProduct` + manual cleanup only | US2, SC-003 |
| 6 | `catalogue-duplicate-prices.json` | `DuplicatePrice` + manual cleanup only | US2 |
| 7 | `catalogue-pricing-gap.json` | `PricingReferenceGap`; no price checks | US4 |
| 8 | `catalogue-unmapped-stripe.json` | `UnmappedCatalogueEntry` warnings | US4 |
| 9 | `catalogue-manual-override-rrp.json` | Uses override RRP not catalogue row | Assumptions |
| 10 | `catalogue-determinism.json` | Two runs → identical output | SC-004, FR-021 |

Fixtures location: `tests/fixtures/catalogue-reconciliation/`

---

## Integration Tests (Azurite)

```powershell
dotnet test tests/BillDrift.Infrastructure.Tests --filter "FullyQualifiedName~CatalogueReconciliation" --no-build
```

| # | Scenario | Validates |
|---|----------|-----------|
| 11 | Persist run → list → detail | Blob archive + table index via DI-injected clients |
| 12 | Re-read blob manifest | Path layout contract |
| 13 | Ingestion run ID resolution | Loads products/prices from 003 archive |

---

## API Validation (development)

Start AppHost:

```powershell
dotnet run --project src/BillDrift.AppHost
```

### Trigger catalogue run

```http
POST https://localhost:{api-port}/api/catalogue-reconciliation/runs
Content-Type: application/json

{
  "stripeIngestionRunId": "{guid-from-stripe-ingest}",
  "pricingIngestionRunId": "{guid-from-pricing-ingest}",
  "productMappings": [ ],
  "options": { "exactAmountMatch": true }
}
```

**Expected**: `201` with summary counts matching fixture expectations.

### List runs

```http
GET /api/catalogue-reconciliation/runs?limit=10
```

**Expected**: Latest run appears with exception counts (from table index).

### Ingest to approval queue

```http
POST /api/catalogue-reconciliation/runs/{catalogueRunId}/ingest-approvals
```

**Expected**: Actionable fixes appear in `/api/approval/proposals` with `category: Catalogue`.

---

## Approval Integration Check

```powershell
dotnet test tests/BillDrift.Application.Tests --filter "FullyQualifiedName~CatalogueApproval" --no-build
```

| # | Scenario | SC |
|---|----------|-----|
| 14 | Unapproved fixes excluded from export | SC-006 |
| 15 | Manual cleanup proposals not exportable | FR-019 |

---

## Success Criteria Mapping

| Criterion | Validation |
|-----------|------------|
| SC-001 | Review run detail JSON for 500-product fixture < 15 min manually |
| SC-002 | Scenarios 2–4 detect all injected gaps |
| SC-003 | Scenarios 5–6; no auto-merge proposals |
| SC-004 | Scenario 10 |
| SC-005 | UAT: operator identifies action from proposal `rationale` |
| SC-006 | Scenarios 14–15 |

---

## Troubleshooting

| Symptom | Check |
|---------|-------|
| Empty exception set but catalogue wrong | Stripe ingestion missing products/prices files |
| All `PricingReferenceGap` | Pricing ingestion run ID wrong or resolved-prices blob empty |
| Azurite connection failures | AppHost storage emulator running; API has `tables` + `blobs` references |
| Storage client not registered | `Program.cs` must use `AddAzureBlobServiceClient` / `AddAzureTableServiceClient` — no manual client construction |
| `InvalidOperationException` on store | Verify `AddCatalogueReconciliationStorage()` registered after Aspire client registration |

---

## Notes

Validated via unit and integration tests on 2026-07-03:

- Engine scenarios 1–10 covered by `CatalogueReconciliationEngineTests`, `DeterminismTests`, and `CatalogueInputsFixtureLoader`.
- Azurite integration test `AzureCatalogueReconciliationStoreTests` skips when storage emulator unavailable.
- `CatalogueReconciliationServiceTests` verifies Stripe and pricing ingestion run IDs resolve via `IIngestionBlobStore`.
- Stripe catalogue blobs use `{ingestionId}/result/stripe-catalogue-products.json` and `stripe-catalogue-prices.json` in the ingestion archive container (Aspire-injected `BlobServiceClient` only).
- Inline `stripeProducts` / `stripePrices` on the API take precedence over `stripeIngestionRunId`.
- Blazor UI deferred per plan; API-only v1.

---

## Related Documents

- [catalogue-reconciliation-pipeline.md](./contracts/catalogue-reconciliation-pipeline.md)
- [catalogue-check-rules.md](./contracts/catalogue-check-rules.md)
- [azure-blob-catalogue-run-archive.md](./contracts/azure-blob-catalogue-run-archive.md)
- [azure-table-catalogue-run-schema.md](./contracts/azure-table-catalogue-run-schema.md)
- [approval-integration.md](./contracts/approval-integration.md)
