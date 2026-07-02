# Classification Test Fixtures

Maps JSON fixtures under this directory to `quickstart.md` validation scenarios.

| Fixture | Scenario | Expected |
|---------|----------|----------|
| `classify-csp-full-signals.json` | V1 Microsoft CSP | `MicrosoftCsp` High confidence |
| `internal-customer-no-missing-billing.json` | V2 Internal suppression | Zero `MissingBillingItem` |
| `non-csp-supplier-only.json` | V3 Non-CSP manual review | `NonCspSupplier`, no bill impact |
| `classify-custom-stripe-only.json` | V8 Custom/service | `CustomService` |
| `classify-conservative-partial-sku.json` | V7 Conservative default | `NonCspSupplier` Low |

Programmatic builders in `ClassificationTestDataBuilder` mirror these fixtures for unit tests.
