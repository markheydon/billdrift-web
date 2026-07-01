# Stripe CSV Header Map Contract

**Feature**: `003-stripe-csv-ingestion`  
**Implementation**: `BillDrift.Infrastructure.Import.Stripe.StripeCsvHeaderMap`  
**Related**: [csv-ingestion-pipeline.md](./csv-ingestion-pipeline.md)

## Purpose

Defines the canonical mapping from Stripe dashboard CSV column headers to logical ingestion fields. Matching is **case-insensitive**; first matching alias wins. Unknown columns are preserved in row metadata dictionaries where possible.

## Subscriptions CSV (`StripeCsvFileKind.Subscriptions`)

### Required logical fields

| Logical field | Accepted header aliases (priority order) |
|---------------|------------------------------------------|
| `CustomerId` | `Customer ID`, `customer_id`, `Customer Id`, `cus_id` |
| `SubscriptionId` | `id`, `Subscription ID`, `subscription_id`, `sub_id` |
| `SubscriptionItemId` | `Subscription Item ID`, `subscription_item_id`, `item_id`, `si_id` |
| `ProductId` | `Product ID`, `product_id`, `Product Id` |
| `PriceId` | `Price ID`, `price_id`, `Price Id`, `Plan ID`, `plan_id` |
| `Quantity` | `Quantity`, `quantity`, `Seats`, `seats` |
| `Status` | `Status`, `status`, `Subscription Status` |

### Optional logical fields

| Logical field | Accepted header aliases |
|---------------|-------------------------|
| `CustomerName` | `Customer Name`, `customer_name`, `Customer Description` |
| `ProductName` | `Product Name`, `product_name`, `Plan`, `plan` |
| `UnitAmount` | `Amount`, `amount`, `Unit Amount`, `unit_amount`, `Plan Amount` |
| `Interval` | `Interval`, `interval`, `Billing Interval`, `billing_interval` |
| `Currency` | `Currency`, `currency` |
| `CustomerEmail` | `Customer Email`, `customer_email` |

### Metadata columns

Any header matching these patterns merges into row metadata:

| Pattern | Example headers |
|---------|-----------------|
| `metadata[{key}]` | `metadata[mex_id]`, `metadata[offer_id]` |
| `{known_key}` | `mex_id`, `MexId`, `offer_id`, `OfferId`, `sku_id`, `SkuId` |
| `supplier_*` | `supplier_ref`, `supplier_reference`, `giacom_ref` |

## Products CSV (`StripeCsvFileKind.Products`)

### Required logical fields

| Logical field | Accepted header aliases |
|---------------|-------------------------|
| `ProductId` | `id`, `Product ID`, `product_id` |
| `Name` | `Name`, `name`, `Product Name`, `product_name` |

### Metadata columns

Same metadata patterns as subscriptions file.

## Prices CSV (`StripeCsvFileKind.Prices`)

### Required logical fields

| Logical field | Accepted header aliases |
|---------------|-------------------------|
| `PriceId` | `id`, `Price ID`, `price_id` |
| `ProductId` | `Product ID`, `product_id`, `Product Id` |
| `Currency` | `Currency`, `currency` |

### Optional logical fields

| Logical field | Accepted header aliases |
|---------------|-------------------------|
| `UnitAmount` | `Amount`, `amount`, `Unit Amount`, `unit_amount` |
| `RecurringInterval` | `Interval`, `interval`, `Recurring Interval`, `recurring_interval` |
| `RecurringIntervalCount` | `Interval Count`, `interval_count`, `Recurring Interval Count` |
| `Description` | `Description`, `description`, `Nickname`, `nickname` |

## File-Level Validation

| Condition | Outcome |
|-----------|---------|
| Any **required** logical field has zero alias matches | `MandatoryHeaderMissing` → file fails |
| Header row present, zero data rows | `EmptyFile` → empty collections, informational |
| Duplicate headers after normalisation | Use first occurrence; log warning |

## Single-Item Subscription Fallback

When `SubscriptionItemId` column is absent but `SubscriptionId`, `ProductId`, and `PriceId` are present:

- Emit one `RawStripeSubscriptionItem` per row.
- `SourceLineKey` = `SubscriptionId` (see pipeline contract).

## Fixture Authoring

Test fixtures under `tests/fixtures/stripe-csv/` MUST document which alias set they exercise in a comment row or companion README:

```text
# Fixture: subscriptions-sample-a.csv — uses Stripe Dashboard default headers (2026-Q2)
```

Include at least:

1. **sample-a** — canonical headers, multi-item subscription, full metadata
2. **column-variant** — reordered columns + alternate alias labels (`customer_id` vs `Customer ID`)
3. **partial-metadata** — rows with missing offer_id / sku_id for warning tests
4. **mixed-status** — active + canceled rows for filter tests

## Versioning

When Stripe renames columns in dashboard exports:

1. Add new alias to this contract.
2. Add regression fixture or extend column-variant CSV.
3. Do **not** remove old aliases without a documented deprecation period.
