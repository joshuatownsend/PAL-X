---
title: Storage layout
description: What lives where under data/storage, what gets created when, and how it's tied to job and upload lifecycles.
---

# Storage layout

PAL-X writes uploads, reports, and datasets to a single directory tree under `Storage:LocalRoot`. The structure is the contract that lets the retention worker, backup workflows, and out-of-band cleanup all reason about the same files.

For the setting, see **[Configuration — Storage](../reference/configuration.md#storage)**.

## Top-level layout

```text
${Storage:LocalRoot}/
├── uploads/
│   └── <sha256>/
│       └── <original-filename>
├── reports/
│   └── <jobId>/
│       ├── report.json
│       ├── report.html
│       └── report.md
└── datasets/
    └── <jobId>/
        └── dataset.json.gz       (only if includeDataset:true)
```

Chart SVGs are CLI-only (`pal analyze --include-charts` writes to `<output>/charts/`). The API does not produce chart artifacts.

That's it. There's no per-workspace prefix on disk — the workspace-id scoping is enforced at the database / repository layer, not at the filesystem. Multiple workspaces share the same on-disk pool because of upload SHA-256 dedup, which is workspace-blind.

## `uploads/<sha256>/`

When a file is uploaded via `POST /uploads`, the bytes are SHA-256-hashed. If the hash already exists, the existing record is returned and the new bytes are discarded. If new, the file is committed to `uploads/<sha256>/<original-filename>`.

The directory name is the SHA-256 hex digest of the file content (64 chars). The leaf is the original filename — preserved so you can identify the source on disk.

Multiple jobs can reference the same upload. The upload is deleted by the retention worker only when **every job referencing it** has been purged.

## `reports/<jobId>/`

When an analysis job completes on the **API server**, `AnalysisWorker` writes all three formats unconditionally:

- `report.json` — the canonical JSON document.
- `report.html` — the HTML rendering.
- `report.md` — GFM Markdown.

The **CLI** is different: `pal analyze` writes JSON and HTML by default and only writes Markdown if `--markdown` is passed. Chart SVGs are also CLI-only — `pal analyze --include-charts` writes to `<output>/charts/`; the API does not generate charts.

The directory name is the job's `Guid` in string form. The report endpoint streams these files directly.

## `datasets/<jobId>/`

If the job was submitted with `includeDataset: true`, the analyzer also writes `dataset.json.gz` here — the gzipped JSON dataset (samples + statistics, normalised to canonical metric IDs). See **[Download a dataset](../guides/download-dataset.md)** for retrieval.

Without that flag, no `datasets/<jobId>/` directory is ever created.

## Sizes — what to budget

Rough rules of thumb (your mileage varies with capture density):

| Item | Typical size |
|---|---|
| Upload (CSV, 1-hour capture, 100 counters @ 15s) | 5–10 MB |
| Upload (BLG, same capture) | 1–2 MB |
| Report JSON | 100–500 KB |
| Report HTML | 150–600 KB |
| Charts (per SVG) | 20–50 KB |
| Dataset gzip | 1–3 MB (smaller than the original CSV thanks to canonical-ID normalisation and gzip) |

For a deployment processing 100 captures/day at the above sizes with `JobRetentionDays = 90`, expect on-disk storage growth of ~50 GB before retention catches up. Datasets dominate if you turn them on by default.

## Encoding

All JSON, HTML, and Markdown artifacts are written with `new UTF8Encoding(false)` — UTF-8 **without BOM**. This matters for byte-identical golden-fixture testing; never re-encode through a tool that adds a BOM.

SVG charts are written by ScottPlot — same encoding rules apply.

## Determinism

For the same input + same packs + same `--now`, the analyzer produces byte-identical reports. This is the contract that makes golden tests work — see `fixtures/cpu-pressure/golden.pal-report.json`.

Implication: if you find two reports for "the same" job differing byte-for-byte, something in the pipeline changed (engine version, pack version, input). A diff utility on the JSON will show you what.

## Disk pressure failure modes

If `Storage:LocalRoot` runs out of disk:

- **Uploads fail at commit time.** The temp file write succeeds but `CommitUploadAsync` throws on rename — the API returns `500 Internal Server Error`. Storage is not modified; the temp file is cleaned up.
- **Reports fail at write time.** The analysis ran but the report writer threw — job marked `failed`, error message in `errorMessage`.
- **Datasets fail at gzip-write time.** Same: job failed; dataset partial-write is cleaned up via the catch handler.
- **Charts fail individually.** A chart write failure marks just that finding as missing its chart artifact; the rest of the report still ships.

None of these corrupt existing data. The repository's "delete files only after DB commit" pattern keeps state consistent.

## File permissions

The API process needs:

- Read + write to `${Storage:LocalRoot}/`.
- Subdirectory create permissions (the API creates `reports/<jobId>/` etc. lazily).

In Docker:

```dockerfile
VOLUME /data/storage
```

The container runs as the default `dotnet` runtime user; mount with appropriate ownership.

In systemd:

```bash
sudo chown -R pal:pal /var/lib/pal/storage
```

The systemd unit's `User=pal Group=pal` then has access.

## Path resolution

If `Storage:LocalRoot` is relative, it's resolved against the API's working directory (`Path.GetFullPath`). In production, **always use an absolute path** to avoid ambiguity:

```text
Storage__LocalRoot=/var/lib/pal/storage    # Linux
Storage__LocalRoot=C:\PAL\Storage          # Windows
Storage__LocalRoot=/data/storage           # Docker
```

The shipped default `data/storage` is fine for development but should be replaced for any deployment that doesn't always run from the repo root.

## Related

- **[Configuration — Storage](../reference/configuration.md#storage)** — settings.
- **[Retention](retention.md)** — what gets deleted when.
- **[Backup and restore](backup-and-restore.md)** — backing up the directory tree.
- **[Download a dataset](../guides/download-dataset.md)** — when datasets land here.
