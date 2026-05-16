---
title: pal remote baselines
description: List baselines and designate completed jobs as baselines.
---

# `pal remote baselines`

Manage baseline designations. Two subcommands:

| Subcommand | Purpose |
|---|---|
| [`list`](#pal-remote-baselines-list) | List designated baselines, optionally filtered by type. |
| [`set`](#pal-remote-baselines-set) | Designate (or clear) a completed job as a baseline. |

See the **[baselines glossary entry](../../getting-started/glossary.md#baseline)** for the type and versioning model.

---

## `pal remote baselines list`

### Synopsis

```text
pal remote baselines list [OPTIONS]
```

### Options

| Option | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Workspace-prefixed base URL. |
| `--api-key <pal_…>` | *(unset)* | Personal access token. |
| `-t`, `--type <TYPE>` | none | Filter by baseline type: `machine`, `role`, `workload`, `release`. |

### Examples

All baselines in the workspace:

```bash
pal remote baselines list --api $PAL_API --api-key $PAL_TOKEN
```

Only `machine`-type baselines:

```bash
pal remote baselines list \
  --api $PAL_API --api-key $PAL_TOKEN \
  --type machine
```

### Exit codes

| Code | Meaning |
|---|---|
| `0` | List retrieved. |
| `1` | Server error. |

---

## `pal remote baselines set`

### Synopsis

```text
pal remote baselines set <job-id> [OPTIONS]
```

### Arguments

| Argument | Purpose |
|---|---|
| `<job-id>` | Job ID (GUID) to designate as a baseline. |

### Options

| Option | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Workspace-prefixed base URL. |
| `--api-key <pal_…>` | *(unset)* | Personal access token. |
| `-l`, `--label <TEXT>` | none | Human-readable label (e.g. `WEB-01 baseline`). |
| `-t`, `--type <TYPE>` | none | Baseline type: `machine`, `role`, `workload`, `release`. |
| `-c`, `--context <JSON>` | none | Arbitrary context JSON (e.g. `'{"machine":"WEB-01"}'`). Used for versioning. |
| `--clear` | off | Remove the baseline designation from this job (inverse of set). |

### Examples

Designate a job as a machine baseline:

```bash
pal remote baselines set \
  --api $PAL_API --api-key $PAL_TOKEN \
  --type machine --label "WEB-01 release-2026.04" --context '{"machine":"WEB-01"}' \
  9c2a14e0-...
```

Clear the designation:

```bash
pal remote baselines set \
  --api $PAL_API --api-key $PAL_TOKEN \
  --clear 9c2a14e0-...
```

### Versioning

Multiple baselines sharing the same `(type, context)` are treated as versions, ordered by `CreatedAt` descending. The newest acts as the active baseline; older entries remain queryable.

### Exit codes

| Code | Meaning |
|---|---|
| `0` | Designation set or cleared. |
| `2` | Malformed job ID or `--context` JSON. |
| `1` | Job not found or server error. |

## Related

- **[pal remote compare](pal-remote-compare.md)** — compare against a baseline.
- **[pal remote submit](pal-remote-submit.md)** with `--baseline` — auto-compare on the next job submission.
