# Catalogue Check Rules

**Consumer**: `BillDrift.Application.CatalogueReconciliation.Detection`  
**Feature**: `012-stripe-catalogue-reconciliation`  
**Extends**: [004 mismatch-rules](../../004-reconciliation-engine/contracts/mismatch-rules.md) (catalogue subset)  
**Date**: 2026-07-03

## Purpose

Defines detection triggers, severity, operator descriptions, and proposed fix payloads for each `CatalogueExceptionType`.

---

## Rule Catalog

### `MissingProduct` (rule: `CAT-001`)

| Trigger | Severity |
|---------|----------|
| Mapped offer/SKU with intended pricing; no Stripe product by ID or metadata | `Warning` |

**Description template**:
```text
Missing Stripe product for offer {OfferId} / SKU {SkuId} ({NormalizedName}).
```

**Proposed fix**: `CreateProduct`

| Proposed state field | Value |
|---------------------|-------|
| `normalizedName` | From mapping |
| `offerId`, `skuId` | Metadata to set on create |
| `mappingId` | Source mapping reference |

---

### `MissingPrice` (rule: `CAT-002`)

| Trigger | Severity |
|---------|----------|
| Product exists; no active price for required term/frequency | `Warning` |

**Description template**:
```text
Missing Stripe price for {OfferId}/{SkuId} — {Term} / {Frequency} (expected RRP {Rrp}).
```

**Proposed fix**: `CreatePrice`

| Proposed state field | Value |
|---------------------|-------|
| `stripeProductId` | Resolved product |
| `unitAmount` | Intended RRP minor units |
| `currency` | From intended price |
| `interval` | From commercial key frequency |

---

### `IncorrectPrice` (rule: `CAT-003`)

| Trigger | Severity |
|---------|----------|
| Active price present; `unit_amount` ≠ intended RRP OR currency differs | `Warning` |

**Description template**:
```text
Stripe price {PriceId} amount {Actual} does not match intended RRP {Expected} for {CommercialKey}.
```

**Proposed fix**: `CreateReplacementPrice`

| Proposed state field | Value |
|---------------------|-------|
| `incorrectPriceId` | Existing price to retire/avoid |
| `newUnitAmount` | Intended RRP |
| `currency` | Must match intended |

**Note**: Stripe prices are immutable — never propose in-place amount edit.

---

### `DuplicateProduct` (rule: `CAT-004`)

| Trigger | Severity |
|---------|----------|
| >1 Stripe product resolves to same `(OfferId, SkuId)` | `Error` |

**Description template**:
```text
Duplicate Stripe products for offer {OfferId} / SKU {SkuId}: {ProductIdList}.
```

**Proposed fix**: `FlagManualCleanup` (`IsActionable = false`)

---

### `DuplicatePrice` (rule: `CAT-005`)

| Trigger | Severity |
|---------|----------|
| >1 active price on same product for same interval + currency | `Error` |

**Description template**:
```text
Duplicate active Stripe prices for product {ProductId} interval {Frequency}: {PriceIdList}.
```

**Proposed fix**: `FlagManualCleanup`

---

### `PricingReferenceGap` (rule: `CAT-006`)

| Trigger | Severity |
|---------|----------|
| Mapping in scope; zero intended prices for root | `Information` |

**Proposed fix**: None

---

### `MappingAmbiguous` (rule: `CAT-007`)

| Trigger | Severity |
|---------|----------|
| `MappingConfidence.Low` or conflicting Stripe product ID slots | `Error` |

**Proposed fix**: None

---

### `UnmappedCatalogueEntry` (rule: `CAT-008`)

| Trigger | Severity |
|---------|----------|
| Stripe product/price with no offer/SKU metadata and no inverse mapping | `Information` |

**Proposed fix**: None

---

## Global Guards

| Guard | Effect |
|-------|--------|
| Duplicate product conflict on root | Suppress `MissingProduct` for that root |
| `MappingAmbiguous` | Suppress missing/incorrect price checks |
| `IncludeNonCspProducts == false` + NonCsp mapping | Skip root |
| Archived price only + `IncludeArchivedPrices == false` | Treated as missing active price |

---

## Approval Adapter Mapping

| `CatalogueProposedActionType` | `ProposedActionType` | `ApprovalEligibility` |
|------------------------------|----------------------|----------------------|
| `CreateProduct` | `CreateOrUpdateCatalogueEntry` | `Eligible` |
| `CreatePrice` | `CreateOrUpdateCatalogueEntry` | `Eligible` |
| `CreateReplacementPrice` | `CreateOrUpdateCatalogueEntry` | `Eligible` |
| `FlagManualCleanup` | — | `CatalogueConflict` |

Execution order index: `10` (catalogue) — same as 004/007.

---

## Test Contract

One fixture per rule. Each test asserts:
- `CatalogueExceptionType` and `Severity`
- `RuleId` matches rule code
- Proposed fix presence per guards table
- Deterministic ordering across duplicate runs
