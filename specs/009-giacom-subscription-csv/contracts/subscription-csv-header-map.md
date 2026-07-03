# Subscription Management CSV Header Map Contract

**Feature**: `009-giacom-subscription-csv`  
**Implementation**: `BillDrift.Infrastructure.Import.Giacom.SubscriptionManagement.SubscriptionManagementCsvHeaderMap`  
**Related**: [csv-ingestion-pipeline.md](./csv-ingestion-pipeline.md)

## Purpose

Maps Giacom `SubscriptionManagementReport.csv` column headers to logical ingestion fields. Matching is **case-insensitive**; first matching alias wins.

> **Note**: Aliases below are **provisional** until validated against sanitized production exports (research R12 fixture dependency).

## Required Logical Fields

| Logical field | Accepted header aliases (priority order) |
|---------------|------------------------------------------|
| `MexId` | `Mex ID`, `MEX ID`, `MexId`, `mex_id`, `Sub Account`, `Sub Account ID` |
| `OfferId` | `Offer ID`, `Offer Id`, `offer_id`, `OfferID` |
| `SkuId` | `SKU ID`, `Sku ID`, `Sku Id`, `sku_id`, `SKU`, `SkuID` |
| `Licences` | `Licences`, `Licenses`, `License Count`, `Qty`, `Quantity`, `Seats` |
| `Status` | `Status`, `Subscription Status`, `Sub Status` |

## Optional Logical Fields — Customer

| Logical field | Accepted header aliases |
|---------------|-------------------------|
| `CustomerName` | `Customer`, `Customer Name`, `Account Name`, `Company` |
| `TenantId` | `Tenant ID`, `Tenant Id`, `tenant_id`, `Microsoft Tenant ID`, `Tenant` |

## Optional Logical Fields — Product

| Logical field | Accepted header aliases |
|---------------|-------------------------|
| `Service` | `Service`, `Service Name`, `Product Family` |
| `ProductName` | `Product`, `Product Name`, `Subscription`, `SKU Name` |
| `ProductType` | `Product Type`, `ProductType`, `Type`, `Billing Type` |

## Optional Logical Fields — Subscription Lifecycle

| Logical field | Accepted header aliases |
|---------------|-------------------------|
| `SupplierSubscriptionId` | `Subscription ID`, `Subscription Id`, `Sub ID`, `Giacom Subscription ID`, `Supplier Reference` |
| `Term` | `Term`, `Term Duration`, `Commitment Term`, `Billing Term` |
| `Frequency` | `Billing Frequency`, `Frequency`, `Billing Cycle`, `Payment Frequency` |
| `RenewalDate` | `Renewal Date`, `Next Renewal`, `Renewal`, `Anniversary Date` |
| `EndOfTermAction` | `End of Term Action`, `End Of Term Action`, `Auto Renew`, `Cancellation Policy` |
| `IsNce` | `NCE`, `Is NCE`, `NCE Flag`, `New Commerce Experience` |
| `IsTrial` | `Trial`, `Is Trial`, `Trial Flag`, `Trial Subscription` |
| `CancellableUntil` | `Cancellable Until`, `Cancel Until`, `Cancellation Deadline` |
| `MigrationToNce` | `Migration to NCE`, `NCE Migration`, `Migrate to NCE` |
| `AssignedLicences` | `Assigned Licences`, `Assigned Licenses`, `Assigned`, `Assigned Seats` |

## Optional Logical Fields — Pricing

| Logical field | Accepted header aliases |
|---------------|-------------------------|
| `Price` | `Price`, `Unit Price`, `Sell Price`, `Customer Price` |
| `Erp` | `ERP`, `Erp`, `Estimated Retail Price`, `RRP`, `List Price` |

## File-Level Validation

| Condition | Outcome |
|-----------|---------|
| No header row | `DocumentUnreadable` / file fail |
| Zero data rows | `EmptyFile` — success with empty output |
| Mandatory logical field has no alias match | `MandatoryHeaderMissing` — file fail |
| Duplicate header names | First occurrence wins; log warning |

## Row-Level Validation

| Condition | Outcome |
|-----------|---------|
| Empty Mex ID | Row skip — `MexIdMissing` |
| Unparseable licences | Row skip — `LicenceCountUnparseable` |
| Unparseable price when column mapped and non-empty | Row skip — `PriceUnparseable` |
| Missing offer or SKU | Warning — `CommercialKeyMissing`; raw row emitted |
| Out of scope product | Scope exclude — `ProductOutOfScope` |

## Fixture Authoring

Test fixtures MUST document which alias variant they exercise in a sibling `.md` file under `tests/fixtures/subscription-management/`.
