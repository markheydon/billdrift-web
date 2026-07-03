# Quickstart: Reconciliation Run History & Audit

**Feature**: `008-reconciliation-run-history`  
**Date**: 2026-07-02

## Prerequisites

- .NET 10 SDK
- Repository built: `dotnet build BillDrift.sln`
- Aspire AppHost with Azure Storage emulator (Azurite) — tables + blobs wired in `AppHost.cs`
- Features 001–007 implemented (domain, ingestion, reconciliation engine, exceptions, classification, approval)
- Branch: `008-reconciliation-run-history`

## Run Tests (primary validation)

```powershell
# Application unit tests — comparison, trends, stable keys
dotnet test tests/BillDrift.Application.Tests --filter "FullyQualifiedName~History"

# Infrastructure table/blob store tests (Azurite)
# Default dotnet test skips these in <1s when Azurite is not running.
# With Azurite up, run explicitly:
dotnet test tests/BillDrift.Infrastructure.Tests --filter "Category=Integration&FullyQualifiedName~RunHistory"

# API integration tests
dotnet test tests/BillDrift.Api.Tests --filter "FullyQualifiedName~RunHistory"
```

## Validation Scenarios

### V1: Persist creates immutable run record

**Fixture**: `tests/fixtures/run-history/jan-2026-run.json`  
**Steps**: Complete reconciliation → `POST /api/run-history` with run + context  
**Expected**: Status `Completed`; table `run` row; blobs under `{runId}/`; manifest hash validates

### V2: All input domains marked present or absent

**Fixture**: Run with missing Stripe input  
**Expected**: `InputSnapshots` has five entries; Stripe `IsPresent=false`; run still `Completed`

### V3: Re-persist completed run rejected

**Steps**: Persist run → attempt second persist same `RunId`  
**Expected**: HTTP 409

### V4: List runs by billing period

**Setup**: Persist runs for Jan and Feb 2026  
**Steps**: `GET /api/run-history?billingPeriodStart=2026-02-01&billingPeriodEnd=2026-02-28`  
**Expected**: Only February run returned

### V5: Run detail includes approval status links

**Setup**: Persist run → ingest approvals (007) → approve one proposal  
**Steps**: `GET /api/run-history/{runId}?includeResults=true`  
**Expected**: `proposalStatusLinks` shows mixed states; blob proposals unchanged

### V6: Month-to-month comparison classifies exceptions

**Fixtures**: `jan-2026-run.json`, `feb-2026-run.json`  
**Steps**: `POST /api/run-history/compare` with earlier=Jan, later=Feb  
**Expected**: Delta report with new/resolved/persisting lists; SC-003 classification

### V7: Mapping version change flagged

**Setup**: Two runs with different mapping content hashes  
**Expected**: `mappingVersionChanged=true` in comparison report

### V8: Recurring drift trend surfaces 3+ occurrences

**Fixture**: `tests/fixtures/run-history/recurring-quantity-drift/` (4 runs)  
**Steps**: `GET /api/run-history/trends/drift?fromDate=...&toDate=...&minOccurrences=3`  
**Expected**: Entry with `OccurrenceCount >= 3` (SC-004)

### V9: Pricing drift timeline shows RRP lag

**Fixture**: `tests/fixtures/run-history/pricing-lag-timeline/`  
**Steps**: `GET /api/run-history/trends/pricing?offerId=...&skuId=...`  
**Expected**: `CatalogueMissing` or amount mismatch events; `lagRunsPersisted >= 2` (SC-005)

### V10: Failed run retained

**Steps**: Persist with invalid/partial engine output  
**Expected**: `Status=Failed`; `FailureReason` populated; partial blobs if written

### V11: Blob integrity check

**Steps**: Tamper blob hash in test → read run detail  
**Expected**: Integrity error logged; API 500 with safe message

### V12: Audit events recorded

