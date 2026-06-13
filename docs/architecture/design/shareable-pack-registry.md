# Design: Shareable Pack Registry / Distribution

> **Status**: Draft — open for maintainer review  
> **Created**: 2026-06-13  
> **Plan**: 007-shareable-pack-registry-design  
> **Decision required on**: distribution model, governance structure, trust provisioning

---

## 1. Current State

### The signing → validation → versioning → sync → HTTP chain

PAL-X already implements a complete *local* pack pipeline. Each stage is described below with precise file citations.

**1a. Signing — `dotnet/src/Pal.Packs/Signing/`**

`PackSigner.Sign(packDirectory, privateKeyPemPath)` reads `pack.yaml`, signs the bytes using RSA-PSS-SHA256, and writes a two-line sidecar file `pack.yaml.sig` adjacent to `pack.yaml` (`PackSigner.cs:32–33`). The sidecar format is:

```
# pal-pack-signature/v1 alg=rsa-pss-sha256 keyid=<8-hex-chars>
<base64-signature>
```

The `keyid` is the first 8 hex characters of the SHA-256 hash of the SPKI-encoded public key (`PackSigner.cs:41–44`). Signing is exposed via `pal packs sign --pack <dir> --key <privkey.pem>` (`Packs/SignPackCommand.cs`).

**1b. Trust model — caller-supplied trusted keys**

`PackVerifier.Verify(packDirectory, IReadOnlyList<RSA> trustedKeys)` (`PackVerifier.cs:9`) is entirely caller-driven: there is no built-in trust store. The verifier tries each supplied key in sequence; if none produces a valid RSA-PSS-SHA256 verification over the raw `pack.yaml` bytes, it returns `VerificationResult(false, keyId, "InvalidSignature")`. The four possible failure reasons are `MissingSignature`, `MalformedSignature`, `NoTrustedKeys`, and `InvalidSignature` (`PackVerifier.cs:15,27,43,52`).

`TrustedKeys.cs` holds a placeholder for an eventual official public key (`OfficialPublicKeyPem = ""` on line 9) and provides `DefaultTrusted(extraKeyPemPath?)` which merges the official key (if present) with one optional caller-supplied PEM (`TrustedKeys.cs:37–49`). Currently empty — no official key is embedded.

`PackLoader.EnforceSignature` (`PackLoader.cs:44–63`) is the integration point: when `SignatureRequirement.Required` is passed, it calls `PackVerifier.Verify` and throws `PackSignatureException` on any failure. The default throughout the codebase is `SignatureRequirement.Optional` — the validation endpoint (`PackEndpoints.cs:43`) and the startup sync (`PackRegistrySyncService.cs:36`) both load packs without enforcing signatures.

`SignatureRequirement` has three values: `Optional`, `Required`, `Forbidden` (`SignatureRequirement.cs`).

**1c. Validation — `PackValidator`**

`PackValidator.Validate(pack)` (`PackValidator.cs:24`) performs structural checks: `pack_id` must be kebab-case, `version` must be semver, rules must have valid `severity`/`category`/`aggregation`/`operator` values from closed enum sets, schema version must be `pal.pack/v1` or `pal.pack/v1.1`. Window conditions gate on `v1.1`. Returns `ValidationResult { IsValid, Errors, Warnings }`.

**1d. Registry persistence — `PackEntity` + `PackVersionEntity`**

`PackEntity` (`Pal.Persistence/Entities/PackEntity.cs`) stores `Id`, `CurrentVersion`, `Title`, `Status`, timestamps, and a collection of `PackVersionEntity`. `PackVersionEntity` (`PackVersionEntity.cs`) stores `PackId`, `Version`, `StoragePath` (a server-local filesystem path), and `CreatedAt`. `StoragePath` is intentionally not exposed via the HTTP API (`PackEndpoints.cs:23`, comment "must not be exposed").

**1e. Startup sync — `PackRegistrySyncService`**

