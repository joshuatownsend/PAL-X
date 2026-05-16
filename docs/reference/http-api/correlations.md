---
title: Correlations
description: Cross-signal correlations — which metric pairs move together across recent jobs.
---

# Correlations

The correlation analyzer looks across the last `N` completed jobs and emits metric pairs whose summary statistics co-vary. Output rows annotate each pair with a direction (`both-worsening`, `both-improving`, `opposite`) and a confidence based on sample count.

This is descriptive, not causal — a correlation row is a hint that two signals move together, not that one causes the other.

| Endpoint | Verb | Auth |
|---|---|---|
| `…/correlations/data` | `GET` | required |

## `GET /api/workspaces/{workspaceId}/correlations/data`

Compute correlations across the most recent `N` completed jobs.

### Query

| Param | Default | Notes |
|---|---|---|
| `last` | `10` | Number of recent jobs to include. |

The `/correlations/data` path avoids a conflict with the Blazor `@page "/correlations"` route.

### Response

```json
{
  "windowSize": 10,
  "items": [
    {
      "metricA": "processor.percent_processor_time",
      "metricB": "physicaldisk.avg_disk_sec_per_read",
      "direction": "both-worsening",
      "confidence": "high",
      "samples": 10
    }
  ]
}
```

### Example

```bash
curl "http://localhost:5043/api/workspaces/$WS/correlations/data?last=10" \
  -H "Authorization: Bearer pal_xxx"
```

## Related

- **[`pal remote correlations`](../cli/pal-remote-correlations.md)** — CLI front-end.
- **[Trends](trends.md)** — single-metric trends across the same window.
- **[Diagnostics](analysis-jobs.md#get-analysisiddiagnostics)** — both-worsening correlation pairs feed into guided insights.
