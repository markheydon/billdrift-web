<!--
Sync Impact Report
==================
Version change: 1.0.0 → 1.1.0
Modified principles:
  - I. Code Quality & Maintainability → expanded with mandatory code commenting rules
Added sections: None
Removed sections: None
Templates requiring updates:
  - .specify/templates/plan-template.md ✅ no change (Constitution Check filled dynamically)
  - .specify/templates/spec-template.md ✅ no change (no comment-specific constraints)
  - .specify/templates/tasks-template.md ✅ updated (Polish phase comment verification)
  - .specify/templates/commands/*.md — N/A (no command files present)
  - README.md ✅ no changes required
Follow-up TODOs: None
-->

# BillDrift Constitution

## Core Principles

### I. Code Quality & Maintainability

All code MUST be clear, reviewable, and structured for long-term maintenance in an
open-source billing-reconciliation tool.

- Domain logic (ingestion, reconciliation, discrepancy detection, approval, Stripe
  actions) MUST live in well-named modules with single, explicit responsibilities.
- Public interfaces between modules MUST be typed and documented; implicit coupling
  across ingestion sources (Giacom PDF, Stripe CSV/API) is prohibited.
- Code comments are REQUIRED, not optional. Every module, public interface, and
  non-trivial algorithm MUST include comments that explain intent, business rules,
  and non-obvious behavior.
- Billing and reconciliation logic (comparison rules, tolerance handling, field
  mapping, parser assumptions, Stripe mutation semantics) MUST be commented so
  operators and contributors can verify correctness without reverse-engineering.
- Comments MUST explain why a decision was made, not merely restate what the code
  does; redundant comments that duplicate obvious code are discouraged, but
  absence of required comments is prohibited.
- Linting and formatting MUST pass in CI before merge; style drift is not accepted.
- Complexity beyond the simplest working design MUST be documented in the feature plan
  Complexity Tracking table with rejected alternatives.
- Dead code, commented-out blocks, and unused dependencies MUST be removed before merge.

**Rationale**: Billing drift resolution depends on trustworthy, inspectable logic.
Maintainable code reduces the risk of silent reconciliation errors and lowers the
barrier for community contribution. Mandatory comments make billing rules auditable
and reduce reliance on tribal knowledge when reconciling supplier data against Stripe.

### II. Testing Standards (NON-NEGOTIABLE)

Billing-critical behavior MUST be proven by automated tests before it ships.

- Reconciliation, mismatch detection, and quantity/pricing comparison logic MUST have
  unit tests covering normal cases, edge cases, and known Giacom/Stripe drift patterns.
- Ingestion pipelines (Giacom PDF parsing, Stripe CSV/API normalization) MUST have
  integration tests using representative fixtures; parsers MUST NOT ship without
  regression fixtures for each supported format variant.
- Contract tests MUST guard external boundaries (Stripe API shapes, file upload
  handlers) whenever schemas or endpoints change.
- For billing-critical features, tests MUST be written or updated first, MUST fail
  before implementation, and MUST pass before merge (red-green-refactor).
- Test names and assertions MUST describe business outcomes (e.g., "flags quantity
  mismatch when Giacom seats exceed Stripe subscription quantity"), not implementation
  details.

**Rationale**: Incorrect reconciliation directly causes missed revenue or customer
overbilling. Tests are the primary safety net for a domain where manual spreadsheets
are the alternative.

### III. Consistent User Experience

The product MUST feel coherent, predictable, and safe for operators reconciling
supplier billing against Stripe subscriptions.

- Terminology MUST be consistent across UI, API responses, logs, and documentation
  (e.g., "discrepancy", "corrective action", "approval", "dry run").
- Discrepancy views MUST clearly show what differs (product, quantity, price, period)
  and what corrective action is proposed before any change is applied.
- Destructive or bill-impacting actions MUST follow the same approval pattern: review
  → explicit confirm → optional dry run → apply; shortcuts that bypass review are
  prohibited.
- Error, empty, and loading states MUST be handled consistently; failures MUST tell the
  operator what went wrong and what to do next without exposing secrets or stack traces.
- Accessibility and responsive layout MUST be considered for primary workflows; new UI
  MUST reuse established components and patterns rather than one-off implementations.

**Rationale**: Users adopt BillDrift to remove manual reconciliation headache. An
inconsistent or opaque UX recreates the confusion the tool is meant to eliminate.

### IV. Security by Design

Security MUST be treated as a requirement, not a polish item, because the tool handles
financial data and third-party API credentials.

- Secrets (Stripe API keys, tokens, connection strings) MUST NEVER be committed,
  logged, or returned in API responses; use environment variables or a secrets manager.
- Stripe and file-ingestion integrations MUST follow least-privilege scopes; write
  access MUST only be used for explicitly approved corrective actions.
- Uploaded files (Giacom PDFs) MUST be validated (type, size limits, malware-safe
  handling) and processed with parser failures isolated from unrelated tenant data.
- Authentication and authorization MUST protect all endpoints that access billing
  data or trigger Stripe mutations; unauthenticated access to sensitive routes is
  prohibited.
- Dependencies MUST be kept current; known high/critical vulnerabilities in direct
  dependencies MUST be remediated or explicitly waived with documented risk acceptance.
- Audit events MUST be recorded for ingestion runs, discrepancy reviews, approvals,
  dry runs, and applied Stripe updates (who, what, when, outcome).

**Rationale**: A billing tool is a high-value target. Credential leakage or
unauthorized subscription changes would undermine user trust and cause direct financial
harm.

### V. Billing Accuracy & Human Control

Reconciliation outcomes MUST be correct, traceable, and never applied without human
approval in the current product scope.

- Reconciliation algorithms MUST be deterministic for a given input snapshot; the same
  Giacom PDF + Stripe data MUST always produce the same discrepancy set.
- Every proposed corrective action MUST be explainable: which rule fired, which
  fields differ, and what Stripe change would result.
- Stripe subscription updates MUST NOT be applied without explicit operator approval;
  fully automated "set and forget" billing pipelines are out of scope until a future
  constitution amendment defines additional safeguards.
- Dry-run or preview mode MUST be available before any write to Stripe so operators
  can validate impact.
- The project MUST remain transparent that it is an independent, unofficial tool not
  affiliated with, endorsed by, or sponsored by Giacom or Stripe; user-facing surfaces
  MUST not imply official partnership.

**Rationale**: BillDrift exists to eliminate missed revenue and prevent overbilling
while removing manual work—not to replace operator judgment with silent automation.

## Domain Constraints

- **Primary users**: Microsoft 365 resellers using Giacom, MSPs billing via Stripe,
  and operators manually reconciling supplier billing against customer subscriptions.
- **v0.1 scope**: Giacom billing PDF ingestion, Stripe subscription ingestion (CSV
  first, API later), manual reconciliation with human approval before corrective
  actions.
- **Out of scope (v0.1)**: Full PSA/billing automation platforms, non-subscription
  billing systems, unattended auto-apply pipelines.
- **License & openness**: The project is open source; contributions MUST preserve
  clarity of billing logic and test coverage expectations defined in this constitution.

## Development Workflow & Quality Gates

- Every feature plan MUST include a Constitution Check gate (pre-research and
  post-design) verifying compliance with Principles I–V.
- Pull requests MUST not merge with failing CI, missing tests for billing-critical
  changes, or unresolved security findings above the project's accepted threshold.
- Code review MUST explicitly confirm: reconciliation correctness, approval/dry-run
  paths for Stripe writes, secret handling, UX consistency for operator-facing
  flows, and adequate code comments on new or modified billing-critical logic,
  public interfaces, and non-obvious implementation choices.
- New ingestion sources or Stripe write capabilities MUST include fixture data,
  contract tests, and operator-facing documentation before release.
- Runtime development guidance lives in feature `quickstart.md` files and `README.md`;
  agents and contributors MUST consult the current feature plan for stack-specific
  commands.

## Governance

This constitution supersedes ad-hoc practices for BillDrift feature work. When
implementation guidance conflicts with a principle here, the constitution wins unless
formally amended.

**Amendment procedure**:

1. Propose changes with rationale and version bump type (MAJOR/MINOR/PATCH).
2. Update `.specify/memory/constitution.md` and propagate required template changes.
3. Record the amendment date and sync impact in the constitution HTML comment header.

**Versioning policy**: Semantic versioning—MAJOR for principle removals or breaking
redefinitions; MINOR for new principles or materially expanded guidance; PATCH for
clarifications and non-semantic wording.

**Compliance review**: Feature specs, plans, and tasks generated via Spec Kit MUST be
reviewed against this constitution before implementation begins and again before merge.

**Version**: 1.1.0 | **Ratified**: 2026-06-26 | **Last Amended**: 2026-07-01
