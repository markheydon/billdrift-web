# Contract: Reseller Pricing CSV Header Map

**Feature**: `010-retail-pricing-ingestion`  
**Implementation**: `BillDrift.Infrastructure.Import.Giacom.RetailPricing.ResellerPricingCsvHeaderMap`  
**Related**: [csv-ingestion-pipeline.md](./csv-ingestion-pipeline.md)

## Purpose

Defines the canonical mapping from Giacom `ResellerPricingVsRRP.csv` column headers to logical ingestion fields. Matching is **case-insensitive**; first matching alias wins.

> **Lock-in requirement**: Validate aliases against sanitized production exports before implementation freeze (see [research.md](../research.md) R15).

## Mandatory Logical Fields

| Logical field | Accepted header aliases (priority order) |
|---------------|------------------------------------------|
| `OfferId` | `Offer ID`, `OfferId`, `Offer_Id`, `Microsoft Offer ID`, `Offer` |
| `SkuId` | `SKU ID`, `SkuId`, `SKU_Id`, `Sku ID`, `SKU`, `Product SKU` |
| `Term` | `Term`, `Contract Term`, `Duration`, `Commitment Term`, `Billing Term` |
| `Frequency` | `Frequency`, `Billing Frequency`, `Bill Frequency`, `Payment Frequency`, `Billing Cycle` |
| `Wholesale` | `Wholesale`, `Wholesale Price`, `Cost`, `Buy Price`, `Partner Price`, `Price` |
| `Rrp` | `RRP`, `Rrp`, `Recommended Retail Price`, `Retail Price`, `List Price`, `Sell Price`, `ERP` |

## Optional Logical Fields

| Logical field | Accepted header aliases |
|---------------|-------------------------|
| `Margin` | `Margin`, `Margin Amount`, `Absolute Margin`, `Profit` |
| `MarginPercent` | `Margin %`, `Margin Percent`, `Margin Percentage`, `MarginPct`, `GP %` |
| `Status` | `Status`, `Product Status`, `Availability`, `State` |
| `Platform` | `Platform`, `Commerce Platform`, `NCE/Legacy`, `Product Platform`, `CSP Platform` |
| `Currency` | `Currency`, `Currency Code`, `Curr` |

## Mandatory Header Gate

Import fails when **any** mandatory logical field has no matching column header.

## Row-Level Rules

| Condition | Action |
|-----------|--------|
| `OfferId` and `SkuId` both blank | Skip row |
| `Wholesale` or `Rrp` blank | Skip row |
| `Term` or `Frequency` blank | Skip row |
| Optional columns absent | Continue; field absent on output |

## Platform Column Values

See [pricing-strategy-rules.md](./pricing-strategy-rules.md) for normalised `PricingPlatform` mapping.

## Fixture Files (tests)

| File | Scenario |
|------|----------|
| `reseller-pricing-sample-a.csv` | Full catalogue with margin + platform |
| `column-variant.csv` | Reordered optional columns |
| `partial-bad-rows.csv` | 5% unparseable amounts (SC-006) |
| `duplicate-keys.csv` | Same commercial key twice |
| `end-of-sale.csv` | EndOfSale status rows |
| `expected/sample-a.json` | Golden normalised output |

Place under `tests/fixtures/reseller-pricing/`.