`PackRegistrySyncService.SyncAsync(packsDirectory)` (`PackRegistrySyncService.cs:17`) walks the directory tree looking for `pack.yaml` files, loads each without signature enforcement, and upserts `(packId, version, packName, yamlPath)` into the database. Called once at startup (`Program.cs:166–168`) with the path from `Packs:Directory` config (default `packs/thresholds`). This is a purely local, one-way, read-from-disk operation.

**1f. HTTP surface — `PackEndpoints`**

Three read-only endpoints, all globally-scoped (not workspace-scoped, `Program.cs:184`):

| Endpoint | Handler |
|---|---|
| `GET /packs` | Lists all `PackEntity` records — id, currentVersion, title, status |
| `GET /packs/{id}/versions` | Lists versions for a pack — packId, version, createdAt |
| `GET /packs/{id}/versions/{version}/validation` | Runs `PackValidator` against stored YAML; returns `{ isValid, errors, warnings }` |

No publish, pull, search, or download endpoint exists.

**1g. CLI pack surface**

| Command | Location |
|---|---|
| `pal packs sign --pack <dir> --key <privkey.pem>` | `Packs/SignPackCommand.cs` |
| `pal validate-pack --path <dir> [--require-signature] [--trust-key <pub.pem>]` | `ValidatePackCommand.cs` |
| `pal remote packs` | `Remote/RemotePacksCommand.cs` — lists server-side packs |
| `pal remote validate-pack <id> <version>` | `Remote/RemoteValidatePackCommand.cs` — calls the validation endpoint |

No `pull`, `search`, or `publish` command exists.

**Summary of the trust model as it stands today**

The model is *caller-supplied, explicit*. Whoever calls `PackVerifier.Verify` provides the trust list. `TrustedKeys.DefaultTrusted` gives a convenience constructor for the common "official key + one extra key" case, but the official key slot is currently empty. Signature enforcement is `Optional` everywhere in production code paths, meaning packs are loaded and synced without any cryptographic verification.

---

## 2. The Gap — What "Shareable" Adds

The current system assumes packs arrive on disk before the API starts. Getting packs from one instance to another requires manually copying directory trees. The gap between this and a *shareable* registry has four distinct sub-problems:

| Sub-problem | Today | Missing |
|---|---|---|
| **Discovery** | None — you must know what packs exist | A searchable index that exposes packs without requiring prior knowledge |
| **Remote fetch / pull** | None — packs are disk-only | A mechanism to download a pack (YAML + sidecar) from a remote source into local storage |
| **Publish** | None from the API or CLI | A way to upload a signed pack so others can discover and pull it |
| **Cross-org / community sharing** | None — each org runs its own instance | A way for packs authored by one org to be usable by unaffiliated orgs |

These sub-problems are largely independent: discovery can be solved without enabling publish (read-only pull from a curated source). The design below structures the option space around who hosts the index and who controls trust.

---

## 3. Distribution Models

### (a) Centralized Registry

**Model**: A single, authoritative hosted index (e.g., a GitHub repository or a dedicated service) holds a manifest of all published packs. Each pack is stored as a GitHub Release asset or similar immutable artifact. Consumers discover packs by querying the index and download them by URL. All published packs are signed by a single trusted maintainer key (or a small key committee).

**Mechanics**:
- The registry index is a JSON or YAML manifest: `{ packs: [{ id, version, description, download_url, sha256_checksum, signature_url }] }`.
- Download fetches the YAML and `.sig` sidecar; verification uses a single embedded official public key (the `TrustedKeys.OfficialPublicKeyPem` slot, currently empty).
- Publishing requires a PR or release workflow controlled by the registry owner.

**Trade-offs**:

| Dimension | Assessment |
|---|---|
| Discovery | Simplest — one URL to query |
| Trust | Single root — trivial to verify, catastrophic if the signing key is compromised |
| Governance | Highest burden on the maintainer — all packs must be reviewed/accepted |
| Supply-chain | Tampering is hard if consumers verify signatures; typosquatting is manageable because the index is curated |
| Availability | Single point of failure if the host goes down |
| PAL-X fit | Slots directly into `TrustedKeys.OfficialPublicKeyPem`; minimal new code |

