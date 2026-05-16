---
title: pal remote alerts
description: List, acknowledge, resolve, snooze, and unsnooze Phase 4 alerts.
---

# `pal remote alerts`

Manage Phase 4 alerts. Five subcommands:

| Subcommand | Purpose |
|---|---|
| [`list`](#pal-remote-alerts-list) | List alerts in the workspace. |
| [`acknowledge`](#pal-remote-alerts-acknowledge) | Mark an alert as acknowledged. |
| [`resolve`](#pal-remote-alerts-resolve) | Mark an alert resolved with an optional note. |
| [`snooze`](#pal-remote-alerts-snooze) | Suppress notifications for an alert until a specified time. |
| [`unsnooze`](#pal-remote-alerts-unsnooze) | Clear an active snooze. |

See the **[alerts glossary entry](../../getting-started/glossary.md#alert)** *(coming)* for the lifecycle model.

---

## `pal remote alerts list`

### Synopsis

```text
pal remote alerts list [OPTIONS]
```

### Options

| Option | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Workspace-prefixed base URL. |
| `--api-key <pal_…>` | *(unset)* | Personal access token. |
| `-s`, `--status <STATUS>` | none | Filter: `open`, `acknowledged`, `resolved`. |
| `--severity <SEV>` | none | Filter: `critical`, `warning`, `informational`. |

### Example

```bash
pal remote alerts list \
  --api $PAL_API --api-key $PAL_TOKEN \
  --status open --severity critical
```

---

## `pal remote alerts acknowledge`

Transition an alert from `open` to `acknowledged`. Doesn't suppress notifications — for that, use `snooze`.

### Synopsis

```text
pal remote alerts acknowledge <id> [OPTIONS]
```

### Arguments

| Argument | Purpose |
|---|---|
| `<id>` | Alert ID (GUID). |

### Options

The standard `--api` and `--api-key`.

### Example

```bash
pal remote alerts acknowledge \
  --api $PAL_API --api-key $PAL_TOKEN \
  1a2b3c4d-...
```

---

## `pal remote alerts resolve`

Mark an alert resolved. The note (when present) is stored on the alert for audit history.

### Synopsis

```text
pal remote alerts resolve <id> [OPTIONS]
```

### Arguments

| Argument | Purpose |
|---|---|
| `<id>` | Alert ID (GUID). |

### Options

| Option | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Workspace-prefixed base URL. |
| `--api-key <pal_…>` | *(unset)* | Personal access token. |
| `-n`, `--note <TEXT>` | none | Free-text resolution note. |

### Example

```bash
pal remote alerts resolve \
  --api $PAL_API --api-key $PAL_TOKEN \
  --note "Replaced failing disk on WEB-01" \
  1a2b3c4d-...
```

---

## `pal remote alerts snooze`

Suppress notifications for an alert until a specified time. `--duration` and `--until` are mutually exclusive — pass exactly one.

### Synopsis

```text
pal remote alerts snooze <id> [OPTIONS]
```

### Arguments

| Argument | Purpose |
|---|---|
| `<id>` | Alert ID (GUID). |

### Options

| Option | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Workspace-prefixed base URL. |
| `--api-key <pal_…>` | *(unset)* | Personal access token. |
| `-d`, `--duration <DURATION>` | none | Relative duration: `30m`, `2h`, `1d`. Mutually exclusive with `--until`. |
| `--until <ISO-8601>` | none | Absolute timestamp. Mutually exclusive with `--duration`. |

### Examples

Snooze for two hours:

```bash
pal remote alerts snooze \
  --api $PAL_API --api-key $PAL_TOKEN \
  --duration 2h 1a2b3c4d-...
```

Snooze until end of next business day:

```bash
pal remote alerts snooze \
  --api $PAL_API --api-key $PAL_TOKEN \
  --until 2026-05-18T17:00:00-07:00 \
  1a2b3c4d-...
```

---

## `pal remote alerts unsnooze`

Clear an active snooze immediately.

### Synopsis

```text
pal remote alerts unsnooze <id> [OPTIONS]
```

### Arguments

| Argument | Purpose |
|---|---|
| `<id>` | Alert ID (GUID). |

### Options

The standard `--api` and `--api-key`.

### Example

```bash
pal remote alerts unsnooze \
  --api $PAL_API --api-key $PAL_TOKEN \
  1a2b3c4d-...
```

## Exit codes (all alert subcommands)

| Code | Meaning |
|---|---|
| `0` | Operation succeeded. |
| `2` | Malformed alert ID, bad `--duration`/`--until`, or mutually-exclusive flags both set. |
| `1` | Alert not found or server error. |

## Related

- **[pal remote schedules](pal-remote-schedules.md)** — pair with alerts for unattended ingestion + monitoring loops.
- The `/alerts` Blazor UI page is the GUI equivalent of these commands.
