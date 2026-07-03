# Run History Test Fixtures

Maps fixture files to `specs/008-reconciliation-run-history/quickstart.md` validation scenarios.

| Fixture | Scenario |
|---------|----------|
| `jan-2026-run.json` | V1 — Persist creates immutable run record |
| `feb-2026-run.json` | V4, V6 — List/filter and month-to-month comparison |
| `recurring-quantity-drift/` | V8 — Recurring drift trend (3+ occurrences) |
| `pricing-lag-timeline/` | V9 — Pricing drift timeline with RRP lag |

Fixtures contain serialized `ReconciliationRun` + `RunArchiveContext` pairs for integration tests.