**Supply-chain implications**: A compromised registry key poisons every consumer instantly. Key rotation requires coordinated distribution of the new public key. Consumers with pinned keys are not automatically updated.

### (b) Federated / Decentralized

**Model**: No central authority. Pack authors host their own packs anywhere (GitHub, S3, personal servers). Consumers configure a list of trusted public keys and a list of "source URLs" to search. Discovery is by direct URL or a gossip/peer-discovery mechanism.

**Mechanics**:
- Each consumer configures `Packs:RemoteSources` in `appsettings.json` as a list of `{ baseUrl, trustedKeyPemPath }` pairs.
- `PackLoader.EnforceSignature` already accepts an `IReadOnlyList<RSA>` — each source's configured key would be passed when pulling from that source.
- No index exists; consumers must know the source URL.

**Trade-offs**:

| Dimension | Assessment |
|---|---|
| Discovery | Hardest — no index; requires out-of-band knowledge of sources |
| Trust | Distributed — each source has its own key; compromise of one key affects only that source's packs |
| Governance | None by default — any key holder can publish anything |
| Supply-chain | Higher typosquatting/confusion risk without a curated namespace |
| Availability | No single point of failure |
| PAL-X fit | Best mechanical fit — `TrustedKeys` and `PackVerifier` already handle multi-key lists |

**Supply-chain implications**: Consumers must vet each source independently. TOFU (Trust On First Use) would be dangerous here — an attacker who can intercept the first pull establishes trust without the consumer noticing.

### (c) Hybrid — Community Index Pointing at Independently-Hosted Signed Packs

**Model**: A community-maintained index (e.g., a GitHub repo, similar to the Homebrew tap or Helm chart hub model) contains *metadata* (id, version, description, download URL, expected SHA-256 checksum, author public key fingerprint) but does NOT host the artifacts themselves. Pack authors host their own artifacts (e.g., GitHub Releases). The index is auditable by pull request; consumers verify signatures locally with author-supplied public keys.

**Mechanics**:
- The index is a `registry.json` or `registry.yaml` checked into a Git repository.
- Each entry includes `{ id, version, author, download_url, pack_yaml_sha256, author_key_fingerprint, sig_url }`.
- A consumer pulls the index, locates the desired pack entry, fetches the artifact from the download URL, verifies the SHA-256 checksum against the index entry (integrity), then verifies the RSA-PSS-SHA256 signature using the author's trusted public key (authenticity).
- Authors submit new pack entries via pull request; the index maintainer reviews the PR, not the pack content itself.

**Trade-offs**:

| Dimension | Assessment |
|---|---|
| Discovery | Good — one index to query; searchable by pack_id/description |
| Trust | Distributed per-author; index only records fingerprints, not the artifacts |
| Governance | Low burden on index maintainer — no content review, only namespace management and duplicate prevention |
| Supply-chain | Two-factor: SHA-256 checksum (integrity) + RSA-PSS signature (authenticity); tampering at the download host does not bypass signature check |
| Availability | Index down → no discovery, but already-known URLs still work |
| PAL-X fit | Good — `PackVerifier` handles the signature check; new code needed only for HTTP fetch + checksum + index parsing |

**Supply-chain implications**: If an author's private key is compromised, their packs are affected. The index fingerprint provides a secondary signal (key mismatch = alert). Typosquatting risk exists but is mitigated by namespace review at PR time.

### Recommendation

**Adopt model (c) — the hybrid community index.**

Rationale:
1. It preserves the existing caller-supplied trust model (`PackVerifier` already handles per-author keys) without requiring a central signing authority.
2. It adds discoverability (a single queryable index) without requiring the maintainer to review pack content.
3. The two-factor verification (SHA-256 checksum + RSA-PSS signature) is materially stronger than model (a)'s single central key: a compromised author key only affects that author's packs.
4. It maps cleanly to existing conventions (Helm Hub, Homebrew taps, VS Code marketplace) that have survived real-world abuse and supply-chain incidents.
5. The official key slot in `TrustedKeys.OfficialPublicKeyPem` can represent the PAL-X project's own key for first-party packs; third-party packs use author-provided keys configured by the consumer — no model change required.

