# Mismatch-to-Exception Mapping

**Consumer**: `BillDrift.Application.Reconciliation.ExceptionSurfacing.Mapping.MismatchToExceptionMapper`  
**Extends**: [004 mismatch-rules](../../004-reconciliation-engine/contracts/mismatch-rules.md)

## Purpose

Defines the deterministic mapping from engine `MismatchType` records to operator-facing `ExceptionCategory` and `ReconciliationDomain`, including catalogue subdivision and non-CSP branching.

---

## Primary Mapping Table

| MismatchType | ExceptionCategory | ReconciliationDomain | Notes |
|--------------|-------------------|----------------------|-------|
| `MissingInStripe` | `MissingBillingItem` | `TruthVsStripe` | |
| `QuantityMismatch` | `QuantityLicenceMismatch` | `TruthVsStripe` | |
| `BillingFrequencyMismatch` | `BillingFrequencyMismatch` | `TruthVsStripe` | |
| `PriceMismatch` | `StripePriceRrpMismatch` | `TruthVsStripe` | Subscription-level price delta |
| `MappingAmbiguous` | `OfferSkuAmbiguousMapping` | `SupplierCostVsMapping` | |
| `MappingMissing` | See branching below | `SupplierCostVsMapping` | |
| `CatalogueMissing` | See subdivision below | `PricingVsCatalogue` | |

---

## MappingMissing Branching

| Condition | ExceptionCategory |
|-----------|-------------------|
| Description contains `"Non-CSP"` (case-insensitive) OR involved supplier line has `ProductClassification.NonCsp` | `NonCspManualReview` |
| Involved entity is supplier cost line with unknown customer | `MexIdMismatch` if MexId conflict detected; else `OfferSkuAmbiguousMapping` |
| Default | `OfferSkuAmbiguousMapping` |

---

## CatalogueMissing Subdivision

Evaluate using `EntityMatchGroup` attachments for the mismatch's match group:

| Condition | ExceptionCategory |
|-----------|-------------------|
| No Stripe product snapshot exists for offer/SKU root in catalogue index evidence | `StripeProductMissing` |
| Stripe product exists but no price for required term/frequency | `StripePriceMissing` |
| Stripe price exists but amount ≠ intended RRP beyond tolerance | `StripePriceRrpMismatch` |
| Cannot determine (no match group) | `StripePriceMissing` (default) |

Subdivision uses same tolerance as `run` options / mismatch `ExpectedValue`/`ActualValue` when group attachments unavailable.

---

## Severity Mapping

| MismatchSeverity | ExceptionSeverity |
|------------------|-------------------|
| `Info` | `Info` |
| `Warning` | `Warning` |
| `Error` | `Error` |

No severity adjustment at mapping stage; suppression may clear `ProposedChangeId` but does not downgrade severity.

---

## Explanation Templates

Mapper preserves engine `Mismatch.Description` as base `Explanation`. Prefix by category when empty:

| Category | Default prefix |
|----------|----------------|
| `MissingBillingItem` | `Subscription should be billed in Stripe but no matching item was found.` |
| `OrphanedBillingItem` | `Stripe is billing an item with no matching active subscription.` |
| `QuantityLicenceMismatch` | `Licence count in Stripe does not match subscription truth.` |
| `BillingFrequencyMismatch` | `Billing frequency in Stripe does not match the subscription term.` |
| `StripePriceRrpMismatch` | `Stripe unit price does not match intended retail price.` |
| `StripeProductMissing` | `No Stripe product exists for this offer/SKU.` |
| `StripePriceMissing` | `Stripe product exists but the required price is missing.` |
| `OfferSkuAmbiguousMapping` | `Cannot uniquely map this line to a product.` |
| `MexIdMismatch` | `Customer Mex ID differs across data sources.` |
| `NonCspManualReview` | `Non-CSP line requires manual mapping and pricing rules.` |
| `ProductMismatch` | `Subscription truth and Stripe reference different products.` |

---

## Proposed Change Linking

| MismatchType | ProposedChangeId populated when |
|--------------|--------------------------------|
| `MissingInStripe` | `CreateMissingItem` proposed and guards pass |
| `QuantityMismatch` | `UpdateQuantity` proposed and guards pass |
| `BillingFrequencyMismatch` | `SwitchPrice` proposed and guards pass |
| `PriceMismatch` | `SwitchPrice` proposed and guards pass |
| `CatalogueMissing` | `CreateOrUpdateCatalogueEntry` proposed and `ProposeCatalogueChanges` true |
| `MappingMissing`, `MappingAmbiguous` | Never |
| Derived exceptions | Never |

Guards inherited from [004 mismatch-rules](../../004-reconciliation-engine/contracts/mismatch-rules.md) global guards table.

---

## Derived Exception Rules (no backing Mismatch)

### OrphanedBillingItem

**Trigger**: In-scope `StripeBillingItem` where:
- Not attached as `EntityMatchGroup.StripeItem` on any group with matching `SubscriptionLine` for same `MexId` + `CommercialKeyRoot`
- Subscription status active (or `IncludeInactiveSubscriptions` true)

**Severity**: `Error`  
**Domain**: `TruthVsStripe`  
**Id**: `{RunId}:d:OrphanedStripe:{StripeSubscriptionItemId}`

### MexIdMismatch

**Trigger**: Same `StripeCustomerId` or match group where `SubscriptionLine.Customer.MexId` ≠ `StripeItem.Customer.MexId` ≠ `SupplierCostLine.Customer.MexId` (any pairwise conflict)

**Severity**: `Error`  
**Domain**: `SupplierCostVsMapping`  
**Id**: `{RunId}:d:MexIdMismatch:{MatchGroupId or StripeCustomerId}`

### ProductMismatch

**Trigger**: Match group has both `SubscriptionLine` and `StripeItem` attached but `CommercialKey` roots differ and `MatchConfidence` ≥ `Medium`

**Severity**: `Error`  
**Domain**: `TruthVsStripe`  
**Id**: `{RunId}:d:ProductMismatch:{MatchGroupId}`

---

## Test Contract

One test per mapping row + one per derived rule. Each test asserts:
- `Category` and `Domain`
- `Severity`
- `Explanation` non-empty
- Evidence contains expected sources
- `ProposedChangeId` presence matches guards table
