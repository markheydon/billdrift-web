# Research: Billing Reconciliation Engine

**Feature**: `004-reconciliation-engine`  
**Date**: 2026-07-02

## R1: Pipeline Architecture

**Decision**: Implement reconciliation as an **ordered multi-stage pipeline** orchestrated by `ReconciliationPipeline`, sharing a mutable-per-run `ReconciliationContext` (indexes, match groups, mismatches, proposed changes) that is not exposed outside the Application layer.

**Rationale**:
- Constitution Principle I requires single-responsibility modules with explicit billing rule comments.
- Stages map directly to spec user stories: indexing → match groups → subscription truth → supplier cost → catalogue → ordering.
- Easier to test each stage in isolation and to audit rule ordering (mapping before quantity/price).

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Single monolithic `Execute` method | Hard to test, violates maintainability principle |
| Infrastructure-hosted engine | Reconciliation is pure domain logic over normalized inputs; no I/O |
| Event-driven reactive pipeline | Over-engineered for v1; harder to guarantee deterministic ordering |

---

## R2: Product Resolution Priority

**Decision**: Resolve `CommercialKey` / `CommercialKeyRoot` using this **fixed priority chain** (first success wins; record `MatchConfidence`):

1. **Explicit offer/SKU** on subscription truth line or supplier line (when both present → `High`)
2. **Stripe product metadata** offer/SKU on matched or candidate Stripe item (both present → `High`; partial → `Medium`)
3. **ProductMapping lookup** by `CommercialKeyRoot` when offer/SKU known from step 1 on one side only
4. **Supplier name variant** via `IProductMappingResolver` exact normalized match (`Medium`)
5. **Deterministic fuzzy name fallback** (see R3) (`Low`)

**Rationale**: Implements FR-004–FR-006 and user story 2. Higher-confidence paths always beat lower-confidence paths. Partial metadata never auto-matches at `High` confidence.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Product name as primary join key | Fragile across Giacom naming variants; rejected in 001 R7 |
| ML/embedding similarity | Non-deterministic; violates FR-015 |
| Stripe product ID as universal key | Not present on subscription truth or supplier lines |

---

## R3: Deterministic Fuzzy Name Fallback

**Decision**: When steps 1–4 fail, apply **`DeterministicFuzzyNameMatcher`** using **token-set Jaccard similarity** on normalized product names (lowercase, punctuation stripped, whitespace collapsed):

- Score = |intersection(tokens)| / |union(tokens)|
- Minimum threshold: **0.85** for a candidate to qualify
- Tie-break: highest score, then lexicographically smallest `CommercialKeyRoot` string, then mapping ID
- **0 candidates** → `MappingMissing`
- **1 candidate** → match at `Low` confidence; **no bill-impacting `ProposedChange`** unless corroborated by offer/SKU on another domain entity in the same group
- **2+ candidates above threshold** → `MappingAmbiguous`

**Rationale**: Spec requires fuzzy fallback (FR-006) while constitution requires determinism (FR-015). Token Jaccard is reproducible, dependency-free, and auditable. Low-confidence guard prevents silent billing changes from name guesswork.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Levenshtein via NuGet | External dependency; 001 domain stays dependency-free |
| No fuzzy fallback | Violates spec FR-006 |
| Auto-propose on fuzzy match | Violates SC-008 and constitution V |

---

## R4: Intended Price Resolution

**Decision**: Build `IntendedPriceIndex` keyed by `CommercialKey`:

- Insert all catalogue-sourced prices first
- Overlay manual overrides (`PriceSource.ManualOverride`) — **override wins on key collision** (FR-017)
- Missing key → no invented price; downstream stages flag pricing reference gap in mismatch description

**Rationale**: Implements FR-017 and spec edge case for override precedence. Index built once per run for O(1) lookup during price comparison.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Resolve at comparison time via LINQ scan | O(n²) for large price lists |
| Lowest price wins on collision | Violates spec manual override rule |

---

## R5: Match Group Construction Strategy

**Decision**: **Subscription truth lines drive primary match group creation** for active subscriptions in scope:

1. For each active `MicrosoftSubscriptionLine` in scope → create or find `EntityMatchGroup` keyed by `(MexId, CommercialKeyRoot, Term, Frequency)`
2. Attach matching `StripeBillingItem` via `StripeItemMatcher` (same customer + commercial key + compatible price term)
3. Attach `IntendedPrice` from index
4. Attach `SupplierCostLine`(s) for same customer + commercial key within billing period scope (recurring + pro-rata as separate attached lines, pro-rata excluded from quantity sum)
5. Orphan supplier lines (no truth match) get standalone groups for visibility (spec user story 3 scenario 5)
6. Catalogue-only gaps discovered from required keys on truth lines + mappings → synthetic groups without subscription line

