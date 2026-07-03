# Contract: Mismatch Comparison Rules

**Feature**: `008-reconciliation-run-history`  
**Project**: `BillDrift.Application.History/StableMismatchKeyFactory.cs`  
**Date**: 2026-07-02

## Purpose

Define deterministic cross-run identity for mismatches enabling month-to-month comparison (FR-010, FR-011) and drift trends (FR-012). Run-scoped `MismatchId` is **not** used for cross-run matching.

---

## StableMismatchKey Format

```text
{mexId}|{offerId}/{skuId}|{mismatchType}|{distinguisher}
```

Components:

| Component | Source | Fallback |
|-----------|--------|----------|
| `mexId` | `Mismatch.Customer.MexId` | `_unknown_` |
| `offerId/skuId` | `CommercialKeyRoot` from `Mismatch.CommercialKey` | `_noid_/_noid_` |
| `mismatchType` | `Mismatch.Type` enum name | Required |
| `distinguisher` | Type-specific token | See below |

All components lowercased; pipe-delimited; max 512 chars (truncate distinguisher if needed).

---

## Distinguisher Rules by MismatchType

| MismatchType | Distinguisher |
|--------------|---------------|
| `QuantityMismatch` | `qty` |
| `PriceMismatch` | `{frequency}:{normalizedExpectedAmount}` |
| `BillingFrequencyMismatch` | `{expectedFrequency}` |
| `MissingInStripe` | `missing-stripe` |
| `MissingInSubscriptionTruth` | `missing-truth` |
| `CatalogueMissing` | `catalogue-missing:{frequency}` |
| `CataloguePriceMismatch` | `catalogue-price:{frequency}` |
| `MappingMissing` | `mapping:{supplierNameHash8}` |
| `MappingAmbiguous` | `mapping-ambiguous:{supplierNameHash8}` |
| `DuplicateStripeItem` | `duplicate:{subscriptionItemId}` |
| Other / Unknown | `general:{descriptionHash8}` |

`normalizedExpectedAmount` = expected value parsed to cents integer when parseable; else hash of `ExpectedValue` string.

`supplierNameHash8` = first 8 hex chars of SHA-256 of supplier product label from description or entity refs.

---

## Comparison Algorithm

`RunComparisonService.Compare(earlierSnapshot, laterSnapshot)`:

1. Build map `StableMismatchKey → Mismatch` for each run's mismatches
2. **New** = keys in later not in earlier
3. **Resolved** = keys in earlier not in later
4. **Persisting** = keys in both
   - Set `ValuesChanged=true` when `ExpectedValue` or `ActualValue` differ (case-insensitive trim)
5. Attach `ApprovalStatusSummary` for persisting items by joining 007 proposals via `IdempotencyKey` on later run's proposals

---

## Mapping Version Change Detection

```csharp
mappingVersionChanged = earlier.MappingVersion.ContentHash != later.MappingVersion.ContentHash;
```

When true, comparison report includes warning banner; persisting mismatches flagged `mayBeMappingDriven` when `MismatchType` is mapping-related.

---

## Input Delta Summary

Per `InputDomainType`:

| Field | Rule |
|-------|------|
| `EarlierRecordCount` / `LaterRecordCount` | From input metadata |
| `FingerprintChanged` | Source `ContentFingerprint` differs |
| Line-level diff | **Out of scope v1** — counts and fingerprint only |

---

## Drift Trend Classification

| OccurrenceCount | Classification |
|-----------------|----------------|
| 1 | Transient / resolved candidate |
| 2 | Recurring (minimum threshold) |
| ≥ 3 in 6 months | High-priority recurring (SC-004) |

`IsRecurring = OccurrenceCount >= 2` (configurable via `minOccurrences` query param).

---

## Test Fixtures Required

| Fixture | Validates |
|---------|-----------|
| Same quantity mismatch 4 runs | Recurring trend entry |
| Mismatch run 1 only | Resolved classification |
| Mapping version change between runs | `MappingVersionChanged=true` |
| Same customer, different MexId | Treated as separate keys |

---

## Invariants

- Identical mismatch semantics across runs MUST produce identical `StableMismatchKey`
- Key computation MUST NOT include `RunId` or `MismatchId`
- Algorithm MUST be pure (no I/O)
