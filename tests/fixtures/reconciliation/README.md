# Reconciliation Test Fixtures

Sanitized test data for the billing reconciliation engine (`004-reconciliation-engine`).

## Policy

- All customer IDs, Stripe IDs, and product names are synthetic — no production data.
- Fixtures map to validation scenarios in `specs/004-reconciliation-engine/quickstart.md`.

## Fixture Files

| File | Quickstart Scenario | Validates |
|------|---------------------|-----------|
| `clean-match-all-domains.json` | Scenario 1 | All domains aligned, zero mismatches |
| `missing-in-stripe.json` | Scenario 2 | MissingInStripe + CreateMissingItem |
| `quantity-mismatch.json` | Scenario 3 | QuantityMismatch + UpdateQuantity |
| `billing-frequency-mismatch.json` | Scenario 4 | BillingFrequencyMismatch + SwitchPrice |
| `price-mismatch.json` | Scenario 5 | PriceMismatch |
| `catalogue-missing.json` | Scenario 6 | CatalogueMissing + CreateOrUpdateCatalogueEntry |
| `mapping-missing.json` | Scenario 7 | MappingMissing, no bill-impacting actions |
| `mapping-ambiguous.json` | Scenario 8 | MappingAmbiguous |
| `non-csp-supplier-line.json` | Scenario 9 | Non-CSP MappingMissing warning |
| `duplicate-stripe-items.json` | Edge case | Duplicate Stripe → MappingAmbiguous |
| `supplier-orphan-line.json` | US3 scenario 5 | Orphan supplier line |
| `expected/quantity-mismatch-run.json` | Golden run | Deterministic mismatch output |

## Loading Fixtures

Use `ReconciliationInputsFixtureLoader.Load(scenarioName)` in tests, or `ReconciliationTestDataBuilder` for programmatic construction.

## Deferred Scenarios

Full JSON deserialization of all domain value objects is deferred; fixtures use scenario name indirection to `ReconciliationTestDataBuilder` methods until normalizers ship.
