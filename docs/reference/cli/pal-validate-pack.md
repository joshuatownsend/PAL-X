---
title: pal validate-pack
description: Validate one pack or a directory of packs against the pal.pack/v1 schema, optionally enforcing signatures.
---

# `pal validate-pack`

Validate one pack — or a directory of packs — against the `pal.pack/v1` and `pal.pack/v1.1` JSON Schema, with optional RSA-PSS-SHA256 signature enforcement.

Use this in CI before publishing a pack, or against a vendor pack before trusting it on production.

## Synopsis

```text
pal validate-pack [OPTIONS]
```

## Options

| Option | Purpose |
|---|---|
| `--path <PATH>` | Path to a pack directory (contains `pack.yaml`) or a specific `pack.yaml` file. Required. |
| `--strict` | Treat warnings as errors. Useful in CI. |
| `--require-signature` | Fail if `pack.yaml.sig` is missing or signature verification fails. |
| `--trust-key <PATH>` | Path to an additional trusted RSA public-key PEM file. Repeatable. |
| `--json-output <PATH>` | Write the validation result as JSON to this path (suitable for piping). |

## Examples

Quick validate one of the shipped packs:

```bash
pal validate-pack --path packs/thresholds/windows-core
```

Validate with signature enforcement against a known-good key:

```bash
pal validate-pack \
  --path packs/vendor/some-third-party-pack \
  --require-signature \
  --trust-key tools/keys/vendor.pub.pem
```

Validate as a CI gate, failing on warnings, capturing JSON:

```bash
pal validate-pack \
  --path packs/thresholds/windows-core \
  --strict \
  --json-output out/validation.json
```

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Pack is valid. |
| `2` | Invalid arguments. |
| `3` | `--path` does not exist. |
| `4` | Pack validation failed (schema, signature, or `--strict` warning). |

## What's checked

- The pack YAML conforms to `pal.pack.v1.json` (or `pal.pack.v1.1.json`, selected by `schema_version`).
- Every rule has a non-empty `rule_id`, valid `severity`, valid `category`.
- Every condition has a valid `metric`, supported `aggregation`, supported `operator`, a numeric `threshold` (or a `host_context`-derived one), and a `duration_percent` in `[0, 100]`.
- Window blocks (v1.1 only) reject `trend` aggregation and require positive `size` / `slide`.
- Signature (when `--require-signature` is set) verifies against the union of the project's built-in trust list and any keys passed with `--trust-key`.

## Related

- **[pal packs sign](pal-packs.md#pal-packs-sign)** — produce the `pack.yaml.sig` sidecar this command verifies.
- **[ADR 0003 — Pack Signing Format](../../architecture/adr/0003-pack-signing-format.md)** — the trust model.
- **[ADR 0002 — Declarative Rule Schema](../../architecture/adr/0002-declarative-rule-schema.md)** — what the schema enforces.
- **[ADR 0004 — Schema v1.1 Rolling Windows](../../architecture/adr/0004-schema-v1.1-rolling-windows.md)** — v1.1 additions.
