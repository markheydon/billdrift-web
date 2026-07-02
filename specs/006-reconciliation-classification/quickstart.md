# Quickstart: Reconciliation Item Classification

**Feature**: `006-reconciliation-classification`  
**Date**: 2026-07-02

## Prerequisites

- .NET 10 SDK
- Repository built: `dotnet build BillDrift.sln`
- Aspire AppHost with Azure Storage emulator (Azurite) — see [research.md](./research.md) R6
- Features 001–005 implemented (domain, ingestion, reconciliation engine, exception surfacing)

## Run Tests (primary validation)

```powershell
# Classification rule unit tests
dotnet test tests/BillDrift.Application.Tests --filter "FullyQualifiedName~Classification"

# Reconciliation integration with classification fixtures
dotnet test tests/BillDrift.Application.Tests --filter "FullyQualifiedName~ClassificationIntegration"

# Infrastructure table store tests (Azurite / emulator)
dotnet test tests/BillDrift.Infrastructure.Tests --filter "FullyQualifiedName~ClassificationStorage"
```

## Validation Scenarios

### V1: Microsoft CSP automatic classification

**Fixture**: `tests/fixtures/classification/classify-csp-full-signals.json`  
**Steps**: Run `ClassificationService.ClassifyAsync` with fixture inputs  
**Expected**: Subscription truth item → `MicrosoftCsp`, `Confidence = High`, rule basis contains `OfferSku` and `Truth`

### V2: Internal customer suppresses missing billing

**Fixture**: `tests/fixtures/classification/internal-customer-no-missing-billing.json`  
**Setup**: Config `InternalMexIds` includes fixture customer Mex ID  
**Steps**: Classify → reconcile → surface exceptions  
**Expected**: Zero `MissingBillingItem` / `MissingInStripe` for internal truth lines without Stripe match

### V3: Non-CSP supplier manual review

**Fixture**: `tests/fixtures/classification/non-csp-supplier-only.json`  
**Expected**: `NonCspSupplier` classification → `NonCspManualReview` exception → no `ProposedChange` with bill impact

### V4: Manual override precedence

**Steps**:
1. Classify supplier-only line → `NonCspSupplier`
2. `ApplyOverrideAsync` → `MicrosoftCsp` with notes
3. Re-classify same inputs

**Expected**: Second run returns `ManualOverride` source; automatic rules ignored

### V5: Override clear re-evaluates

**Steps**: Clear override on V4 item → classify again  
**Expected**: Reverts to automatic `NonCspSupplier`

### V6: Classification determinism

**Steps**: Classify same fixture twice with frozen clock optional  
**Expected**: `ByStableKey` dictionaries equal (ignore `ClassifiedAt`)

### V7: Conservative default on ambiguous partial SKU

**Fixture**: `classify-conservative-partial-sku.json`  
**Expected**: `NonCspSupplier` + `Low` confidence, not `MicrosoftCsp` High

### V8: Custom/service Stripe-only

**Fixture**: `classify-custom-stripe-only.json`  
**Expected**: `CustomService`; no missing-billing from truth side

## Local Aspire Run

```powershell
cd src/BillDrift.AppHost
dotnet run
```

Verify API health: `GET /health`  
After implementation:

```powershell
# Config internal Mex IDs
PUT /api/classification-config/internal-mex-ids
Content-Type: application/json
["INTERNAL-MEX-001"]

# Get classification for item
GET /api/classifications/{stableKey}
```

## Azure Table Verification (emulator)

Use Azure Storage Explorer or `az storage entity show` against Azurite:

- Table `itemclassifications`
- After override: row in `PartitionKey=item` with `Source=ManualOverride`
- History row in `PartitionKey=hist`

## Success Criteria Mapping

| Spec SC | Validated by |
|---------|--------------|
| SC-002 | V2 |
| SC-003 | V3 |
| SC-004 | V6 |
| SC-007 | V1 |
| SC-008 | V7 |

## Validation Checklist (2026-07-02)

| Scenario | Status | Notes |
|----------|--------|-------|
| V1 Microsoft CSP | PASS | `ClassificationRuleEngineTests` + `classify-csp-full-signals` builder |
| V2 Internal suppression | PASS | `ClassificationIntegrationTests.InternalCustomer_*` |
| V3 Non-CSP manual review | PASS | `ClassificationIntegrationTests.NonCspSupplier_*` |
| V4 Override precedence | PASS | `ClassificationOverrideTests` |
| V5 Override clear | PASS | `ClassificationOverrideTests` |
| V6 Determinism | PASS | `ClassificationServiceTests` |
| V7 Conservative default | PASS | `ClassificationRuleEngineTests` |
| V8 Custom/service Stripe-only | PASS | `ClassificationIntegrationTests.CustomService_*` |

Run: `dotnet test tests/BillDrift.Application.Tests --filter "FullyQualifiedName~Classification"`

## Related Contracts

- Rules: [classification-rules.md](./contracts/classification-rules.md)
- Pipeline: [classification-pipeline.md](./contracts/classification-pipeline.md)
- Storage: [azure-table-schema.md](./contracts/azure-table-schema.md)
- Engine hooks: [reconciliation-integration.md](./contracts/reconciliation-integration.md)

## Out of Scope for This Quickstart

- Fluent UI Blazor classification screens (future feature; use Fluent UI Blazor skill when implementing)
- Entra ID authenticated operator identity (use placeholder `OperatorId` in v1 tests)
