---
title: pal remote compare
description: Diff two completed analysis jobs and show a finding-level summary.
---

# `pal remote compare`

Compare two completed jobs and show which findings improved, worsened, appeared, or disappeared. The same diff the Compare UI page renders.

Unlike `auto-compare` (which fires on submit when `--baseline` was passed), this command is on-demand for any two jobs.

## Synopsis

```text
pal remote compare [OPTIONS]
```

## Options

| Option | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Workspace-prefixed base URL. |
| `--api-key <pal_…>` | *(unset)* | Personal access token. |
| `--baseline <JOB-GUID>` | required | Job ID of the baseline run (the "before"). |
| `--candidate <JOB-GUID>` | required | Job ID of the candidate run (the "after"). |

Both `--baseline` and `--candidate` accept the job GUID directly; they're not positional arguments.

## Example

```bash
pal remote compare \
  --api $PAL_API --api-key $PAL_TOKEN \
  --baseline 1a2b3c4d-... \
  --candidate 9c2a14e0-...
```

## What you get

For each rule that fired in either job:

- A direction marker: `improving`, `worsening`, `appearing`, `disappearing`, or `stable`.
- The before-after evidence (aggregated values).
- The rule's `rule_id`, `severity`, `category`.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Comparison retrieved. |
| `2` | Malformed GUIDs or missing arguments. |
| `1` | Either job not found or other server error. |

## Related

- **[pal remote submit](pal-remote-submit.md)** with `--baseline` — auto-compare on completion instead of running this command manually.
- **[pal remote baselines](pal-remote-baselines.md)** — designate one of the jobs as a baseline for future runs.
- **[pal remote trends](pal-remote-trends.md)** — multi-job equivalent.
