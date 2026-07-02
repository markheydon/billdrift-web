# Suppression and Ordering Rules

**Consumer**: `BillDrift.Application.Reconciliation.ExceptionSurfacing`  
**Related**: [exception-surfacing-pipeline.md](./exception-surfacing-pipeline.md), [mismatch-to-exception-mapping.md](./mismatch-to-exception-mapping.md)

## Purpose

Defines false-positive controls (suppression), `RequiresActionNow` computation, and deterministic ordering for customer groups and exceptions.

---

## Suppression Rules

Applied during **Suppress** phase. First matching root cause on a match group wins.

### SR-1: Root-Cause Mapping Suppression

**When**: Match group has any exception with category ∈ `{OfferSkuAmbiguousMapping, NonCspManualReview}` OR engine `MismatchType` ∈ `{MappingMissing, MappingAmbiguous}` on group

**Suppress on same group**:
- `QuantityLicenceMismatch`
- `BillingFrequencyMismatch`
- `StripePriceRrpMismatch`
- `ProductMismatch`
- `MissingBillingItem` (if mapping unresolved)

**Keep**: The mapping exception(s)

---

### SR-2: Root-Cause MexId Suppression

**When**: `MexIdMismatch` exception exists for match group or Stripe customer

**Suppress on same customer + affected items**:
- `MissingBillingItem`
- `OrphanedBillingItem`
- `QuantityLicenceMismatch`
- `BillingFrequencyMismatch`
- `StripePriceRrpMismatch`
- `ProductMismatch`

**Keep**: `MexIdMismatch`

---

### SR-3: Low-Confidence Proposed Action Strip

**When**: `EntityMatchGroup.Confidence` is `Low` or `None`

**Action**: Do not remove exception; set `ProposedChangeId = null` and `RequiresActionNow = false` for bill-impacting categories

**Keep**: Mapping review exceptions

---

### SR-4: Catalogue Subsumed by Subscription

**When**: Same `CommercialKey` has both:
- `StripePriceRrpMismatch` with domain `TruthVsStripe`, AND
- Catalogue category (`StripeProductMissing`, `StripePriceMissing`, `StripePriceRrpMismatch` with domain `PricingVsCatalogue`)

**Suppress**: Catalogue-domain exception

**Keep**: Subscription-level `StripePriceRrpMismatch`

---

### SR-5: Out-of-Scope Inactive Orphan

**When**: `OrphanedBillingItem` candidate references Stripe item on canceled subscription AND `IncludeInactiveSubscriptions == false`

**Suppress**: Entire orphaned candidate (not surfaced)

---

## Consolidation Rules

### CR-1: Catalogue Key Merge

Merge exceptions where:
- Same `CommercialKey` (offer, SKU, term, frequency)
- Category ∈ `{StripeProductMissing, StripePriceMissing, StripePriceRrpMismatch}`
- Same `ReconciliationDomain` (`PricingVsCatalogue`)

**Merge behaviour**:
- Union evidence (dedupe by `Source+Field+Value`)
- `Severity` = max severity of merged set
- `SuppressedSiblingCount` = sum of merged counts + (merged count - 1)

---

## RequiresActionNow Rules

Computed in **Finalize** phase after suppression.

### AR-1: Bill-Impacting Billing Exceptions

`RequiresActionNow = true` when ALL:
- `Severity == Error`
- Category ∈ `{MissingBillingItem, OrphanedBillingItem, QuantityLicenceMismatch, BillingFrequencyMismatch, ProductMismatch}`
- No active SR-1 or SR-2 suppression on the group's billing exceptions
- `MatchConfidence` ≥ `Medium` (or no match group — derived exceptions use detector confidence)

### AR-2: Corrective Action Ready

`RequiresActionNow = true` when ALL:
- `Severity == Error`
- `ProposedChangeId` is non-null
- SR-3 has not stripped the link

### AR-3: Blocking Setup Work

`RequiresActionNow = true` when ALL:
- `Severity == Error`
- Category ∈ `{OfferSkuAmbiguousMapping, MexIdMismatch, StripeProductMissing, StripePriceMissing, NonCspManualReview}`
- Operator must resolve before billing corrections

**Note**: AR-3 reflects blocking setup per spec FR-011 — not "ready to apply" bill changes.

### AR-4: Default

Otherwise `RequiresActionNow = false` (includes all `Warning` and `Info` unless overridden by future amendment).

---

## Category Priority (within-group ordering)

Lower number = higher priority.

| Priority | Category |
|----------|----------|
| 10 | `MissingBillingItem` |
| 20 | `OrphanedBillingItem` |
| 30 | `MexIdMismatch` |
| 40 | `OfferSkuAmbiguousMapping` |
| 50 | `ProductMismatch` |
| 60 | `QuantityLicenceMismatch` |
| 70 | `BillingFrequencyMismatch` |
| 80 | `StripePriceRrpMismatch` (TruthVsStripe) |
| 90 | `StripeProductMissing` |
| 100 | `StripePriceMissing` |
| 110 | `StripePriceRrpMismatch` (PricingVsCatalogue) |
| 120 | `NonCspManualReview` |

---

## Customer Group Ordering

Sort key (ascending unless noted):

1. `HighestSeverity` — Error (0) < Warning (1) < Info (2)
2. `RequiresActionNowCount` — descending
3. `Customer.MexId.Value` — ordinal ascending

---

## Within-Group Exception Ordering

Sort key:

1. `Severity` — Error < Warning < Info
2. `RequiresActionNow` — true before false
3. `Category` priority from table above
4. `Product.CommercialKey` — ordinal string of `OfferId/SkuId/Term/Frequency`, nulls last
5. `Id.Value` — ordinal ascending (stable tie-break)

---

## Flat List Derivation

`ReconciliationExceptionViewModel.FlatExceptions()`:

```text
CustomerGroups
  .OrderBy(existing customer group order)
  .SelectMany(g => g.Exceptions)  // already ordered within group
  .ToList()
```

Consumers MUST NOT re-sort if deterministic order required.

---

## Test Contract

| Test | Asserts |
|------|---------|
| `SR1_mapping_suppresses_quantity` | Quantity mismatch absent when mapping missing on group |
| `SR2_mexid_suppresses_billing` | Missing billing absent when MexId mismatch present |
| `SR3_low_confidence_strips_action` | `ProposedChangeId` null, `RequiresActionNow` false |
| `SR4_catalogue_subsumed` | Single subscription price exception remains |
| `SR5_inactive_orphan_excluded` | No orphaned exception for canceled item |
| `CR1_catalogue_merge` | One exception per commercial key |
| `Ordering_customer_groups` | Error customer before Warning-only customer |
| `Ordering_within_group` | Missing billing before quantity mismatch |
| `Determinism_flat_list` | Two Surface calls → identical ordered IDs |
