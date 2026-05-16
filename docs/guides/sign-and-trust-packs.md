---
title: Sign and trust packs
description: Use RSA-PSS-SHA256 to sign a pack and enforce signature verification on consumers — for cross-team or vendor pack distribution.
---

# Sign and trust packs

Goal: sign a pack with your team's private key so consumers can verify it was published by you, and configure their CLI / API to reject unsigned or wrong-key packs in production.

For the trust model and format, see **[ADR 0003 — Pack Signing Format](../architecture/adr/0003-pack-signing-format.md)**. For the command flags, see **[CLI — `pal packs sign`](../reference/cli/pal-packs.md#pal-packs-sign)** and **[`pal validate-pack`](../reference/cli/pal-validate-pack.md)**.

## When to sign

Sign packs when they cross trust boundaries:

- **Cross-team distribution** — your platform team publishes a pack, application teams consume it.
- **Vendor packs** — a third party ships rules for their workload.
- **Production pinning** — you only want CI / production to load packs whose signature matches a pinned key.

Inside one team, an internal repo with code review is usually sufficient and signing adds friction without value.

## 1. Generate a signing key

The signing scheme is RSA-PSS-SHA256 with 3072-bit keys. Generate the keypair once and keep the private key secret:

```bash
# Generate the private key (3072-bit RSA)
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out team.priv.pem

# Derive the public key
openssl rsa -in team.priv.pem -pubout -out team.pub.pem
```

Distribute `team.pub.pem` to consumers; `team.priv.pem` stays with the publisher.

## 2. Sign a pack

```bash
pal packs sign \
  --pack packs/published/my-cpu-pack \
  --key team.priv.pem
```

This produces `packs/published/my-cpu-pack/pack.yaml.sig` adjacent to `pack.yaml`. The sidecar format:

```text
# pal-pack-signature/v1 alg=rsa-pss-sha256 keyid=ab12cd34
<base64 signature on one line>
```

The `keyid` is the first 8 hex chars of SHA-256 over the SPKI public key bytes — informational only.

You can verify with vanilla OpenSSL too:

```bash
# Extract the base64 signature from the sidecar (skip the comment line)
tail -1 pack.yaml.sig | base64 -d > sig.bin

openssl dgst -verify team.pub.pem \
  -signature sig.bin \
  -sigopt rsa_padding_mode:pss \
  -sigopt digest:sha256 \
  -sigopt rsa_pss_saltlen:32 \
  pack.yaml
```

The signature is over **raw `pack.yaml` bytes** — no YAML canonicalisation. That keeps the implementation BCL-only and the verification trivially auditable.

## 3. Enforce signatures on the CLI

Without `--require-signature`, a missing or invalid signature is silently ignored. To enforce:

```bash
pal validate-pack \
  --path packs/published/my-cpu-pack \
  --require-signature \
  --trust-key team.pub.pem
```

The CLI's trust list is the union of the project's built-in key and every `--trust-key` you pass. `--trust-key` is repeatable.

Likewise on analysis:

```bash
pal analyze \
  --input cpu.csv \
  --output out \
  --pack-dir packs/published \
  --require-signature \
  --trust-key team.pub.pem
```

The same gate is applied: every pack loaded from `--pack-dir` must have a valid signature against the trust list.

## 4. Use signatures in CI

For a CI pipeline that consumes published packs:

```yaml
- name: Verify pack signatures
  run: |
    pal validate-pack \
      --path packs/published/ \
      --strict \
      --require-signature \
      --trust-key tools/keys/team.pub.pem
```

If any pack lacks a signature, has an invalid signature, or was signed by a key not in the trust list, the validation step fails with exit code `4` (pack validation failure).

## 5. Rotate a key

When a key needs rotating (compromise, scheduled rotation, team handover):

1. Generate a new keypair (`new.priv.pem` / `new.pub.pem`).
2. **Distribute the new public key first.** Add it to all consumers' `--trust-key` lists alongside the old key — they trust both during the rollover window.
3. Re-sign every published pack with the new private key.
4. After consumers have picked up the re-signed packs (a release cycle or two), remove the old public key from their trust lists.

There is no in-band revocation — trust is rooted in the consumer's trust list, so revocation is "stop trusting this key." Detection of compromised packs is out of scope; the signature only proves authorship.

## What's NOT signed

- **Pack-level `metric_aliases`** added at *load* time via pack-level `metric_aliases:` — these are part of `pack.yaml` and therefore covered by the signature.
- **Compiled metric alias additions** via `MetricAliasRegistry.Add(...)` — these are engine-internal and not signed (they're code, not data).
- **The `pack.yaml.sig` sidecar itself** — it's the signature; verifying it against itself is circular.

## Common failures

| Message | Cause | Fix |
|---|---|---|
| `pack.yaml.sig is missing` | No sidecar in the pack directory | Sign with `pal packs sign`, or drop `--require-signature` if you're authoring locally. |
| `Signature verification failed: no trusted key matches` | Key not in trust list | Add the publisher's `--trust-key`. |
| `Signature verification failed: signature invalid` | YAML was modified after signing, or signature was hand-edited | Re-sign after the YAML change. |

## Related

- **[ADR 0003 — Pack Signing Format](../architecture/adr/0003-pack-signing-format.md)** — algorithm choice, sidecar format, trust model.
- **[CLI — `pal packs sign`](../reference/cli/pal-packs.md#pal-packs-sign)** — flags.
- **[CLI — `pal validate-pack`](../reference/cli/pal-validate-pack.md)** — `--require-signature` / `--trust-key`.
- **[Write a pack](write-a-pack.md)** / **[Validate a pack](validate-a-pack.md)** — the workflow signing fits into.
