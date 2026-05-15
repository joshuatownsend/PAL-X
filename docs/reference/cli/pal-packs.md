---
title: pal packs
description: Pack-management commands. Currently houses `pal packs sign`.
---

# `pal packs`

Umbrella command for pack-management operations. Today the only subcommand is `sign`.

## Synopsis

```text
pal packs <COMMAND>
```

## Subcommands

| Command | Purpose |
|---|---|
| **[pal packs sign](#pal-packs-sign)** | Sign a pack directory, producing `pack.yaml.sig`. |

---

## `pal packs sign`

Compute an RSA-PSS-SHA256 signature over `pack.yaml` and write it as a sidecar `pack.yaml.sig` adjacent to the source.

The resulting signature is what `pal validate-pack --require-signature` verifies. See **[ADR 0003](../../architecture/adr/0003-pack-signing-format.md)** for the format details.

### Synopsis

```text
pal packs sign [OPTIONS]
```

### Options

| Option | Purpose |
|---|---|
| `--pack <PATH>` | Path to the pack directory containing `pack.yaml`. Required. |
| `--key <PATH>` | Path to an RSA private-key PEM file (PKCS#8 or traditional). Required. |

### Examples

Sign one of the shipped packs with a development key:

```bash
pal packs sign \
  --pack packs/thresholds/windows-core \
  --key tools/test-keys/dev.priv.pem
```

Then verify against the matching public key:

```bash
pal validate-pack \
  --path packs/thresholds/windows-core \
  --require-signature \
  --trust-key tools/test-keys/dev.pub.pem
```

### Exit codes

| Code | Meaning |
|---|---|
| `0` | Signature written successfully. |
| `2` | Invalid arguments. |
| `3` | `--pack` directory or `--key` file does not exist. |
| `5` | Cryptographic error (e.g. key is malformed). |

### Notes

- The signature covers the byte-exact `pack.yaml` content. Re-sign whenever you modify the pack.
- Private keys should never be checked into a public repo. The repo's `tools/test-keys/dev.priv.pem` is intentionally a fixture for tests; do not use it for production.

### Related

- **[pal validate-pack](pal-validate-pack.md)** — verify the signature.
- **[ADR 0003 — Pack Signing Format](../../architecture/adr/0003-pack-signing-format.md)** — the format and trust model.
- **Sign and trust packs** *(coming)* — how to set up a real signing workflow.
