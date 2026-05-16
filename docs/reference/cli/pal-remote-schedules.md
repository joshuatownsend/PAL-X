---
title: pal remote schedules
description: List, create, enable, disable, and delete Phase 4 ingestion schedules.
---

# `pal remote schedules`

Manage Phase 4 directory-poll ingestion schedules. Five subcommands:

| Subcommand | Purpose |
|---|---|
| [`list`](#pal-remote-schedules-list) | List schedules in the workspace. |
| [`create`](#pal-remote-schedules-create) | Create a new directory-poll schedule. |
| [`enable`](#pal-remote-schedules-enable) | Enable a schedule. |
| [`disable`](#pal-remote-schedules-disable) | Disable a schedule (worker stops polling it). |
| [`delete`](#pal-remote-schedules-delete) | Delete a schedule permanently. |

A schedule tells the `ScheduledIngestionWorker` to scan a directory on an interval, queue an analysis job for every new file that matches a glob, and route the result through the standard analysis pipeline.

---

## `pal remote schedules list`

### Synopsis

```text
pal remote schedules list [OPTIONS]
```

### Options

The standard `--api` and `--api-key`.

### Example

```bash
pal remote schedules list --api $PAL_API --api-key $PAL_TOKEN
```

---

## `pal remote schedules create`

### Synopsis

```text
pal remote schedules create [OPTIONS]
```

### Options

| Option | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Workspace-prefixed base URL. |
| `--api-key <pal_…>` | *(unset)* | Personal access token. |
| `-n`, `--name <NAME>` | required | Human-readable schedule name. Unique within the workspace. |
| `-i`, `--interval <MIN>` | required | Polling interval in minutes. Range `5`–`1440`. |
| `--path <PATH>` | required | Absolute directory path the server will scan for new files. |
| `--glob <PATTERN>` | `*.csv` | File glob pattern. Common: `*.csv` or `*.blg`. |
| `-p`, `--pack <PACK-ID>` | required | Pack ID(s) to run on each ingested file. Repeatable. |
| `--disabled` | off | Create the schedule in the disabled state — useful for staging. |

### Example

Poll a perfmon export directory every 15 minutes:

```bash
pal remote schedules create \
  --api $PAL_API --api-key $PAL_TOKEN \
  --name "WEB-01 hourly" \
  --interval 15 \
  --path /mnt/perfmon-exports/web-01 \
  --glob '*.csv' \
  --pack windows-core --pack iis-core
```

### Notes

- The `--path` is interpreted **on the server's filesystem**, not your local one. For Docker deployments, that's the path inside the container — mount the host directory into the container first.
- An idempotency marker keeps the worker from re-analyzing the same file twice across restarts.

---

## `pal remote schedules enable`

### Synopsis

```text
pal remote schedules enable <id> [OPTIONS]
```

### Arguments

| Argument | Purpose |
|---|---|
| `<id>` | Schedule ID (GUID). |

### Options

The standard `--api` and `--api-key`.

### Example

```bash
pal remote schedules enable \
  --api $PAL_API --api-key $PAL_TOKEN \
  1a2b3c4d-...
```

---

## `pal remote schedules disable`

### Synopsis

```text
pal remote schedules disable <id> [OPTIONS]
```

### Arguments

| Argument | Purpose |
|---|---|
| `<id>` | Schedule ID (GUID). |

### Options

The standard `--api` and `--api-key`.

### Example

```bash
pal remote schedules disable \
  --api $PAL_API --api-key $PAL_TOKEN \
  1a2b3c4d-...
```

Disabling is reversible. Use `delete` for permanent removal.

---

## `pal remote schedules delete`

### Synopsis

```text
pal remote schedules delete <id> [OPTIONS]
```

### Arguments

| Argument | Purpose |
|---|---|
| `<id>` | Schedule ID (GUID). |

### Options

The standard `--api` and `--api-key`.

### Example

```bash
pal remote schedules delete \
  --api $PAL_API --api-key $PAL_TOKEN \
  1a2b3c4d-...
```

## Exit codes (all schedule subcommands)

| Code | Meaning |
|---|---|
| `0` | Operation succeeded. |
| `2` | Missing required flag, malformed schedule ID, or invalid `--interval`. |
| `1` | Schedule not found, name conflict, or server error. |

## Related

- **[pal remote alerts](pal-remote-alerts.md)** — what fires when a scheduled job produces a critical finding.
- The `/schedules` Blazor UI page is the GUI equivalent of these commands.
