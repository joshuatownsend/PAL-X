---
title: Set a baseline
description: Designate a completed job as a baseline and version it via the (type, contextJson) pair.
---

# Set a baseline

Goal: take a completed analysis job, mark it as a baseline, and label it so future comparisons find it. Implicit versioning lets you keep older baselines around without explicit "v1 / v2" entities.

For the API shape, see **[HTTP API — Baselines](../reference/http-api/baselines.md)**. For the model, see **[Concepts — Baselines and comparisons](../concepts/baselines-and-comparisons.md)**.

## 1. Have a completed job

Submit and let it complete. From the CLI:

```bash
pal remote submit --file capture.csv --pack windows-core
# Job ID: 38d1...

pal remote status 38d1...
# Status: completed
```

You need the job's id and a `completed` status.

## 2. Designate via the CLI

```bash
pal remote baselines set 38d1... \
  --label "v2.5.0 reference" \
  --type release \
  --context '{"release":"v2.5.0"}'
```

This calls `PATCH /analysis/{id}/baseline` with the flag set on.

- `--type` must be one of `machine`, `role`, `workload`, `release`.
- `--context` must be valid JSON. The server normalises it (`{"a":1, "b":2}` and `{"b":2,"a":1}` produce the same baseline group).
- `--label` is free-form, only for human display.

## 3. List baselines

```bash
# All baselines in the workspace
pal remote baselines list

# Just the release-type ones
pal remote baselines list --type release
```

The list shows label, type, context, packs, and createdAt. Newest first.

## 4. List versions of one baseline

When you've designated multiple jobs with the same `(type, context)`, you can enumerate the version history:

```bash
pal remote baselines versions \
  --type release \
  --context '{"release":"v2.5.0"}'
```

Each row in the output is one historical designation; the most recent is at the top.

## 5. Revoke

To un-designate (`isBaseline: false`):

```bash
pal remote baselines unset 38d1...
```

The job stays — only its baseline flag clears. The next `baselines list` won't include it.

## Worked example — release baseline workflow

A common pattern: every production release's first post-deploy capture becomes the baseline for that release.

```bash
# After deploying v2.5.0, capture and analyse a sample
pal remote submit --file post-deploy-v2.5.0.csv --pack windows-core
# Job ID: 38d1...

# Wait for it to complete (or use --wait if you prefer)
pal remote status 38d1...

# Designate as baseline
pal remote baselines set 38d1... \
  --label "v2.5.0 production reference" \
  --type release \
  --context '{"release":"v2.5.0"}'

# Three months later, do the same for v2.6.0
pal remote submit --file post-deploy-v2.6.0.csv --pack windows-core
# Job ID: 7e2a...

pal remote baselines set 7e2a... \
  --label "v2.6.0 production reference" \
  --type release \
  --context '{"release":"v2.6.0"}'
```

Both jobs are now baselines under `type: release` but with different `context` values. You can:

- Look up the v2.5.0 baseline: `baselines versions --type release --context '{"release":"v2.5.0"}'`.
- List all release baselines: `baselines list --type release`.
- Compare a new candidate against v2.5.0 directly without remembering the job id.

## Auto-compare on submit

If your workflow always compares each new job to a specific baseline, submit with `--baseline`:

```bash
pal remote submit \
  --file new-capture.csv \
  --pack windows-core \
  --baseline 38d1...
```

The worker runs `IAutoCompareService` against the named baseline as soon as the new job completes — no separate compare call needed. See **[Compare jobs](compare-jobs.md)**.

## Related

- **[Compare jobs](compare-jobs.md)** — using baselines via manual and auto-compare.
- **[CLI — `pal remote baselines`](../reference/cli/pal-remote-baselines.md)** — flags.
- **[HTTP API — Baselines](../reference/http-api/baselines.md)** — under-the-hood requests.
- **[Concepts — Baselines and comparisons](../concepts/baselines-and-comparisons.md)** — why implicit versioning.
