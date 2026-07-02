# Contract: Approval API Endpoints

**Feature**: `007-reconciliation-approval-workflow`  
**Project**: `BillDrift.Api/Approval/ApprovalEndpoints.cs`  
**Date**: 2026-07-02

## Base Path

```text
/api/reconciliation/{runId:guid}/approvals
```

All endpoints require operator context (`IOperatorContext.OperatorId`). Development: `X-Operator-Id` header. Production auth integration deferred.

---

## POST `/ingest`

Ingest proposals from a completed reconciliation run.

**Request body**:

```json
{
  "includeInvestigationItems": true
}
```

**Behaviour**: Loads run from caller-provided pipeline or accepts embedded run reference (implementation: call engine + surfacing + classification then ingest — or ingest from persisted run snapshot when available).

**Response** `200`:

```json
{
  "runId": "guid",
  "ingestedCount": 12,
  "pendingCount": 8,
  "investigationCount": 4,
  "supersededCount": 2
}
```

**Idempotent** per `runId`.

---

## GET `/`

Get approval queue view model.

**Query**:
- `customerMexId` (optional filter)
- `includeCatalogue` (default `true`)
- `includeInvestigation` (default `true`)

**Response** `200`: `ApprovalQueueViewModel` (see data-model.md)

---

## GET `/{proposalId:guid}`

Get single proposal detail.

**Response** `200`: `ApprovalProposalViewModel`  
**Response** `404`: Not found

---

## POST `/{proposalId:guid}/approve`

**Request body**:

```json
{
  "acknowledgeStale": false
}
```

**Response** `200`: `ApprovalDecision`  
**Response** `409`: Invalid state / stale without ack  
**Response** `422`: Ineligible

---

## POST `/{proposalId:guid}/reject`

**Request body**:

```json
{
  "reason": "Mapping confirmed incorrect; will fix in Stripe manually"
}
```

**Response** `200`: `ApprovalDecision`  
**Response** `400`: Missing reason

---

## POST `/bulk-approve/preview`

**Request body**:

```json
{
  "customerMexId": "MEX123",
  "proposalIds": ["guid", "guid"]
}
```

**Response** `200`:

```json
{
  "confirmationToken": "hash",
  "summary": {
    "count": 2,
    "subscriptionActions": 1,
    "catalogueActions": 1
  }
}
```

---

## POST `/bulk-approve`

**Request body**:

```json
{
  "confirmationToken": "hash",
  "customerMexId": "MEX123",
  "proposalIds": ["guid", "guid"]
}
```

**Response** `200`: `BulkApproveResult` with decisions array  
**Response** `409`: Token mismatch or invalid state

---

## POST `/export`

**Request body**:

```json
{
  "customerMexId": null
}
```

**Response** `200`: `ApprovedChangeset` with `exportId`, `entryCount`, `downloadUrl`  
**Response** `422`: No approved items

---

## GET `/export/{exportId:guid}/download`

Returns blob JSON (`application/json`) with content-disposition attachment.

---

## GET `/audit`

**Query**: `proposalId` (optional)

**Response** `200`: Array of `ApprovalAuditEvent` ordered desc by timestamp

---

## OpenAPI

Map endpoints in development via existing `MapOpenApi()`. Tag: `Approval`.

---

## Web Client

`BillDrift.Web/Services/ApprovalApiClient.cs` implements typed client for above endpoints using `HttpClient` base `https+http://api` from Aspire service discovery.
