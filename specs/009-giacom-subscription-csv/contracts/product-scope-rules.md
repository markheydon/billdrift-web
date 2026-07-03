# Product Scope Rules Contract

**Feature**: `009-giacom-subscription-csv`  
**Implementation**: `BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement.ProductScopeClassifier`

## Purpose

Defines which Subscription Management report rows qualify as **Microsoft 365 / CSP-style** subscription truth (FR-013) vs out-of-scope products excluded from output.

## Evaluation Order

For each parsed row, evaluate in order; first definitive match wins:

```
1. Deny-list match on Service, ProductType, or ProductName → EXCLUDE (ProductOutOfScope)
2. Allow-list match on Service, ProductType, or ProductName → INCLUDE
3. Ambiguous (no allow/deny match) → INCLUDE with ProductScopeAmbiguous warning
   UNLESS ProductName contains deny token → EXCLUDE with warning
```

## Deny List (hard exclude)

Case-insensitive substring match on `Service`, `ProductType`, or `ProductName`:

| Token | Rationale |
|-------|-----------|
| `Exclaimer` | Spec example non-CSP product |
| `Non-CSP` | Explicit non-CSP marker |
| `Non CSP` | Variant |
| `Third Party` | Non-Microsoft add-ons |
| `Third-Party` | Variant |
| `Acronis` | Common non-M365 add-on |
| `Dropbox` | Non-M365 |
| `Adobe` | Non-M365 |

Configurable via `SubscriptionManagementScopeOptions.DenyTokens` (defaults above).

## Allow List (hard include)

Case-insensitive substring match:

| Field | Tokens |
|-------|--------|
| `Service` | `Microsoft`, `Office 365`, `Microsoft 365`, `M365`, `CSP` |
| `ProductType` | `CSP`, `NCE`, `Microsoft`, `Online Services` |
| `ProductName` | `Microsoft 365`, `Office 365`, `Exchange Online`, `SharePoint`, `OneDrive`, `Microsoft Teams`, `Teams`, `Defender`, `Entra`, `Azure AD`, `Intune`, `Power BI`, `Visio`, `Project`, `Dynamics 365 Business`, `Windows 365` |

## Ambiguous Rows

When both allow and deny lists miss:

- If `ProductName` is non-empty and contains any allow-list product token → **INCLUDE** + `ProductScopeAmbiguous` warning
- If `ProductName` is empty and `OfferId`/`SkuId` present → **INCLUDE** + warning (operator review)
- If only deny-adjacent tokens (e.g., `Online`) without Microsoft context → **EXCLUDE** + warning

## Summary Counts

`SubscriptionManagementCsvIngestionSummary.RowsExcludedByScope` increments for every `EXCLUDE` decision. Excluded rows do not appear in `RawRows` or `SubscriptionLines`.

## Test Fixtures Required

| Fixture | Purpose |
|---------|---------|
| `mixed-products.csv` | M365 + Exclaimer rows |
| `sparse-service.csv` | Blank service, M365 product name |
| `nce-products.csv` | NCE-flagged M365 rows only |
