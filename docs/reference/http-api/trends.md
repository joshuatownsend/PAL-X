---
title: Trends
description: Multi-job trend evaluation — which findings are appearing, worsening, or resolving over the last N jobs.
---

# Trends

Trends roll up the last `N` completed jobs in the workspace and emit per-rule trend categories:

- `appearing` — found in recent jobs, absent in older ones.
- `worsening` — present throughout but with degrading statistics.
- `improving` — present throughout but with improving statistics.
- `resolved` — present in older jobs, absent recently.
- `unchanged` — stable across the window.

Categorisation is purely statistical (slope of severity counts / aggregated metric values across the window) — no inference or thresholds beyond what's documented in `TrendAnalyzer`.

| Endpoint | Verb | Auth |
|---|---|---|
| `…/trends/data` | `GET` | required |

## `GET /api/workspaces/{workspaceId}/trends/data`

Compute trends across the most recent `N` completed jobs.

### Query

| Param | Default | Notes |
|---|---|---|
| `last` | `10` | Number of recent jobs to include in the window. |

The route is `/trends/data` (not `/trends`) to avoid a routing conflict with the Blazor `@page "/trends"` UI route — same convention applies on the [Correlations](correlations.md) page.

### Response

```json
{
  "windowSize": 10,
  "items": [
    {
      "ruleId": "high-cpu-sustained",
      "category": "cpu",
      "canonicalMetric": "processor.percent_processor_time",
      "trend": "worsening",
      "firstSeenAt": "…",
      "lastSeenAt": "…",
      "samples": [...]
    }
  ]
}
```

### Example

```bash
curl "http://localhost:5043/api/workspaces/$WS/trends/data?last=20" \
  -H "Authorization: Bearer pal_xxx"
```

## Related

- **[`pal remote trends`](../cli/pal-remote-trends.md)** — CLI front-end.
- **[Correlations](correlations.md)** — sibling analytic surface, same routing pattern.
- **[Diagnostics](analysis-jobs.md#get-analysisiddiagnostics)** — guided insights cite trend rows by `affectedRuleIds`.
