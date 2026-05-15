---
title: pal remote
description: Talk to a running PAL API server. Umbrella for every command that goes over the network.
---

# `pal remote`

Umbrella command for everything that runs against a hosted **PAL API server** instead of locally. The same analysis engine, exposed as a service.

## Synopsis

```text
pal remote <COMMAND>
```

## Shared options

Every `pal remote` subcommand inherits these two flags from `RemoteSettings`:

| Flag | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Base URL of the PAL API server. **Must include the workspace path prefix** — e.g. `http://localhost:8080/api/workspaces/00000000-0000-0000-0000-000000000002` — for workspace-scoped subcommands (everything except a few admin-y ones). The CLI joins relative paths (`uploads`, `analysis`, `compare`) to this base. |
| `--api-key <pal_…>` | *(unset)* | Personal access token, minted at `/account/tokens` in the Blazor UI. |

The default workspace ID (`00000000-0000-0000-0000-000000000002`) is seeded by the `AddMultitenancy` migration. The bootstrap admin account is a member of this workspace out of the box. Additional workspaces appear in the picker at `/submit`.

## Subcommands

### Working with jobs

| Command | Purpose |
|---|---|
| **[pal remote submit](pal-remote-submit.md)** | Upload a file and queue an analysis job. |
| **[pal remote status](pal-remote-status.md)** | Poll a job until it completes. |
| **[pal remote results](pal-remote-results.md)** | Show findings from a completed job. |
| **[pal remote report](pal-remote-report.md)** | Download the HTML, JSON, or Markdown report. |
| **[pal remote dataset](pal-remote-dataset.md)** | Download the normalized dataset artifact. |
| **[pal remote diagnostics](pal-remote-diagnostics.md)** | Show guided diagnostics insights. |

### Analytics across multiple jobs

| Command | Purpose |
|---|---|
| **[pal remote compare](pal-remote-compare.md)** | Diff two completed jobs. |
| **[pal remote trends](pal-remote-trends.md)** | Finding trends across the last N jobs. |
| **[pal remote correlations](pal-remote-correlations.md)** | Co-occurring finding pairs across the last N jobs. |

### Packs

| Command | Purpose |
|---|---|
| **[pal remote packs](pal-remote-packs.md)** | List packs registered on the server. |
| **[pal remote validate-pack](pal-remote-validate-pack.md)** | Validate a stored pack version on the server. |

### Baselines

| Command | Purpose |
|---|---|
| **[pal remote baselines](pal-remote-baselines.md)** | List and set baseline designations. |

### Alerts (Phase 4)

| Command | Purpose |
|---|---|
| **[pal remote alerts](pal-remote-alerts.md)** | List, acknowledge, resolve, snooze, and unsnooze alerts. |

### Schedules (Phase 4)

| Command | Purpose |
|---|---|
| **[pal remote schedules](pal-remote-schedules.md)** | List, create, enable, disable, and delete ingestion schedules. |

## Exit codes

Same scheme as the local commands:

| Code | Meaning |
|---|---|
| `0` | Request succeeded. |
| `1` | Request succeeded but result is below a threshold (rarely used in remote commands). |
| `2` | Invalid arguments (malformed GUID, mutually-exclusive flags). |
| `5` | HTTP request failed or server returned an unexpected error. |

A `404` from the API typically means your `--api` base URL is missing the workspace prefix. Double-check that.

## Related

- **[First analysis — remote API](../../getting-started/first-analysis-remote.md)** — end-to-end walkthrough.
- **[CLI reference overview](index.md)** — the local commands.
