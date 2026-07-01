# Domain Assembly Contract

**Assembly**: `BillDrift.Domain`  
**Version**: v1 (feature `001-billing-domain-model`)

## Purpose

`BillDrift.Domain` is the pure billing-reconciliation domain library. It exposes types and validation rules only — no I/O, no framework dependencies, no persistence attributes.

## Dependency Rules

| Allowed | Prohibited |
|---------|------------|
| .NET BCL (`System`, `System.Collections.Generic`) | Aspire, Azure SDK, EF Core |
| | ASP.NET Core, Blazor, Fluent UI |
| | Stripe.net, PDF parsers |
| | Logging abstractions tied to hosting |

**Downstream consumers**: `BillDrift.Application`, `BillDrift.Infrastructure`, `BillDrift.Api`, test projects.

**Upstream dependencies**: None.

## Public Surface

All types listed in [data-model.md](../data-model.md) are `public` unless prefixed as internal implementation detail (none in v1).

### Namespaces

| Namespace | Contents |
|-----------|----------|
| `BillDrift.Domain.Common` | Value objects, identifiers, enums shared across contexts |
| `BillDrift.Domain.Import` | Raw import records |
| `BillDrift.Domain.Import.Stripe` | Stripe raw export shapes |
| `BillDrift.Domain.Billing` | Normalized billing entities |
| `BillDrift.Domain.Mapping` | Product mapping |
| `BillDrift.Domain.Reconciliation` | Runs, matches, mismatches, proposed changes |

## Immutability Contract

- All entity types are `sealed record`; value objects are `readonly record struct`.
- Collections exposed as `IReadOnlyList<T>` or `IReadOnlyDictionary<TKey, TValue>`.
- No public setters; state changes produce new instances.

## Validation Contract

Validation failures throw `DomainValidationException` (defined in `BillDrift.Domain.Common`) with:
- `PropertyName`
- `Message` (operator-safe, no stack traces in message)

Factory methods (e.g. `Money.Gbp`, `CommercialKey.Create`) perform validation at construction.

## Versioning

Breaking changes to public record shapes require:
1. Major version bump of package (when published)
2. Migration notes in feature plan
3. Contract test updates in `BillDrift.Domain.Tests`

## Binary Compatibility Notes

- Adding optional record parameters with defaults is non-breaking.
- Adding enum values is non-breaking if consumers switch on defaults.
- Removing or renaming public members is breaking.
