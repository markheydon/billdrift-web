# Contract: Azure Table Schema

**Feature**: `006-reconciliation-classification`  
**Storage**: Azure Table Storage via Aspire-injected `TableServiceClient`  
**Date**: 2026-07-02

## Table

**Default name**: `itemclassifications` (override via `ClassificationStorageOptions.TableName`)

Create table idempotently on first store access.

## Entity Types

### Current Classification / Override (`PartitionKey = "item"`)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `PartitionKey` | string | Yes | Constant `"item"` |
| `RowKey` | string | Yes | URL-encoded `StableKey` (max 1024) |
| `Kind` | string | Yes | `ReconciliationItemKind` name |
| `CustomerMexId` | string | Yes | Mex ID value |
| `Classification` | string | Yes | Enum name |
| `Source` | string | Yes | `Automatic` or `ManualOverride` |
| `RuleBasis` | string | Yes | Rule trace string |
| `Confidence` | string | Yes | Enum name |
| `OverrideNotes` | string | No | Override explanation |
| `OperatorId` | string | No | Last operator |
| `UpdatedAt` | DateTimeOffset | Yes | UTC timestamp |
| `EntityId` | string | No | In-run GUID string |

**RowKey encoding**: `Uri.EscapeDataString(stableKey)` — decode on read.

### History Entry (`PartitionKey = "hist"`)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `PartitionKey` | string | Yes | Constant `"hist"` |
| `RowKey` | string | Yes | `{encodedStableKey}:{utcTicks:D19}` |
| `ItemStableKey` | string | Yes | Decoded key for queries |
| `PriorClassification` | string | No | Null for first entry |
| `NewClassification` | string | Yes | Enum name |
| `Source` | string | Yes | Source enum |
| `Notes` | string | No | Override notes |
| `OperatorId` | string | No | Actor |
| `Timestamp` | DateTimeOffset | Yes | Event time |

**Query history**: `PartitionKey eq 'hist' and ItemStableKey eq '{key}'` — ordered by RowKey descending, `Take(limit)`.

### Configuration — Internal Mex IDs (`PartitionKey = "config"`, `RowKey = "internal-mex-ids"`)

| Property | Type | Description |
|----------|------|-------------|
| `Json` | string | `["MEX001","MEX002"]` serialized Mex ID strings |
| `UpdatedAt` | DateTimeOffset | Last config change |
| `OperatorId` | string | Who updated |

### Configuration — Product Category Rules (`PartitionKey = "config"`, `RowKey = "product-category-rules"`)

| Property | Type | Description |
|----------|------|-------------|
| `Json` | string | Serialized `ProductCategoryRule[]` |
| `UpdatedAt` | DateTimeOffset | Last config change |
| `OperatorId` | string | Who updated |

## Blob Usage (secondary)

**Container**: `classification-config` (default)

Optional `config-snapshot-{timestamp}.json` written on config update for audit export. **Not** used for operational reads during reconciliation.

Inject `BlobServiceClient` via Aspire; use only in `ClassificationConfigExporter` if implemented.

## Access Patterns

| Operation | Pattern | Frequency |
|-----------|---------|-----------|
| Load config | 2 point reads (`config` partition) | Once per reconciliation run |
| Load overrides | Point read per `StableKey` or batch | Per item (cache per run) |
| Save override | Upsert `item` + insert `hist` | Operator action |
| Clear override | Delete `item` row if Source was override + insert `hist` | Operator action |

## Emulator / Local Dev

Azurite tables via Aspire `RunAsEmulator()`. No connection strings in application code.

## Security

- No secrets in table entities
- `OperatorId` is display identity from future Entra auth; v1 may use `"system"` or `"operator"` placeholder
- API endpoints require auth when Entra integration lands (out of scope v1 — document in API plan)

## Migration

Greenfield table — no migration from prior features. Existing reconciliation tests use `InMemoryItemClassificationStore` with empty config.