**Rationale**: Subscription truth defines expected billing state. Supplier cost enriches but does not drive Stripe comparison logic. Prevents supplier-only noise from creating false Stripe mismatches.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Full outer join on all four domains | Creates combinatorial explosion and ambiguous groups |
| Stripe-driven grouping | Stripe is source of truth for *actual* billing, not *expected* state |

---

## R6: Non-CSP Supplier Line Handling

**Decision**: When `ProductMapping.Classification == NonCsp` or line cannot be classified as CSP:

- If `IncludeNonCspProducts == false` (default): attach line to group if customer resolvable, emit **`MappingMissing`** mismatch with description prefix `"Non-CSP line requires manual mapping:"`, **no bill-impacting proposed actions**
- If `IncludeNonCspProducts == true`: participate in normal matching but still flag with informational severity for operator awareness

**Rationale**: Implements FR-009, FR-012, SC-006. Uses existing `MismatchType.MappingMissing` with distinct operator-facing description rather than adding a new enum value (domain model has 7 types; spec's "non-CSP" category is a sub-classification of mapping issue).

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| New `MismatchType.NonCspManualReview` enum | Requires domain model change; description-based distinction sufficient for v1 |
| Silent skip | Violates SC-006 |

---

## R7: Duplicate Stripe Item Detection

**Decision**: When multiple `StripeBillingItem` records match the same `(Customer, CommercialKey)` for an active subscription truth line:

- Do **not** pick arbitrarily
- Emit **`MappingAmbiguous`** mismatch referencing all candidate Stripe item IDs
- No quantity/price comparison until resolved

**Rationale**: Spec edge case — duplicate billing must not silently merge. Ambiguous mapping blocks false corrective actions.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Match first by Stripe ID sort | Hides duplicate billing problem |
| Sum quantities across duplicates | Assumes intentional split billing without operator confirmation |

---

## R8: Quantity Aggregation Rules

**Decision**:

- **Subscription truth quantity**: `LicenceCount` on the matched line
- **Stripe quantity**: `Quantity` on matched `StripeBillingItem`
- **Supplier quantity for comparison display**: sum of `Quantity` on `Recurring` charge type lines only; **`ProRatedAdjustment` lines attached but excluded from sum** (FR-016)

**Rationale**: Pro-rata credits/adjustments must not inflate seat counts (001 contract test requirement).

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Include all supplier quantities | Double-counts mid-period adjustments |
| Ignore supplier quantity entirely | Operator loses visibility in match group |

---

## R9: Price Comparison and Tolerance

**Decision**:

- Compare Stripe `UnitAmount` to `IntendedPrice.Rrp` for the same `CommercialKey`
- Use `ReconciliationOptions.PriceTolerance` (absolute `Money`, default zero)
- Mismatch when `|stripe - intended| > tolerance`
- **`BillingFrequencyMismatch`** checked before price: if Stripe price interval ≠ expected term/frequency from truth/mapping, flag frequency first
- When correct alternate Stripe price exists in catalogue index → propose `SwitchPrice`; when no alternate exists → mismatch only, no switch proposal

**Rationale**: Ordered rule priority prevents misleading price mismatches on wrong-interval prices (001 R7).

---

## R10: Testing Strategy

**Decision**:

- **Unit tests** per stage, matcher, and detector in `BillDrift.Application.Tests`
- **Integration tests** via full `ReconciliationEngine.Execute` with JSON `ReconciliationInputs` fixtures
- **Golden-run comparison**: serialize mismatch set (type + customer + commercial key + description) for determinism
- **One fixture minimum per `MismatchType`** plus clean all-match fixture

**Rationale**: Constitution Principle II (NON-NEGOTIABLE). Application layer owns the algorithm; tests prove business outcomes.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Domain-only tests | Engine logic lives in Application; domain tests already cover entity construction |
| Property-based fuzzing only | Harder to tie to named business scenarios operators understand |

---

## R11: Application Layer Placement

**Decision**: Keep **`IReconciliationEngine` in Application**; implementation in **`BillDrift.Application.Reconciliation`** namespace; **no Infrastructure project involvement**.

**Rationale**: Engine is pure orchestration over domain types. Matches 001 R5 normalization boundary — ingestion in Infrastructure, business rules in Application.

**Alternatives considered**:

| Alternative | Rejected because |
|-------------|------------------|
| Engine in Domain | Domain should stay entity + validation rules only |
| Engine in Infrastructure | No external I/O; would imply hidden side effects |
