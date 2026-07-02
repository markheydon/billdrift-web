# Mismatch Detection and Proposed Action Rules

**Consumer**: `BillDrift.Application.Reconciliation.Detection`  
**Extends**: [001 reconciliation-engine contract](../../001-billing-domain-model/contracts/reconciliation-engine.md)

## Purpose

Defines when each `MismatchType` is emitted, severity levels, operator-facing descriptions, and corresponding `ProposedActionType` values.

---

## Detection Order (per match group)

Rules evaluated **in this order**; earlier mapping failures suppress later billing comparisons:

1. Product resolution failure (`MappingMissing`, `MappingAmbiguous`, non-CSP)
2. Duplicate Stripe candidates (`MappingAmbiguous`)
3. Missing Stripe item (`MissingInStripe`)
4. Billing frequency (`BillingFrequencyMismatch`)
5. Quantity (`QuantityMismatch`)
6. Price (`PriceMismatch`)
7. Catalogue gap (`CatalogueMissing`)

**Rationale**: Prevents quantity/price mismatches on wrong product joins (001 R7, FR-008).

---

## Rule Catalog

### `MappingMissing`

| Trigger | Severity |
|---------|----------|
| Product resolution returns `Unresolved` | `Error` |
| Non-CSP line with `IncludeNonCspProducts == false` | `Warning` |
| Supplier line with unknown customer Mex ID | `Error` |

**Description template**:
```text
Cannot map [entity type] to a known product/customer. Source: [product name or ID]. Resolution attempted: [ProductResolutionPath chain].
```

Non-CSP variant prefix:
```text
Non-CSP line requires manual mapping: [remainder]
```

**Proposed action**: None

---

### `MappingAmbiguous`

| Trigger | Severity |
|---------|----------|
| Multiple `ProductMapping` name variant matches | `Error` |
| Multiple fuzzy candidates ≥ threshold | `Error` |
| Multiple Stripe items for same customer + commercial key | `Error` |

**Description template**:
```text
Ambiguous match for [customer/product context]. Candidates: [id list].
```

**Proposed action**: None

---

### `MissingInStripe`

| Trigger | Severity |
|---------|----------|
| Active subscription truth line, no attached Stripe item, mapping resolved (confidence ≥ Medium) | `Error` |
| Active truth, Stripe subscription canceled (when inactive excluded) | `Error` |

**Expected value**: Subscription truth licence count + term/frequency  
**Actual value**: `"Not billed in Stripe"`

**Proposed action**: `CreateMissingItem`

| Proposed field | Source |
|----------------|--------|
| `targetCustomer` | Stripe customer ID if known, else MexId |
| `commercialKey` | From truth line |
| `quantity` | Truth `LicenceCount` |
| `priceId` | Expected price from mapping or catalogue index |

**Guard**: No proposal when confidence is `Low` or `None`.

---

### `QuantityMismatch`

| Trigger | Severity |
|---------|----------|
| Attached truth + Stripe; `LicenceCount != Stripe.Quantity` | `Error` |

**Expected value**: Truth `LicenceCount` as string  
**Actual value**: Stripe `Quantity` as string

**Proposed action**: `UpdateQuantity`

| Proposed field | Value |
|----------------|-------|
| `subscriptionItemId` | Stripe item ID |
| `proposedQuantity` | Truth licence count |

**Guard**: Requires confidence ≥ Medium and no unresolved mapping issues on group.

---

### `BillingFrequencyMismatch`

| Trigger | Severity |
|---------|----------|
| Stripe price interval ≠ expected interval from `CommercialKey.Frequency` / truth term | `Error` |

**Expected value**: Expected interval (e.g., `"monthly"`)  
**Actual value**: Stripe price interval

**Proposed action**: `SwitchPrice` when alternate price ID exists in catalogue index

| Proposed field | Value |
|----------------|-------|
| `subscriptionItemId` | Stripe item ID |
| `proposedPriceId` | Correct price from mapping/catalogue |

**If no alternate price**: Mismatch only, no proposed action.

---

### `PriceMismatch`

| Trigger | Severity |
|---------|----------|
| `|Stripe.UnitAmount - IntendedPrice.Rrp| > PriceTolerance` | `Error` |
| Intended price missing for key | `Warning` (description notes pricing reference gap; not a price mismatch) |

**Expected value**: Intended RRP formatted with currency  
**Actual value**: Stripe unit amount formatted

**Proposed action**: `SwitchPrice` when correct price ID exists at intended amount

**If no alternate price at correct amount**: Mismatch only — operator must create catalogue entry first.

**Precondition**: Frequency must already match (frequency rule runs first).

---

### `CatalogueMissing`

| Trigger | Severity |
|---------|----------|
| `ProductMapping` exists but no Stripe price for required term/frequency | `Warning` |
| Active truth requires offer/SKU not present in Stripe catalogue | `Warning` |

**Expected value**: `"Stripe price for [CommercialKey]"`  
**Actual value**: `"Not found in catalogue"`

**Proposed action**: `CreateOrUpdateCatalogueEntry` when `ProposeCatalogueChanges == true`

| Payload field | Value |
|---------------|-------|
| `commercialKeyRoot` | Offer/SKU |
| `normalizedName` | From mapping |
| `pricesToCreate` | Missing term/frequency combinations |

---

## Proposed Action Guards (global)

| Guard | Effect |
|-------|--------|
| Mapping issue on group | No bill-impacting actions |
| `MatchConfidence == Low` on product resolution | No bill-impacting actions |
| `IncludeNonCspProducts == false` + non-CSP | No bill-impacting actions |
| `ProposeCatalogueChanges == false` | No catalogue proposals |

**Bill-impacting actions**: `CreateMissingItem`, `UpdateQuantity`, `SwitchPrice`

---

## Idempotency Keys

Format: `{RunId}:{MismatchId}:{ActionType}`

Implemented by `ProposedChangeFactory` using domain `IdempotencyKey` value object.

---

## Execution Order Index

When multiple proposed changes target the same subscription:

| Order | Action type |
|-------|-------------|
| 10 | `CreateOrUpdateCatalogueEntry` |
| 20 | `CreateMissingItem` |
| 30 | `SwitchPrice` |
| 40 | `UpdateQuantity` |

Catalogue changes precede subscription item changes so subsequent actions reference valid price IDs.

---

## Mismatch Severity Summary

| Type | Default severity |
|------|------------------|
| `MappingMissing` | Error (Warning for non-CSP) |
| `MappingAmbiguous` | Error |
| `MissingInStripe` | Error |
| `QuantityMismatch` | Error |
| `BillingFrequencyMismatch` | Error |
| `PriceMismatch` | Error |
| `CatalogueMissing` | Warning |

---

## Test Contract

One test fixture per rule row above. Each test asserts:
- `MismatchType` and `Severity`
- `ExpectedValue` and `ActualValue` populated
- `Description` contains rule identifier phrase (for operator clarity)
- Proposed action presence/absence per guards table

Determinism test: duplicate `Execute` calls → equivalent mismatch sets compared by `(Type, Customer.MexId, CommercialKey, Description)`.

Pro-rata test: supplier recurring qty=10 + pro-rata adjustment qty=2 → quantity comparison uses 10 vs Stripe, not 12.
