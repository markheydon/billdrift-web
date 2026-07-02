# Matching Phases Contract

**Consumer**: `BillDrift.Application.Reconciliation` pipeline stages  
**Related**: [data-model.md](../data-model.md), [001 normalization contract](../../001-billing-domain-model/contracts/normalization.md)

## Purpose

Defines deterministic rules for resolving **customers**, **products**, and **Stripe billing items** into `EntityMatchGroup` records.

---

## Phase 1: Customer Resolution

**Key**: `MexId` (required)

| Source entity | Customer field |
|---------------|----------------|
| `MicrosoftSubscriptionLine` | `Customer.MexId` |
| `SupplierCostLine` | `Customer.MexId` |
| `StripeBillingItem` | Metadata Mex ID or linked customer Mex ID |

**Rules**:
1. All lines MUST resolve to the same `CustomerIdentity` when grouped
2. Missing Mex ID → line skipped with `MappingMissing` (supplier) or validation warning (truth)
3. Display name and tenant ID merged from first non-null source in priority: truth → Stripe → supplier

---

## Phase 2: Product Resolution

See [research R2](../research.md#r2-product-resolution-priority). Implemented by `CommercialKeyResolver`.

### Priority chain

```
ExplicitOfferSku → StripeMetadata → MappingByRoot → NameVariantExact → NameFuzzy → Unresolved
```

### Per-source rules

| Source | Primary key extraction |
|--------|------------------------|
| `MicrosoftSubscriptionLine` | `CommercialKey` from offer/SKU + term + frequency columns |
| `StripeBillingItem` | Product metadata offer/SKU; else `ProductMapping.StripeProductId` lookup |
| `SupplierCostLine` | Offer/SKU if present on line; else supplier product name → mapping |
| `IntendedPrice` | Always has full `CommercialKey` |

### Confidence assignment

| Path | Confidence |
|------|------------|
| `ExplicitOfferSku` (both IDs present) | `High` |
| `StripeMetadata` (both IDs present) | `High` |
| `MappingByRoot` | `Medium` |
| `NameVariantExact` | `Medium` |
| `NameFuzzy` (single candidate ≥ 0.85) | `Low` |
| `Unresolved` | `None` |

---

## Phase 3: Stripe Item Matching

Implemented by `StripeItemMatcher`.

**Input**: `CustomerIdentity`, `CommercialKey`, active `StripeBillingItem` candidates from index

**Algorithm**:
1. Filter items by `MexId` match
2. Filter by `CommercialKeyRoot` match (offer/SKU on item metadata or resolved via product)
3. Filter by billing interval compatible with `CommercialKey.Frequency`
4. Evaluate candidate set:

| Candidates | Action |
|------------|--------|
| 0 | No Stripe attachment; downstream `MissingInStripe` if truth active |
| 1 | Attach to match group; confidence inherits from product resolution |
| 2+ | Do not attach; emit `MappingAmbiguous` with all candidate item IDs |

**Inactive subscriptions**: Excluded when `IncludeInactiveSubscriptions == false` (default).

---

## Phase 4: Match Group Assembly

Implemented by `MatchGroupBuildStage`.

### Primary driver: subscription truth

For each `MicrosoftSubscriptionLine` where:
- `SubscriptionStatus == Active` (or included inactive when option set)
- Billing period overlaps `ReconciliationRequest.Scope`

Create group keyed by `(MexId, CommercialKey)`.

### Attachments (in order)

1. `StripeBillingItem` via Phase 3
2. `IntendedPrice` via `IntendedPriceIndex.TryGet`
3. `SupplierCostLine`(s) with same MexId + `CommercialKeyRoot` and period in scope

### Orphan supplier lines

Supplier lines not attached during truth-driven pass:
1. Attempt customer + product resolution independently
2. Create standalone group with `Confidence <= Medium`
3. Emit mapping issues as needed

### Duplicate truth lines

Two active truth lines with identical `(MexId, CommercialKey)`:
- Merge into one group
- Use **max** `LicenceCount` for quantity comparison (document in code comment — conservative for revenue protection)
- Emit Info mismatch if counts differ between duplicate truth rows

---

## Phase 5: Fuzzy Name Matching

Implemented by `DeterministicFuzzyNameMatcher`.

**Normalization** (applied to both input name and candidate variant names):
1. Trim
2. Lowercase invariant
3. Remove punctuation: `.`, `,`, `(`, `)`, `-`, `/`
4. Collapse whitespace

**Scoring**: Token-set Jaccard similarity on whitespace-split tokens

**Threshold**: 0.85 minimum

**Tie-break** (deterministic):
1. Highest score
2. Lexicographically smallest `OfferId` + `SkuId` composite
3. Lexicographically smallest mapping record ID

**Guard**: Fuzzy-only matches (`Confidence == Low`) MUST NOT produce bill-impacting `ProposedChange` unless the same group also has `High` or `Medium` confidence product resolution from another attached entity.

---

## Phase 6: Non-CSP Classification

When resolved `ProductMapping.Classification == NonCsp`:

| `IncludeNonCspProducts` | Behaviour |
|-------------------------|-----------|
| `false` (default) | Attach line if customer known; emit `MappingMissing` with `"Non-CSP line requires manual mapping:"` prefix |
| `true` | Normal reconciliation path + Info severity note in description |

---

## Ordering Guarantees

Match groups ordered by:
1. `Customer.MexId` (ordinal)
2. `CommercialKey.OfferId` + `CommercialKey.SkuId` (ordinal)
3. `CommercialKey.Term`, `CommercialKey.Frequency` (enum ordinal)

Mismatches ordered by:
1. `Customer.MexId`
2. `CommercialKey` (nulls last)
3. `MismatchType` (enum ordinal)

---

## Test Contract

Matching phase tests MUST cover:
- Offer/SKU high-confidence match without name similarity
- Partial Stripe metadata → Medium confidence
- Exact name variant match
- Fuzzy single candidate at 0.85 threshold
- Fuzzy tie → ambiguous
- Duplicate Stripe items → ambiguous
- Non-CSP flag with default options
- Orphan supplier line standalone group