**Steps**: Persist + compare + export  
**Expected**: Audit rows for `RunArchived`, `RunCompared`, `RunHistoryExported`

## Local Aspire Run

```powershell
cd src/BillDrift.AppHost
dotnet run
```

1. Open Web frontend URL from Aspire dashboard  
2. Navigate to `/history`  
3. Verify run list renders after persisting a test run  
4. Open run detail → verify input tabs and exception counts  
5. Compare two runs via `/history/compare`  
6. View drift trends at `/history/trends`

## API Smoke (curl)

```powershell
$runId = "00000000-0000-0000-0000-000000000001"

# List runs
curl -H "X-Operator-Id: dev@local" http://localhost:5000/api/run-history

# Run detail
curl -H "X-Operator-Id: dev@local" "http://localhost:5000/api/run-history/$runId"

# Compare
curl -X POST -H "Content-Type: application/json" -H "X-Operator-Id: dev@local" `
  -d '{"earlierRunId":"...","laterRunId":"..."}' `
  http://localhost:5000/api/run-history/compare
```

## Success Criteria Mapping

| SC | Validation |
|----|------------|
| SC-001 | V1 + V4 — detail load timing in integration test |
| SC-002 | V2 — all domains explicit |
| SC-003 | V6 — comparison classification fixture |
| SC-004 | V8 — recurring drift fixture |
| SC-005 | V9 — pricing lag fixture |
| SC-006 | V5 — approval join on detail |
| SC-007 | Retention options test (unit) |
| SC-008 | Manual usability — deferred to operator validation |

## Related Contracts

- [run-history-pipeline.md](./contracts/run-history-pipeline.md)
- [azure-table-schema.md](./contracts/azure-table-schema.md)
- [azure-blob-run-archive.md](./contracts/azure-blob-run-archive.md)
- [mismatch-comparison-rules.md](./contracts/mismatch-comparison-rules.md)
- [pricing-drift-timeline.md](./contracts/pricing-drift-timeline.md)

## Storage Verification (Azurite)

```powershell
# After V1, confirm table + blob exist
# Table: reconciliationrunhistory, partition run
# Blob container: reconciliation-runs/{runId}/manifest.json
```

Use Azure Storage Explorer or `az storage` CLI against Azurite connection string from Aspire dashboard.

## Implementation Validation Checklist (2026-07-02)

| Scenario | Status | Evidence |
|----------|--------|----------|
| V1 Persist creates immutable run record | PASS | `RunArchiveServiceTests.Persist_creates_immutable_run_record` |
| V2 All input domains present/absent | PASS | `RunArchiveServiceTests.Persist_marks_all_input_domains_present_or_absent` |
| V3 Re-persist completed rejected | PASS | `RunArchiveServiceTests.Re_persist_completed_run_is_rejected` |
| V4 List runs by billing period | PASS | `RunHistoryServiceTests.ListRuns_filters_by_billing_period` |
| V5 Approval status links on detail | PASS | `RunHistoryServiceTests.GetRunDetail_includes_approval_status_links` |
| V6 Month-to-month comparison | PASS | `RunComparisonServiceTests.Compare_classifies_new_resolved_and_persisting_exceptions` |
| V7 Mapping version change flagged | PASS | `RunComparisonServiceTests.Compare_flags_mapping_version_change` |
| V8 Recurring drift trend | PASS | `DriftTrendAnalyzerTests.Analyze_surfaces_recurring_drift_with_minimum_occurrences` |
| V9 Pricing drift timeline | PASS | `PricingDriftAnalyzerTests` (unit coverage) |
| V10 Failed run retained | PASS | `RunArchiveServiceTests.Failed_run_is_retained_with_failure_reason` |
| V11 Blob integrity check | PASS | `AzureBlobRunArchiveStoreTests` (Azurite when available) |
| V12 Audit events recorded | PASS | `AzureTableRunHistoryStoreTests.Audit_events_append_on_persist_operations` |
