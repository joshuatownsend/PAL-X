---
title: Use the HTTP API
description: End-to-end automation against the PAL-X API — mint a token, upload a capture, run an analysis, fetch the report.
---

# Use the HTTP API

Goal: drive PAL-X from a script or non-CLI integration. This guide assumes you have an API running locally on the default port (`5043`) and a bootstrap admin account.

For the auth model, see **[Concepts — Multitenancy and auth](../concepts/multitenancy-and-auth.md)**. For per-endpoint contracts, see **[HTTP API — Index](../reference/http-api/index.md)**.

## 1. Bootstrap a token

API keys are minted via the workspace-scoped `/tokens` endpoint, which requires authentication itself. The API doesn't accept HTTP Basic auth — non-`Bearer` `Authorization` headers are forwarded to the cookie scheme. Log in first to capture a cookie, then mint:

```bash
WS=00000000-0000-0000-0000-000000000002   # default workspace id

# Log in (form POST), capture cookie
curl -X POST http://localhost:5043/account/login \
  -c cookies.txt -L \
  -d "email=admin@example.com&password=your-bootstrap-password"

# Mint a token using the cookie
curl -X POST "http://localhost:5043/api/workspaces/$WS/tokens" \
  -b cookies.txt \
  -H "Content-Type: application/json" \
  -d '{"name":"automation"}'
```

Response:

```json
{
  "id": "…",
  "name": "automation",
  "createdAt": "2026-05-15T10:00:00Z",
  "token": "pal_AbCdEf0123…"
}
```

**The raw `token` field is returned exactly once.** Capture it; you can't retrieve it later.

```bash
TOKEN=pal_AbCdEf0123…
```

For all subsequent calls, pass it as `Authorization: Bearer $TOKEN`.

## 2. Upload a capture

```bash
curl -X POST http://localhost:5043/api/workspaces/$WS/uploads \
  -H "Authorization: Bearer $TOKEN" \
  -F file=@capture.csv
```

Response:

```json
{
  "uploadId": "…",
  "fileName": "capture.csv",
  "sourceType": "csv"
}
```

```bash
UPLOAD_ID=…
```

The upload step is dedup-aware: if the SHA-256 of `capture.csv` matches an existing upload, the API returns the existing record (`200 OK` instead of `201 Created`) and the bytes aren't re-stored.

## 3. Submit an analysis

```bash
curl -X POST http://localhost:5043/api/workspaces/$WS/analysis \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"uploadId\":\"$UPLOAD_ID\",\"packs\":[\"windows-core\"]}"
```

Response:

```json
{
  "analysisId": "…",
  "status": "queued"
}
```

```bash
JOB_ID=…
```

The status is `queued` → `running` → `completed`. The work happens asynchronously in `AnalysisWorker` background service.

### Optional flags

| Field | Effect |
|---|---|
| `includeDataset: true` | Also persist the gzipped dataset so you can download it later via `/dataset` |
| `selectedBaselineId: <jobId>` | Auto-compare against this baseline when the job completes |
| `packs: ["windows-core@1.0.0"]` | Pin to a specific pack version (otherwise the latest applies) |

## 4. Poll for completion

```bash
while true; do
  STATUS=$(curl -s -H "Authorization: Bearer $TOKEN" \
    http://localhost:5043/api/workspaces/$WS/analysis/$JOB_ID \
    | jq -r .status)
  case "$STATUS" in
    completed) break ;;
    failed)    echo "job failed"; exit 1 ;;
  esac
  sleep 5
done
```

On a typical 1-hour capture, analysis completes in a few seconds. On a multi-day capture with charts enabled, it can take longer.

## 5. Fetch the report

```bash
# JSON — canonical
curl -s -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5043/api/workspaces/$WS/analysis/$JOB_ID/report?format=json" \
  -o report.json

# HTML — browser-friendly
curl -s -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5043/api/workspaces/$WS/analysis/$JOB_ID/report?format=html" \
  -o report.html
```

Inspect the verdict:

```bash
jq '.summary.overall_status, .summary.finding_counts' report.json
```

For the JSON shape see **[Reference — Report schema](../reference/report-schema.md)**.

## End-to-end script

Putting it all together in a single bash script (production version would be more defensive):

```bash
#!/usr/bin/env bash
set -euo pipefail

API=http://localhost:5043
WS=00000000-0000-0000-0000-000000000002
TOKEN=$PAL_API_KEY   # set externally

# Upload
UPLOAD_ID=$(curl -sf -X POST "$API/api/workspaces/$WS/uploads" \
  -H "Authorization: Bearer $TOKEN" \
  -F file=@"$1" \
  | jq -r .uploadId)

# Submit
JOB_ID=$(curl -sf -X POST "$API/api/workspaces/$WS/analysis" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"uploadId\":\"$UPLOAD_ID\",\"packs\":[\"windows-core\"]}" \
  | jq -r .analysisId)

# Poll
while true; do
  STATUS=$(curl -sf -H "Authorization: Bearer $TOKEN" \
    "$API/api/workspaces/$WS/analysis/$JOB_ID" \
    | jq -r .status)
  [ "$STATUS" = "completed" ] && break
  [ "$STATUS" = "failed" ] && { echo "❌ failed"; exit 1; }
  sleep 5
done

# Verdict
curl -sf -H "Authorization: Bearer $TOKEN" \
  "$API/api/workspaces/$WS/analysis/$JOB_ID/report?format=json" \
  | jq '.summary'
```

## PowerShell version

```powershell
$API = 'http://localhost:5043'
$WS = '00000000-0000-0000-0000-000000000002'
$h = @{ Authorization = "Bearer $env:PAL_API_KEY" }

$upload = Invoke-RestMethod -Method Post -Uri "$API/api/workspaces/$WS/uploads" `
  -Headers $h -Form @{ file = Get-Item $args[0] }

$job = Invoke-RestMethod -Method Post -Uri "$API/api/workspaces/$WS/analysis" `
  -Headers $h -ContentType 'application/json' `
  -Body (@{ uploadId = $upload.uploadId; packs = @('windows-core') } | ConvertTo-Json)

do {
  Start-Sleep -Seconds 5
  $status = (Invoke-RestMethod -Headers $h -Uri "$API/api/workspaces/$WS/analysis/$($job.analysisId)").status
} while ($status -notin 'completed','failed')

if ($status -eq 'failed') { throw 'analysis failed' }

(Invoke-RestMethod -Headers $h -Uri "$API/api/workspaces/$WS/analysis/$($job.analysisId)/report?format=json").summary
```

## Pitfalls

| Symptom | Cause | Fix |
|---|---|---|
| `401 Unauthorized` on `/tokens` | Bootstrap admin credentials wrong | Check `appsettings.json` `IdentitySeeder` config |
| `403 Forbidden` on `/api/workspaces/...` | Wrong workspace id, or token user not an org member | Use the default workspace id `…000002` or add the user to the org |
| `404 Not Found` on submit | Wrong upload id | Re-check the upload response |
| `409 Conflict` on report fetch | Job not yet `completed` | Poll until status flips |
| `502 Bad Gateway` on webhook test | Receiver endpoint unreachable | See **[Configure webhooks](configure-webhooks.md)** |

## Related

- **[Concepts — Multitenancy and auth](../concepts/multitenancy-and-auth.md)** — the underlying model.
- **[HTTP API — Index](../reference/http-api/index.md)** — error model, status codes, content types.
- **[Reference — Report schema](../reference/report-schema.md)** — the JSON you'll be parsing.
- **[CLI — `pal remote`](../reference/cli/pal-remote.md)** — the shorter path if you can use the CLI.
