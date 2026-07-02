# Stripe CSV Test Fixtures

Sanitized synthetic Stripe dashboard exports for regression tests.

## Fixtures

| File | Purpose |
|------|---------|
| `subscriptions-sample-a.csv` | Canonical headers, multi-item subscription, full metadata |
| `products-sample-a.csv` | Products catalogue matching sample-a |
| `prices-sample-a.csv` | Prices catalogue matching sample-a |
| `subscriptions-column-variant.csv` | Reordered columns + `customer_id` alias |
| `subscriptions-mixed-status.csv` | Active + canceled rows for filter tests |
| `subscriptions-partial-metadata.csv` | Rows with missing offer_id / sku_id |
| `subscriptions-partial-success.csv` | One bad quantity row + valid siblings |

## Expected outputs

Golden JSON files live under `expected/`. Regenerate with test helper `WriteGoldenFile` after validating parser output.

## Commit policy

Use sanitized or synthetic data only — no production customer emails or payment details.