If community governance is not yet available (see Section 7), start with a read-only pull from a single first-party signed source (see Section 6, Phase 1), which is essentially model (a) without the third-party author complexity.

---

## 4. Trust & Supply-Chain

### Trusted key provisioning

Keys are provisioned through three channels:

1. **Embedded official key** — `TrustedKeys.OfficialPublicKeyPem` (`TrustedKeys.cs:9`). Currently empty. For first-party packs, the PAL-X project generates an RSA-3072 (or Ed25519) key pair; the public key is baked into the binary. Authors of first-party packs sign with the corresponding private key (held offline).

2. **Consumer-configured keys** — `TrustedKeys.DefaultTrusted(extraKeyPemPath?)` (`TrustedKeys.cs:37`). Consumers configure one or more author public keys via `appsettings.json` or a trusted-keys directory. These correspond to third-party pack authors. `PackVerifier.Verify` already accepts a list — no API change is needed.

3. **Index-recorded fingerprints** — in the hybrid model, the community index records the SHA-256 fingerprint of each author's public key. The CLI can warn if a locally-configured key's fingerprint does not match the index entry.

### Key rotation

Key rotation is the primary operational risk. The protocol must be:
- Old key signs a "rotation notice" document attesting to the new key.
- The index entry is updated (via pull request) with the new fingerprint.
- Consumers who have pre-configured the old key receive an out-of-band notification (release notes, RSS, etc.) to reconfigure the new key.
- A grace period allows both keys to be trusted simultaneously.
- There is **no automated key rotation** — key updates require explicit consumer action.

### `SignatureRequirement` for remote packs

Remote packs (fetched from any external source) MUST be loaded with `SignatureRequirement.Required`. The `PackLoader.EnforceSignature` path (`PackLoader.cs:44–63`) already implements this correctly; the new pull path must pass `SignatureRequirement.Required` and a non-empty trusted keys list. An empty trusted keys list produces `VerificationResult(false, keyId, "NoTrustedKeys")` (`PackVerifier.cs:42–43`) — the pull must fail rather than silently succeed.

The current default of `SignatureRequirement.Optional` is acceptable for **local packs loaded from disk** where the disk contents are controlled by the server operator. It is **not acceptable** for packs fetched from any remote source.

### TOFU vs. pre-configured trust

**TOFU (Trust On First Use) is rejected** for remote packs. TOFU provides no protection against a malicious or compromised pack being fetched on the first pull — the moment when protection is most needed. TOFU may feel convenient for developers but creates a false sense of security.

**Pre-configured trust is required**: a consumer must explicitly add an author's public key to their trust configuration before they can pull that author's packs. This is a deliberate friction point that forces the operator to make a trust decision consciously.

**Fingerprint-only mode** (listing available packs from an index without downloading them) can proceed without a trusted key; only the pull/install step requires verification.

### Tampering and typosquatting prevention

