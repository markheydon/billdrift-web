# Contract: Pricing Strategy Rules

**Feature**: `010-retail-pricing-ingestion`  
**Implementation**: `BillDrift.Application.Normalization.IntendedPriceResolver` + `RetailPricingIngestionService` merge stage

## Purpose

Documents business rules for resolving the **effective intended retail price** from catalogue and manual override inputs. These rules satisfy spec FR-014–FR-018 and align with 001 domain precedence (FR-010).

## Default Strategy

| Source | Effective retail charge | Classification |
|--------|------------------------|----------------|
| Catalogue row (`PriceSource.Catalogue`) | **RRP** from price list | `ProductClassification.Csp` |
| Manual override (`PriceSource.ManualOverride`) | **RRP** from operator entry | `ProductClassification.NonCsp` |

Wholesale from catalogue is retained for **margin analysis** against supplier PDF costs; it does not define the customer charge.

## Precedence

```
IF manual override exists for CommercialKey
  THEN effective RRP = manual override RRP
       classification = NonCsp
ELSE IF catalogue row exists for CommercialKey
  THEN effective RRP = catalogue RRP
       classification = Csp
ELSE
  THEN no IntendedPrice in resolved output
```

Manual override wins **even when** a catalogue row exists for the same key (spec US3 scenario 2).

## Catalogue-Only Keys

Products present only in `ResellerPricingVsRRP.csv`:
- Emit single resolved `IntendedPrice` with `PriceSource.Catalogue`.
- `OverrideWinsCount` unchanged.

## Override-Only Keys (Bespoke / Non-CSP)

Products **absent** from catalogue but with manual override:
- Emit resolved `IntendedPrice` with `PriceSource.ManualOverride`.
- `ProductClassification.NonCsp` — signals downstream classification (`NonCspManualReview` path in 006).
- No catalogue gap invented.

## Keys With Neither Source

Commercial keys referenced by subscription truth or Stripe but lacking both catalogue and override:
- **Absent** from `ResolvedPrices`.
- Reconciliation reports `CatalogueMissing` / pricing gaps (004) — out of scope for ingestion to invent values.

## End-of-Sale Catalogue Rows

| Field | Behaviour |
|-------|-----------|
| `PriceListStatus` | `EndOfSale` |
| Effective RRP | Still catalogue RRP |
| Reconciliation | Status surfaced for operator review; price comparison still runs |

## Platform Classification

| `PricingPlatform` | Typical source |
|-------------------|----------------|
| `Nce` | Platform column = NCE variants |
| `Legacy` | Platform column = Legacy variants |
| `Unknown` | Column absent or unrecognised |

Platform does **not** change RRP precedence; informational for operator and classification signals.

## Duplicate Catalogue Rows

Within one CSV upload, duplicate normalized `CommercialKey`:
- **Last row wins** for catalogue sourcing.
- Log `DuplicateCommercialKey` warning with row numbers.
- Manual override still beats winning catalogue row after duplicate resolution.

## Manual Override Validation

| Field | Rule |
|-------|------|
| `Rrp` | Required; parseable decimal |
| `Term`, `Frequency` | Required; mappable to enums |
| `OfferId` / `SkuId` | At least one required; both preferred |
| `Reason` | Required non-empty |
| `EffectiveDate` | Required |
| `Wholesale` | Optional |

Rejected overrides increment `ManualOverridesRejected`; do not fail entire import unless all overrides rejected and zero catalogue rows succeed.

## Resolution Summary Counters

| Counter | Meaning |
|---------|---------|
| `CatalogueOnlyCount` | Keys resolved from catalogue only |
| `OverrideWinsCount` | Keys where manual beat catalogue |
| `ResolvedPriceCount` | Distinct keys in `ResolvedPrices` |

## Downstream Consumers

| Consumer | Uses |
|----------|------|
| `IntendedPriceIndex` | `ResolvedPrices` for match groups |
| `MismatchDetector` | `IntendedPrice.Rrp` vs Stripe unit amount |
| Catalogue checks (004) | `IntendedPrice.Rrp` vs Stripe price catalogue |
| `PricingDriftAnalyzer` (008) | `PriceSource` + RRP timeline |
| `ClassificationRuleEngine` (006) | `ProductClassification.NonCsp` on manual overrides |
