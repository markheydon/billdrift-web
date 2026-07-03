# Subscription Management CSV Fixtures

Synthetic fixtures for Giacom `SubscriptionManagementReport.csv` ingestion tests.

Production CSV exports were unavailable when this feature was implemented; fixtures exercise header alias variants and scope rules documented in `specs/009-giacom-subscription-csv/contracts/`.

| Fixture | Purpose |
|---------|---------|
| `subscription-management-sample-a.csv` | Multi-customer M365 happy path (Scenario 1) |
| `mixed-products.csv` | M365 + Exclaimer scope filter (Scenario 2) |
| `column-variant.csv` | Alternate header aliases (Scenario 5) |
| `partial-success.csv` | Missing Mex ID and bad licence count (Scenario 5) |
| `lifecycle-columns.csv` | NCE/trial/pricing lifecycle columns (Scenario 6) |

Golden expected output lives under `expected/`. Regenerate with test helper `GoldenFileComparer.WriteGoldenFile` when parser behaviour is intentionally changed.

**Commit policy**: Only sanitized synthetic data — no production customer names, tenant IDs, or commercial keys from live exports.