| Threat | Mitigation |
|---|---|
| MITM during download | SHA-256 checksum from index compared before signature verification |
| Compromised artifact host | RSA-PSS signature (signed with author's offline key) is independent of host |
| Typosquatting (`windows-core` vs `w1ndows-core`) | Pack-id namespace review at index PR time; `PackValidator` enforces kebab-case |
| Malicious pack content | `PackValidator` enforces structural constraints; rules are declarative and have no execution capability |
| Key compromise | Key rotation protocol; fingerprint mismatch alert from index |
| Registry index tampering | Index is a Git repository; consumers can pin a specific commit SHA |

---

## 5. Proposed API / CLI Surface

These are sketches — not implementation specifications. All new operations reuse `PackVerifier` and `PackValidator` unchanged; new code is limited to HTTP fetch, index parsing, and local storage management.

### New CLI commands

```
pal packs search <query>
    Queries the configured registry index for packs matching <query>.
    Displays: id, version, description, author, key fingerprint.
    Does NOT download; requires no trusted key.
    Reads: Packs:RegistryIndexUrl from config.

pal packs pull <id>@<version>  [--trust-key <pubkey.pem>]
    1. Fetches the index entry for <id>@<version>.
    2. Downloads pack.yaml and pack.yaml.sig from download_url / sig_url.
    3. Verifies SHA-256 checksum against the index entry.
    4. Calls PackVerifier.Verify with the supplied --trust-key (or configured
       keys for this source); fails if no trusted key matches.
    5. Calls PackValidator.Validate; fails if invalid.
    6. Writes the pack to the local packs directory (Packs:Directory).
    Error cases: MissingSignature, InvalidSignature, NoTrustedKeys,
    ChecksumMismatch, ValidationError.

pal packs publish <dir>  [--key <privkey.pem>]  [--registry <url>]
    (Phase 2+) Signs the pack if not already signed, then submits a PR or
    API call to add the pack to the index. Exact mechanism depends on index
    hosting choice.
```

### New API endpoints

All pack endpoints remain globally scoped (not workspace-scoped), consistent with `Program.cs:184`.

```
GET /packs/registry/search?q=<query>&page=<n>
    Proxies or caches results from the configured registry index.
    Returns: [{ id, version, description, author, keyFingerprint, downloadUrl }]

POST /packs/pull  { "packId": "...", "version": "...", "trustKeyPem": "..." }
    Server-side pull: fetches, verifies (SignatureRequirement.Required),
    validates, and stores a pack from the remote registry.
    Requires: operator-level auth (not regular API key — see below).
    Returns: 201 Created with { id, version } on success, 422 with errors
    on validation or signature failure.
```

**Scoping note**: Pack pull is an operator-level operation (it modifies server-local storage that affects all workspaces). It must not be callable by workspace-level API keys. This implies a new auth tier or a configuration flag to disable remote pull entirely for hosted deployments.

### Reuse of existing components

| Existing component | Reuse in pull path |
|---|---|
| `PackVerifier.Verify(dir, trustedKeys)` | Called after download, before storage — no change |
| `PackValidator.Validate(pack)` | Called after signature verification — no change |
| `PackRegistrySyncService.SyncAsync` | Called after local write to register the new pack in the DB — no change |
| `PackEndpoints` (list/versions/validation) | Unchanged — pulled packs become normal local packs |

---

## 6. Phasing

### Phase 1 — Read-only pull from a single configured signed source (smallest increment)

**Scope**: `pal packs pull <id>@<version> --trust-key <pub.pem>` against a single URL configured as `Packs:RemoteBaseUrl` in `appsettings.json`. No index — the operator specifies the exact URL pattern. The server URL convention is `<base>/<id>/<version>/pack.yaml` and `<base>/<id>/<version>/pack.yaml.sig`.

**What this adds**:
- HTTP fetch of pack.yaml + pack.yaml.sig.
- SHA-256 checksum comparison (hash the downloaded bytes; compare to an optional expected hash in the pull command).
- `SignatureRequirement.Required` + caller-supplied trust key.
- Write to `Packs:Directory` and call `SyncAsync` to register.

**New code estimate**: ~2 new classes (`RemotePackFetcher`, `PackPullService`), one new CLI command, one optional API endpoint. `PackVerifier`, `PackValidator`, `PackRegistrySyncService` are unchanged.

**What Phase 1 deliberately excludes**: discovery/search, index, multi-source, publish.

### Phase 2 — Community index + search

Adds the registry index format, `pal packs search`, and multi-source key configuration. Requires a governance decision (Section 7) about who hosts and maintains the index.

### Phase 3 — Publish workflow

Adds `pal packs publish`, a PR-based or API-based submission to the community index, and the `POST /packs/pull` server-side endpoint. Requires key-rotation protocol to be documented and published.

### Phase 4 — Key management UX

Trusted key management via `pal packs trust-key add/remove/list`, fingerprint verification warnings, rotation notices.

**Sizing note**: This is an L-effort build across all phases. Phase 1 alone is an S–M effort (no index, no discovery, no publish). Phases 2–4 collectively constitute the bulk of the L estimate. Delivering Phase 1 first provides real value (operators can pull first-party packs without copying directories) while governance decisions for Phases 2–4 are made.

---

## 7. Open Questions for the Maintainer

These are governance and product decisions — not technical ones. They must be resolved before committing to Phase 2 or beyond.

**Q1 — Who curates the community index?**  
The hybrid model requires someone to review namespace-reservation PRs (to prevent typosquatting). Is this the PAL-X maintainer, a volunteer committee, or is the registry intentionally kept internal (first-party packs only, no third-party submission)?

**Q2 — Is there an org or community to host it?**  
A community registry requires a GitHub org, a domain, and a human willing to respond to abuse reports. If the project is pre-community (currently internal), start with Phase 1 only and defer the community index.

**Q3 — Does the registry stay internal-only?**  
If PAL-X is an internal tool (single-org deployment), the shareable layer may only need to solve "how do we distribute first-party packs to multiple PAL-X instances" — not community sharing. Phase 1 solves this. Phases 2–4 are only relevant if third-party pack authors are expected.

**Q4 — What is the key management story for end operators?**  
Operators configuring third-party trust keys must understand they are making a supply-chain decision. Is there UX (a `pal packs trust-key` command, a UI panel) to help them manage this, or is it raw PEM configuration? This affects Phase 1 UX design.

**Q5 — Should `TrustedKeys.OfficialPublicKeyPem` be populated for Phase 1?**  
If Phase 1 distributes first-party packs only, PAL-X should generate and embed an official signing key. This commits the project to maintaining that key long-term. If the project is still pre-1.0, consider deferring the official key until the first stable release.

**Q6 — How is the pack namespace governed?**  
`PackValidator` enforces that `pack_id` is kebab-case but does not enforce namespace ownership. In a multi-author registry, `com.example.windows-core` vs `windows-core` vs a simple flat namespace all have different collision properties. This is a naming convention decision, not a technical one.

---

## Appendix: Key file references

| File | Relevance |
|---|---|
| `dotnet/src/Pal.Packs/Signing/PackVerifier.cs` | Core verification logic; `trustedKeys` model |
| `dotnet/src/Pal.Packs/Signing/PackSigner.cs` | RSA-PSS-SHA256 signing; sidecar format; keyid derivation |
| `dotnet/src/Pal.Packs/Signing/TrustedKeys.cs` | `OfficialPublicKeyPem` placeholder; `DefaultTrusted()` |
| `dotnet/src/Pal.Packs/Signing/SignatureRequirement.cs` | `Optional / Required / Forbidden` enum |
| `dotnet/src/Pal.Packs/PackLoader.cs` | `EnforceSignature` (lines 44–63); default `Optional` |
| `dotnet/src/Pal.Packs/PackValidator.cs` | Structural validation; enum domains; schema version gate |
| `dotnet/src/Pal.Api/Services/PackRegistrySyncService.cs` | Disk-sync; upsert into DB; no signature enforcement |
| `dotnet/src/Pal.Api/Endpoints/PackEndpoints.cs` | List/versions/validation endpoints; global scope |
| `dotnet/src/Pal.Api/Program.cs:166–168` | Startup sync call |
| `dotnet/src/Pal.Api/Program.cs:184` | Global (non-workspace) pack endpoints registration |
| `dotnet/src/Pal.Persistence/Entities/PackEntity.cs` | Registry persistence; `StoragePath` not exposed |
| `dotnet/src/Pal.Persistence/Entities/PackVersionEntity.cs` | Per-version storage record |
| `dotnet/src/Pal.Cli/Commands/Packs/SignPackCommand.cs` | `pal packs sign` CLI surface |
| `dotnet/src/Pal.Cli/Commands/Remote/RemotePacksCommand.cs` | `pal remote packs` — list only |
| `dotnet/src/Pal.Cli/Commands/Remote/RemoteValidatePackCommand.cs` | `pal remote validate-pack` |
