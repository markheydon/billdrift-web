# Exception Surfacing Fixtures

Metadata stubs for exception surfacing integration tests. Actual input data is built programmatically via `ExceptionSurfacingTestDataBuilder` and chained through `ExceptionSurfacingTestBuilder` (engine execute → surfacing).

## Scenarios

| Fixture | quickstart.md | Builder method | Validates |
|---------|---------------|----------------|-----------|
| `mixed-three-customers.json` | Scenario 1 | `MixedThreeCustomers` | Customer group ordering, summary counts |
| `suppression-mapping-root-cause.json` | Scenario 3 | `SuppressionMappingRootCause` | SR-1 mapping suppression |
| `catalogue-consolidation.json` | Scenario 5 | `CatalogueConsolidation` | CR-1 catalogue merge |
| `orphaned-stripe-item.json` | Scenario 4 | `OrphanedStripeItem` | Orphaned billing derived detection |
| `mex-id-mismatch.json` | — | `MexIdMismatch` | Mex ID derived detection |
| `low-confidence-no-action.json` | Scenario 6 | `LowConfidenceNoAction` | SR-3 proposed action strip |
| `clean-run-empty.json` | Scenario 7 | `CleanMatchAllDomains` | Empty state |

Reconciliation scenarios under `tests/fixtures/reconciliation/` are also used directly for mapping and evidence tests (e.g. `quantity-mismatch` for Scenario 2).

## Usage

```csharp
var builder = new ExceptionSurfacingTestBuilder();
var vm = builder.SurfaceScenario("mixed-three-customers");
```

## Coverage map (quickstart.md)

- Scenario 1: `OrderingTests`, `ExceptionSurfacingServiceTests`
- Scenario 2: `EvidenceBuilderTests` (quantity-mismatch)
- Scenario 3: `SuppressionRulesTests`
- Scenario 4: `DerivedDetectorTests`
- Scenario 5: `ConsolidationTests`
- Scenario 6: `SuppressionRulesTests`
- Scenario 7: `ExceptionSurfacingServiceTests`
- Scenario 8: `DeterminismTests`
