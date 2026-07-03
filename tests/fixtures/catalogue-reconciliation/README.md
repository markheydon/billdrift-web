# Catalogue Reconciliation Fixtures

JSON scenario descriptors for `CatalogueReconciliationEngineTests` per `specs/011-stripe-catalogue-reconciliation/quickstart.md`.

Each `catalogue-*.json` file names a scenario resolved by `CatalogueInputsFixtureLoader` in `tests/BillDrift.Application.Tests/CatalogueReconciliation/`. Input data is built programmatically via `CatalogueReconciliationTestDataBuilder` (same pattern as reconciliation engine fixtures).

Product mappings reference `tests/fixtures/product-mappings/sample-mappings.json`.
