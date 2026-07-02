# Contract: Classification Rules

**Feature**: `006-reconciliation-classification`  
**Date**: 2026-07-02

## Rule Precedence (CR-1)

Rules evaluate in strict order. **First matching rule wins** unless manual override is present (CR-0).

### CR-0: Manual Override

**When**: Active override exists in `IItemClassificationStore` for `StableKey`  
**Then**: Return override classification, `Source = ManualOverride`, `Confidence = High`, `RuleBasis = "ManualOverride"`  
**Notes**: Required per `ClassificationRuleConfiguration.RequireNotesForAlertSuppression` when classification suppresses missing-billing alerts.

---

### CR-1: Internal Customer

**When**: `item.CustomerMexId` ∈ `config.InternalMexIds`  
**Then**: `Classification = Internal`, `Confidence = High`, `RuleBasis = "InternalMexId:{mexId}"`

---

### CR-2: Custom/Service Independence

**When** any of:
- `ProductCategory == CustomService` from category rules AND `HasStripeBillingOnly` (no supplier cost, no subscription truth in scope)
- OR `HasStripeBillingOnly` AND no `HasOfferSku` AND product category is `Other`

**Then**: `Classification = CustomService`, `Confidence = High` (or `Medium` if only Stripe present without category rule)

**RuleBasis**: `"CustomService:StripeOnly"` or `"CustomService:CategoryRule:{pattern}"`

---

### CR-3: Non-CSP Supplier

**When**:
- `HasSupplierCostEvidence == true`
- AND `InSubscriptionTruth == false` for correlated product in scope
- AND CR-1, CR-2 did not fire

**Then**: `Classification = NonCspSupplier`, `Confidence = High`, `RuleBasis = "NonCsp:SupplierOnly"`

**Partial truth correlation**: If supplier line has no offer/SKU but subscription truth exists for same customer + product name via mapping → do **not** fire CR-3; proceed to CR-4/CR-5.

---

### CR-4: Microsoft CSP

**When**:
- `HasOfferSku == true`
- AND (`InSubscriptionTruth == true` OR `InIntendedPriceList == true`)
- AND `ProductCategory == Microsoft365`
- AND CR-1 through CR-3 did not fire

**Then**: `Classification = MicrosoftCsp`

**Confidence**:
- `High` — all three: offer/SKU, truth, price list
- `Medium` — offer/SKU + truth OR price list only
- `Low` — offer/SKU + price list only without truth (timing lag)

**RuleBasis**: `"MicrosoftCsp:OfferSku+Truth+PriceList"` (signals joined with `+`)

---

### CR-5: Conservative Default (CR-FALLBACK)

**When**: No prior rule matched with sufficient confidence  
**Then**: `Classification = NonCspSupplier`, `Confidence = Low`, `RuleBasis = "ConservativeDefault:{signals}"`

**Rationale**: FR-018 — prefer manual review over false CSP match.

---

## Product Category Resolution (PCR-1)

Evaluate `ProductCategoryRules` in declaration order:

1. `OfferIdPrefix` match on offer ID
2. `SkuIdPrefix` match on SKU ID  
3. `ProductNameContains` on normalized product name

If no rule matches → `ProductCategory.Other`.

Microsoft 365 detection: rules seeded with known offer ID prefixes (e.g. `CFQ7TTC0`, `MS365-`) in default config fixture — operator-editable via config API.

---

## Reconciliation Impact Rules (RI-1)

| Classification | Rule ID | Effect |
|----------------|---------|--------|
| `Internal` | RI-1a | Do not emit `MismatchType.MissingInStripe` for subscription truth lines |
| `Internal` | RI-1b | Do not surface `MissingBillingItem` exception (SR-6) |
| `Internal` | RI-1c | Quantity/price/frequency checks proceed when Stripe item present |
| `NonCspSupplier` | RI-2a | Treat as non-CSP in match stages (existing 004 behaviour) |
| `NonCspSupplier` | RI-2b | Surface `NonCspManualReview` exception category |
| `NonCspSupplier` | RI-2c | No bill-impacting `ProposedChange` without operator mapping |
| `CustomService` | RI-3a | Suppress missing-billing from truth absence |
| `CustomService` | RI-3b | Orphaned Stripe review may still apply |
| `MicrosoftCsp` | RI-4 | Standard reconciliation path |

---

## Exception Surfacing Suppression (SR-6)

**When**: Exception category is `MissingBillingItem` AND linked subscription truth item has `Classification == Internal`  
**Then**: Suppress exception; record `SuppressionRule.ClassificationInternal`

**When**: `MissingBillingItem` AND `Classification == CustomService` AND no subscription truth in group  
**Then**: Suppress; record `SuppressionRule.ClassificationCustomService`

---

## Override Validation (OV-1)

| Target classification | Notes required (default) |
|----------------------|--------------------------|
| `Internal` | Yes |
| `CustomService` | Yes |
| `MicrosoftCsp` | No (but recommended) |
| `NonCspSupplier` | No |

Override that would **increase** alert suppression without notes → reject with validation error.

---

## Test Matrix (minimum fixtures)

| Fixture ID | Expected classification | Key assertion |
|------------|------------------------|---------------|
| `classify-csp-full-signals` | `MicrosoftCsp` High | Offer/SKU + truth + price list |
| `classify-internal-mex` | `Internal` High | Config internal Mex ID |
| `classify-non-csp-supplier-only` | `NonCspSupplier` High | PDF line, no truth |
| `classify-custom-stripe-only` | `CustomService` High | Stripe item only |
| `classify-override-wins` | Per override | Manual beats automatic |
| `classify-conservative-partial-sku` | `NonCspSupplier` Low | Partial metadata |
| `classify-determinism` | Identical ×2 | Snapshot equality |
