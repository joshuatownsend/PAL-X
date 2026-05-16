---
title: Packs
description: List packs in the registry, list versions of a pack, validate a stored pack against the schema.
---

# Packs

Pack management lives at the global root (no workspace prefix) — packs are a shared resource across orgs. The endpoints below read from the pack registry that `PackRegistrySyncService` populates at startup from the `Packs:Directory` config path.

To upload a new pack, use the Blazor UI at `/packs` — there is no HTTP `POST /packs` endpoint today.

| Endpoint | Verb | Auth |
|---|---|---|
| `/packs` | `GET` | required |
| `/packs/{id}/versions` | `GET` | required |
| `/packs/{id}/versions/{version}/validation` | `GET` | required |

## `GET /packs`

List every pack in the registry.

```json
{
  "items": [
    { "packId": "windows-core", "name": "Windows Core", "latestVersion": "1.0.0" }
  ]
}
```

## `GET /packs/{id}/versions`

List all stored versions of one pack. Returns `404` if the pack id is unknown.

```json
{
  "items": [
    { "packId": "windows-core", "version": "1.0.0", "createdAt": "…" }
  ]
}
```

The internal `StoragePath` is deliberately not exposed — that field is server-local and could leak filesystem layout.

## `GET /packs/{id}/versions/{version}/validation`

Load the stored pack YAML and run it through `PackValidator`. Returns the same shape `pal validate-pack` produces.

### Response (valid)

```json
{
  "isValid": true,
  "errors": [],
  "warnings": []
}
```

### Response (invalid)

```json
{
  "isValid": false,
  "errors": [
    "Rule 'high-cpu': invalid severity 'major'"
  ],
  "warnings": []
}
```

### Status codes

- `200 OK` — validation ran (regardless of whether the pack is valid).
- `404 Not Found` — pack id or version unknown.
- `422 Unprocessable Entity` — the pack failed to load (malformed YAML, signature failure, IO error). Different from validation failure: this is a "couldn't even parse it" condition.

### Example

```bash
curl http://localhost:5043/packs/windows-core/versions/1.0.0/validation \
  -H "Authorization: Bearer pal_xxx"
```

## Related

- **[`pal remote validate-pack`](../cli/pal-remote-validate-pack.md)** — same logic, CLI front-end.
- **[`pal validate-pack`](../cli/pal-validate-pack.md)** — local validation against a filesystem pack.
- **[Pack schema v1](../pack-schema-v1.md)** — what `isValid` means.
