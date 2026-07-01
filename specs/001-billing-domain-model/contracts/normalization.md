# Normalization Contract

**Consumer**: `BillDrift.Infrastructure` (parsers) → `BillDrift.Application` (normalizers)  
**Domain types**: `BillDrift.Domain.Import` → `BillDrift.Domain.Billing`, `.Mapping`

## Purpose

Defines the boundary between raw imported data and normalized domain entities. Parsers produce `Import.*` types; normalizers produce `Billing.*` types.

## Normalizer Interfaces (Application Layer)

```csharp
namespace BillDrift.Application.Normalization;

public interface IGiacomBillingNormalizer
{
  SupplierCostLine Normalize(RawGiacomBillingLine raw);
}

public interface ISubscriptionManagementNormalizer
{
  MicrosoftSubscriptionLine Normalize(RawSubscriptionManagementRow raw);
}

public interface IPriceListNormalizer
{
  IntendedPrice Normalize(RawPriceListRow raw);
  IntendedPrice Normalize(RawManualPriceEntry raw);
}

public interface IStripeBillingNormalizer
{
  IReadOnlyList<StripeBillingItem> Normalize(
    RawStripeCustomer customer,
    IReadOnlyList<RawStripeSubscription> subscriptions,
    IReadOnlyList<RawStripeSubscriptionItem> items,
    IReadOnlyList<RawStripeProduct> products,
    IReadOnlyList<RawStripePrice> prices);
}
```

## Input Guarantees (from Infrastructure)

| Source | Parser MUST provide |
|--------|---------------------|
| Giacom PDF | Stable `RawImportId`, non-empty `SourceDocumentId` |
| Subscription Management CSV | Header-mapped columns, `RowNumber` for idempotency |
| ResellerPricingVsRRP.csv | Header-mapped columns, `RowNumber` |
| Manual price entry | Operator-provided `EffectiveDate` |
| Stripe export | Valid Stripe object IDs |

## Normalization Rules

### Giacom Billing → `SupplierCostLine`

| Raw field | Normalized field | Rule |
|-----------|------------------|------|
| `MexIdRaw` | `Customer.MexId` | Trim, validate non-empty |
| `ChargeTypeRaw` | `ChargeType` | Map "Pro-rated"/"Prorated" → `ProRatedAdjustment`; else `Recurring` |
| `QuantityRaw` | `Quantity` | Parse int; fail if invalid |
| `PeriodStartRaw`/`PeriodEndRaw` | `BillingPeriod` | Parse `DateOnly` (UK culture default) |
| `LineCostRaw` | `LineCost` | Parse decimal → `Money.Gbp` |

### Subscription Management → `MicrosoftSubscriptionLine`

| Raw field | Normalized field | Rule |
|-----------|------------------|------|
| `StatusRaw` | `SubscriptionStatus` | Case-insensitive enum map |
| `LicencesRaw` | `LicenceCount` | Parse int |
| `OfferIdRaw`/`SkuIdRaw` | `CommercialKeyRoot` | Trim, validate |

### Price List → `IntendedPrice`

| Raw field | Normalized field | Rule |
|-----------|------------------|------|
| All price fields | `Money` | Parse decimal, GBP |
| `StatusRaw` | `PriceListStatus` | Map "End of Sale" → `EndOfSale` |
| — | `PriceSource` | `Catalogue` for CSV; `ManualOverride` for manual entries |

### Stripe → `StripeBillingItem`

- Flatten one `StripeBillingItem` per `RawStripeSubscriptionItem`.
- Join product/price by ID.
- Extract `MexId`, `OfferId`, `SkuId` from item or subscription metadata keys:
  - `mex_id`, `offer_id`, `sku_id` (canonical lowercase)
  - Fallback: `MexId`, `OfferId`, `SkuId` (PascalCase legacy)
- Map Stripe `interval` + `interval_count` → `BillingFrequency` and `Term`.

## Price Resolution Contract

```csharp
public interface IIntendedPriceResolver
{
  /// <summary>
  /// Returns effective intended price for a CommercialKey.
  /// ManualOverride beats Catalogue (FR-010).
  /// </summary>
  IntendedPrice? Resolve(CommercialKey key, IReadOnlyList<IntendedPrice> prices);
}
```

## Product Name Resolution Contract

```csharp
public interface IProductMappingResolver
{
  ProductMappingResolution Resolve(
    string supplierProductName,
    IReadOnlyList<ProductMapping> mappings);

  sealed record ProductMappingResolution(
    ProductMapping? Mapping,
    MappingResolutionStatus Status);

  enum MappingResolutionStatus
  {
    Found,
    NotFound,
    Ambiguous
  }
}
```

## Failure Contract

Normalizers throw `NormalizationException` with:
- `RawImportId` of failing record
- `FieldName`
- `RawValue`
- `Message`

Failed records do not partially populate normalized entities.

## Batch Idempotency

Re-normalizing the same `RawImportId` MUST produce logically equal normalized entities (same business field values; IDs may be stable if normalizer assigns deterministic GUIDs from `RawImportId` — decision for implementation: use `Guid.CreateVersion5` from `RawImportId` for stable entity IDs).
