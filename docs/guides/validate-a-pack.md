---
title: Validate a pack
description: Use pal validate-pack locally and in CI — including --strict, signature enforcement, and JSON output.
---

# Validate a pack

Goal: catch pack-authoring mistakes before they reach an analysis run. Validation is fast (milliseconds) and is the right gate to put in CI before publishing a pack.

For the underlying constraints, see **[Reference — Pack schema v1](../reference/pack-schema-v1.md)** and **[Pack schema v1.1](../reference/pack-schema-v1.1.md)**. For the command's flag-level reference, see **[CLI — `pal validate-pack`](../reference/cli/pal-validate-pack.md)**.

## Quick check during authoring

The minimum command to validate one pack:

```bash
pal validate-pack --path packs/local/my-cpu-pack
```

Exit code `0` and `Pack is valid.` means you're good. Anything else is an error or strict warning that needs fixing.

You can also point `--path` at a parent directory containing multiple packs; the validator walks down and validates each one it finds.

## In CI — treat warnings as errors

`--strict` turns informational warnings into hard failures. Use it in CI so reviewers don't get to merge a pack with `Pack 'foo' contains no rules` or similar.

```bash
pal validate-pack \
  --path packs/local/my-cpu-pack \
  --strict
```

For a CI job, capture the result as JSON so the pipeline can pretty-print or annotate it:

```bash
pal validate-pack \
  --path packs/local/my-cpu-pack \
  --strict \
  --json-output validation.json
```

The CLI emits snake_case fields plus pack metadata:

```json
{
  "pack_id": "my-cpu-pack",
  "version": "0.1.0",
  "rule_count": 3,
  "is_valid": true,
  "errors": [],
  "warnings": []
}
```

The **HTTP API**'s pack-validation endpoint emits a slimmer camelCase shape — see [`GET /packs/{id}/versions/{version}/validation`](../reference/http-api/packs.md#get-packsidversionsversionvalidation). The two shapes are not identical today.

## With signature enforcement

If you publish signed packs (see **[Sign and trust packs](sign-and-trust-packs.md)**), CI should also verify the signature against the trust list:

```bash
pal validate-pack \
  --path packs/published/my-cpu-pack \
  --require-signature \
  --trust-key tools/keys/team.pub.pem \
  --strict
```

`--trust-key` accepts a single extra public key (in addition to the project's built-in trust list). If you need to trust multiple external keys today, concatenate them into a single PEM file or hand-roll the trust list via the .NET API. Without `--require-signature`, a missing or invalid signature is silently ignored — which is the right default for local authoring but wrong for CI on published packs.

## A worked CI snippet (GitHub Actions)

```yaml
- name: Validate packs
  run: |
    for pack in packs/published/*/; do
      pal validate-pack \
        --path "$pack" \
        --strict \
        --require-signature \
        --trust-key tools/keys/team.pub.pem \
        --json-output "validation-$(basename $pack).json"
    done
```

Each pack produces its own validation JSON; you can upload them as artifacts for review.

## Common errors

The validator's messages are deliberately specific — each error says which rule and which field. Common patterns:

| Error | Cause | Fix |
|---|---|---|
| `schema_version 'pal.pack/v2' is not recognized` | Typo or unsupported version | Use `pal.pack/v1` or `pal.pack/v1.1`. |
| `Rule 'foo': invalid severity 'major'` | Severity not in the enum | One of `critical`, `warning`, `informational`. |
| `Rule 'foo', metric 'bar': invalid aggregation 'sum'` | Aggregation not in the enum | One of `avg`, `min`, `max`, `p90`, `p95`, `p99`, `trend`. |
| `Rule 'foo', metric 'bar': aggregation 'trend' is not supported with 'window'` | `trend` evaluated over a rolling window | Drop the `window:` block or pick a different aggregation. |
| `Rule 'foo', metric 'bar': 'window' requires schema_version pal.pack/v1.1` | v1.1 feature on a v1 pack | Bump `schema_version`. |
| `Rule 'foo': references recommendation 'bar' which is not defined in the pack` | Typo in a recommendation ID | Match the ID in the pack-level `recommendations:` map. |

## Same validator via HTTP

If you've already published a pack to the server, validate the stored version via:

```bash
pal remote validate-pack <pack-id> <version>
```

This hits **[`GET /packs/{id}/versions/{version}/validation`](../reference/http-api/packs.md#get-packsidversionsversionvalidation)** which loads the stored YAML and runs the same `PackValidator`. The output shape is identical.

## Related

- **[CLI — `pal validate-pack`](../reference/cli/pal-validate-pack.md)** — every flag, every exit code.
- **[HTTP API — `GET /packs/{id}/versions/{version}/validation`](../reference/http-api/packs.md#get-packsidversionsversionvalidation)** — server-side validation.
- **[Write a pack](write-a-pack.md)** — author the pack you're now validating.
- **[Sign and trust packs](sign-and-trust-packs.md)** — pair signing with validation in CI.
