---
title: pal list-packs
description: List all packs available on the search path.
---

# `pal list-packs`

Enumerate every pack the analyzer would find on the current search path.

Use this to confirm what `pal analyze --auto-resolve-packs` will load, or to inventory third-party packs you've dropped into the search path.

## Synopsis

```text
pal list-packs [OPTIONS]
```

## Options

| Option | Purpose |
|---|---|
| `--pack-dir <PATH>` | Additional search path. Repeatable. Extends the built-in `packs/thresholds/` lookup. |
| `--json-output <PATH>` | Write the listing as JSON to this path. If omitted, prints a human-readable table. |

## Examples

Human-readable list of every built-in pack:

```bash
pal list-packs --pack-dir packs/thresholds
```

Capture as JSON for tooling:

```bash
pal list-packs \
  --pack-dir packs/thresholds \
  --pack-dir packs/vendor \
  --json-output out/packs.json
```

## What you get

For each discovered pack:

- `pack_id`, `pack_name`, `version`, `description`.
- `schema_version` (`pal.pack/v1` or `pal.pack/v1.1`).
- `applicability` mode and counters.
- Rule count, signed/unsigned status.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Listing produced. |
| `2` | Invalid arguments. |

## Related

- **[pal analyze](pal-analyze.md)** — `--auto-resolve-packs` uses the same search path.
- **[pal validate-pack](pal-validate-pack.md)** — validate one of the discovered packs.
