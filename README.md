# BillDrift - Web App

> Stripe subscriptions drift. Supplier billing changes. BillDrift keeps them in sync.

BillDrift is an open-source tool designed to detect and resolve billing drift between supplier billing data and Stripe subscriptions.

It is particularly useful for Microsoft 365 resellers using Giacom, where subscription quantities, products, and pricing can frequently change over time.

BillDrift ingests Giacom billing PDFs and Stripe subscription data, reconciles them, highlights discrepancies, and allows you to approve corrective actions before applying updates.

The goal is simple: eliminate missed revenue, prevent overbilling, and remove the manual headache.

**Disclaimer**: BillDrift is an independent, unofficial tool and is not affiliated with, endorsed by, or sponsored by Giacom or Stripe.

## Who this is for

- Microsoft 365 resellers using Giacom
- MSPs billing subscriptions via Stripe
- Anyone manually reconciling supplier billing vs customer subscriptions

## Who this is NOT for (yet)

- Full PSA / billing automation platforms
- Non-subscription billing systems
- Fully automated “set and forget” billing pipelines

---

# Development / Coming Soon

## Current Scope (v0.1)

- Giacom billing PDFs (pre/post billing reports).
- Stripe subscriptions (CSV now, API later).
- Manual reconciliation workflow (human approval step).

Future versions will expand automation and provider integrations.

## Roadmap

- [ ] Core domain model & reconciliation engine
- [ ] PDF ingestion (Giacom)
- [ ] Stripe CSV ingestion
- [ ] Reconciliation + mismatch detection
- [ ] Approval workflow (manual)
- [ ] Stripe API integration (read)
- [ ] Stripe API updates (with approval + dry run mode)
