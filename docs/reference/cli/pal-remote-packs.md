---
title: pal remote packs
description: List packs registered on the server.
---

# `pal remote packs`

List every pack registered on the running PAL API server. The server maintains its own pack registry (synced from `Packs:Directory` at startup); this command shows that registry.

## Synopsis

```text
pal remote packs [OPTIONS]
```

## Options

| Option | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Workspace-prefixed base URL. |
| `--api-key <pal_…>` | *(unset)* | Personal access token. |

## Example

```bash
pal remote packs --api $PAL_API --api-key $PAL_TOKEN
```

## What you get

A table of pack IDs and the versions registered for each. Use **[pal remote validate-pack](pal-remote-validate-pack.md)** to validate a specific stored version.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | List retrieved. |
| `1` | API unreachable or server error. |

## Related

- **[pal remote validate-pack](pal-remote-validate-pack.md)** — validate one stored pack version.
- **[pal list-packs](pal-list-packs.md)** — local-disk equivalent (shows what's on the analyzer's search path).
