# ADR 0003 — Pack Signing Format and Trust Model

**Status:** Accepted  
**Date:** 2026-04-27  
**Deciders:** Josh Townsend (project lead)

## Context

PAL packs (YAML rule files) can be shared across teams or published to a pack registry.
Without signature verification, a pack consumer has no assurance that the pack content
was authored by a trusted party. This ADR documents the signing scheme introduced in Phase 1.5.

## Decisions

### Algorithm: RSA-PSS-SHA256 with 3072-bit keys

**Rationale:** BCL-only (no NuGet dep) via `System.Security.Cryptography.RSA`. Ed25519 is smaller
(64-byte signatures vs ~384 bytes) but requires a third-party package until .NET 9. RSA-PSS-SHA256
is a FIPS-approved scheme with well-understood revocation patterns. 3072-bit keys provide ~128-bit
security margin — appropriate for code signing over a 5-10 year horizon.

**Salt:** PSS salt length defaults to `HashSize` (32 bytes for SHA-256) for consistency.

### Sign over raw file bytes, not parsed YAML

Signing `File.ReadAllBytes("pack.yaml")` requires no YAML canonicalization step and is trivially
auditable — `openssl dgst -verify pubkey.pem -signature sig.bin pack.yaml`. Embedding a signature
field inside the YAML would require knowing the content before computing it (a contradiction),
and YAML canonicalization is non-trivial.

### Sidecar placement: `pack.yaml.sig`

The sidecar lives adjacent to `pack.yaml` in the pack directory:

```
pack.yaml
pack.yaml.sig
```

Sidecar format:
```
# pal-pack-signature/v1 alg=rsa-pss-sha256 keyid=ab12cd34
<base64 single-line RSA-PSS-SHA256 signature over pack.yaml bytes>
```

The `keyid` field holds the first 8 hex chars of SHA-256 of the SPKI-encoded public key bytes.
It is informational only — all trusted keys are tried on verification failure; the keyid aids
human debugging.

### Trust model (v1.5): embedded official public key + CLI `--trust-key`

- `TrustedKeys.OfficialPublicKeyPem` is an embedded constant in `Pal.Packs.Signing`. In v1.5
  dev builds this is empty (no trusted official key); production deployment replaces it.
- `--trust-key <pem>` on `validate-pack` and `pal packs sign` allows users to pin additional
  trusted public keys at call time.
- No trust store, no key rotation mechanism — deferred to v2 when a pack registry exists.

### `SignatureRequirement` enum on `PackLoader`

Pack consumption defaults to `Optional` (existing behavior). CLI and API callers that enforce
integrity pass `Required`, which causes `PackLoader.Load()` to verify the sidecar before
deserializing YAML. `Forbidden` is available for sealed environments that reject any signed packs.

## Consequences

- Unsigned packs continue to work by default (no breaking change).
- `pal packs sign --pack <dir> --key <privkey.pem>` writes the sidecar.
- `pal validate-pack --require-signature --trust-key <pubkey.pem>` enforces verification.
- Pack tampering (any modification to `pack.yaml`) causes verification to fail.
- The `pack.yaml.sig` file should be committed to version control alongside `pack.yaml`.

## Not addressed here

- Key rotation / revocation
- Multi-signature / threshold signing
- Pack registry publish/pull signing workflows (Phase 2+ scope)
