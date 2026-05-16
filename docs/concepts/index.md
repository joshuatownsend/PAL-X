---
title: Concepts
description: How PAL-X thinks — the design rationale behind packs, findings, baselines, and the analytical surfaces.
---

# Concepts

Concept pages explain the **WHY** behind PAL-X — design decisions, trade-offs, and the mental model you need to use the tool effectively. They link out to **[Reference](../reference/index.md)** for the *what* (fields, endpoints, flags) and to **[Guides](../guides/index.md)** for the *how* (step-by-step tasks).

If you want to know what a specific field does, look in Reference. If you want to know what to type to accomplish X, look in Guides. If you want to know why PAL-X works the way it does — what trade-off it made, what alternative it considered — start here.

## Pages

- **[Packs and rules](packs-and-rules.md)** — the unit of distribution and the unit of evaluation; why declarative not DSL.
- **[Datasets and inputs](datasets-and-inputs.md)** — how CSV/BLG inputs become the dataset the engine evaluates; host_context, canonical IDs, determinism.
- **[Baselines and comparisons](baselines-and-comparisons.md)** — the designate-a-job model, implicit versioning, what the diff actually reports.
- **[Analytics surfaces](analytics-surfaces.md)** — trends, correlations, and guided diagnostics; descriptive not causal.
- **[Alerting and notification](alerting-and-notification.md)** — the 3-of-5 policy, alert lifecycle, webhook events, end-to-end automation shape.
- **[Multitenancy and auth](multitenancy-and-auth.md)** — orgs, workspaces, two auth schemes, three roles, default tenant.
