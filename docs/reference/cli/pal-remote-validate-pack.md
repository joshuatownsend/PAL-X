---
title: pal remote validate-pack
description: Validate a stored pack version on the server.
---

# `pal remote validate-pack`

Run the server's `PackValidator` against a stored pack version and return the structured result (errors + warnings). Useful for verifying that a vendor pack registered with the server still passes validation after a server upgrade.

## Synopsis

```text
pal remote validate-pack <pack-id> <version> [OPTIONS]
```

## Arguments

| Argument | Purpose |
|---|---|
| `<pack-id>` | Pack ID registered on the server (e.g. `windows-core`). |
| `<version>` | Pack version to validate (e.g. `1.0.0`). |

## Options

| Option | Default | Purpose |
|---|---|---|
| `--api <URL>` | `http://localhost:8080` | Workspace-prefixed base URL. |
| `--api-key <pal_…>` | *(unset)* | Personal access token. |

## Example

```bash
pal remote validate-pack \
  --api $PAL_API --api-key $PAL_TOKEN \
  windows-core 1.0.0
```

## What you get

A JSON-shaped response with `isValid`, `errors[]`, `warnings[]`. The same shape the server uses for `GET /packs/{id}/versions/{version}/validation`.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Pack validated successfully (no errors). |
| `2` | Bad arguments. |
| `4` | Validation produced errors. |
| `1` | Server error or pack/version not found. |

## Related

- **[pal validate-pack](pal-validate-pack.md)** — local-disk equivalent (validates a pack on your filesystem).
- **[pal remote packs](pal-remote-packs.md)** — list pack IDs and versions available on the server.
