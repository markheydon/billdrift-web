# Reseller pricing CSV fixtures

Sanitized `ResellerPricingVsRRP.csv` samples for retail pricing ingestion tests.

| File | Scenario |
|------|----------|
| `reseller-pricing-sample-a.csv` | Full catalogue with margin and platform columns |
| `column-variant.csv` | Reordered optional columns |
| `partial-bad-rows.csv` | Rows with missing commercial key and unparseable wholesale |
| `duplicate-keys.csv` | Same commercial key twice (last row wins) |
| `end-of-sale.csv` | End-of-sale status row retains RRP |
| `headers-only.csv` | Header row with no data rows |

Golden expected output lives under `expected/`. Do not commit production exports without sanitization.
