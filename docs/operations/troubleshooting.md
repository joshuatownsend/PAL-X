---
title: Troubleshooting
description: Common operational failures — port collisions, BLG on Linux, broken seed, FK errors, migration drift.
---

# Troubleshooting

A catalogue of failures we've actually seen. Each entry has the symptom you'll observe, the root cause, and the fix.

## Postgres port collision (5432 already in use)

**Symptom:** `docker compose up` fails with `Error response from daemon: bind: address already in use` on the Postgres service.

**Cause:** Another Postgres is already running on the host port (often a native installation or another container).

**Fix:** Set `POSTGRES_PORT_HOST` to a free port before `docker compose up`:

```bash
export POSTGRES_PORT_HOST=5433
docker compose up -d
```

The container-internal port is unchanged (Postgres still listens on `5432` inside the container); only the host-side mapping moves. Other services on the compose network reach Postgres at `postgres:5432` regardless.

## BLG on non-Windows host

**Symptom:** `pal analyze --input capture.blg` exits with `PlatformNotSupportedException: BLG ingestion requires Windows.`

**Cause:** PAL-X reads BLG via Windows PDH (Performance Data Helper). PDH is Windows-only.

**Fix:** Convert to CSV on a Windows machine using `relog`:

```powershell
relog -f CSV capture.blg -o capture.csv
```

Then analyse `capture.csv` on any platform. See **[Convert BLG on Linux](../guides/convert-blg-on-linux.md)** for the CI pattern.

## Bootstrap admin not created

**Symptom:** Cannot log in as `admin@pal.local` after first boot. No errors at startup.

**Cause:** `PAL_BOOTSTRAP_ADMIN_PASSWORD` (or `Auth:BootstrapAdminPassword`) wasn't set when the API first started.

**Fix:** Set the env var and restart:

```bash
export PAL_BOOTSTRAP_ADMIN_PASSWORD='strong-password'
systemctl restart pal-api
```

If the admin user still doesn't exist after a restart, check the startup logs for the message `Bootstrap admin account created: admin@pal.local`. The seeder is idempotent — it won't create the admin if it already exists, even with a different password.

If you suspect the admin row exists with a forgotten password, see **[Auth and tokens — Rotate the admin password](auth-and-tokens.md#rotate-the-admin-password)**.

## Migration fails on startup

**Symptom:** API exits at startup with `An error occurred while applying migration 'XXXXXX_Name'`.

**Cause:** Database in an unexpected state — schema edited out-of-band, partial migration from a prior crashed startup, or missing privileges.

**Fix:** Don't hand-fix the schema. Restore from your most recent backup (you have one, per **[Upgrading](upgrading.md)**) and try again with the database in a known state.

If the migration failed because the API user can't `CREATE`/`ALTER`, run migrations as a privileged user out-of-band — see **[Postgres setup — Run migrations out-of-band](postgres-setup.md#run-migrations-out-of-band)**.

## `403 Forbidden` on `/api/workspaces/...`

**Symptom:** An authenticated user gets `403 Forbidden` on every workspace-scoped endpoint.

**Cause:** The user isn't a member of the org that owns the workspace, or the workspace id is wrong, or the user is a member of a different org.

**Fix:**

1. Confirm the workspace exists: `curl http://localhost:8080/api/workspaces/$WS/analysis -H "Authorization: Bearer $ADMIN_TOKEN"` (using an admin from the right org).
2. Confirm the user's memberships: `curl http://localhost:8080/api/orgs/$ORG/members -H "Authorization: Bearer $ADMIN_TOKEN"`.
3. Add membership if missing: `curl -X PUT http://localhost:8080/api/orgs/$ORG/members/$USER_ID -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" -d '{"role":"Analyst"}'`.

If the user expected to use the default tenant: the default workspace id is `00000000-0000-0000-0000-000000000002` under org `00000000-0000-0000-0000-000000000001`.

## `401 Unauthorized` after token expiry

**Symptom:** Automation that worked previously suddenly returns `401 Unauthorized`.

**Cause:** The token's `expiresAt` passed, or it was revoked, or it was minted by a user that's been deleted.

**Fix:**

```bash
# List your tokens to find the expired one
curl http://localhost:8080/api/workspaces/$WS/tokens -H "Authorization: Bearer $ANOTHER_TOKEN"

# Mint a new one
curl -X POST http://localhost:8080/api/workspaces/$WS/tokens \
  -u admin@pal.local:<password> \
  -H "Content-Type: application/json" \
  -d '{"name":"automation-replacement"}'
```

Update the consumer with the new token. Optionally `DELETE` the old token.

## Foreign-key violation on tests

**Symptom:** Test runs fail with `Foreign key violation: insert or update on table "analysis_jobs" violates foreign key constraint "fk_analysis_jobs_workspace_workspace_id"`.

**Cause:** Test entity factories (`MakeJob`, `MakeUpload`, etc.) didn't set `WorkspaceId` and the DB-level cascade constraint rejects the row.

**Fix:** Every test factory must set `WorkspaceId = DefaultTenant.WorkspaceId`. This is the seeded workspace ID that every test relies on. See `CLAUDE.md` for the canonical guidance.

## Pack auto-load surprises

**Symptom:** A pack you expected to auto-apply doesn't fire any rules. Or one you expected not to apply has fired against the wrong dataset.

**Cause:** The pack's `applicability` block doesn't match the dataset's metrics.

**Fix:**

```bash
# Inspect what canonical metrics your dataset has
pal inspect-dataset --input capture.csv
```

Compare the dataset's canonical metrics against the pack's `applicability.requires_any` or `requires_all`. If the pack should always run regardless, change to `applicability.always: true`. If the pack should only run under specific conditions, verify the required metric IDs appear in the dataset.

For packs that use shipped metric aliases that don't match your captures (non-English Windows, vendor counters), add `metric_aliases:` to the pack.

## Host-context rule silently skipped

**Symptom:** A memory-relative or CPU-count-relative rule fires on some captures but not others, and you can't tell why.

**Cause:** The capture lacked host_context information, so the rule was skipped. PAL-X emits an informational warning (`host_context.unknown`) in the report.

**Fix:** Either:

1. Provide host context on the CLI: `--host-memory-mb 32768 --host-cpu-count 16`.
2. Drop a `host_context.json` sidecar next to the input file with the same values.
3. Use BLG captures that carry host info in their binary header.

## `unknown.*` series in reports

**Symptom:** Reports list series tagged `unknown.*` in their `series_index`.

**Cause:** The counter path didn't match any pattern in the built-in `MetricAliasRegistry`. The series is ingested but no rule matches it.

**Fix:** Add `metric_aliases:` to a pack that maps the counter path to a canonical ID. See **[Reference — Metric IDs: Pack-level metric_aliases](../reference/metric-ids.md#pack-level-metric_aliases)** for the format.

## Webhook test fails with 502

**Symptom:** `POST /webhooks/{id}/test` returns `502 Bad Gateway`.

**Cause:** The receiver endpoint is unreachable or returned a non-2xx status.

**Fix:**

1. Confirm the URL is correct (no typos, no stale token in the path).
2. Confirm the API host can reach the receiver (no firewall block, no DNS issue).
3. If the receiver requires auth, the webhook config doesn't carry credentials — receivers must accept un-credentialed PAL-X webhooks (signature verification is the auth model).
4. Check the receiver's logs for what it received.

## Retention worker never runs

**Symptom:** Old jobs accumulate; storage grows without bound. Logs don't show retention activity.

**Cause:** Both `Retention:JobRetentionDays` and `Retention:AuditEventRetentionDays` are `0`. The worker logs `retention disabled (both settings are 0); exiting` and stops.

**Fix:** Set at least one to a non-zero value and restart:

```bash
export Retention__JobRetentionDays=90
systemctl restart pal-api
```

The worker will start its daily loop after the 5-minute startup delay.

## Schedule never picks up files

**Symptom:** A schedule exists, files are dropped in the watched directory, but no jobs are created.

**Cause:** One of:

1. The schedule is `enabled: false`.
2. The files haven't been stable for `Schedules:FileStableAgeSeconds` (default 30s) since `LastWriteTime`.
3. The API process doesn't have read permission on the directory.
4. `Schedules:Enabled` (the global switch) is `false`.
5. The glob pattern doesn't match the filenames.

**Fix:** Walk through each:

```bash
# 1. Is the schedule enabled?
pal remote schedules get <id>

# 2. Are files old enough? touch them or wait
ls -la /var/captures/

# 3. Can the API process read?
sudo -u pal ls /var/captures/

# 4. Is the global flag on?
# (check env or appsettings; default is true)

# 5. Does the glob match?
ls /var/captures/*.csv
```

The worker logs at `Information` level when it ticks — check logs to see what it considered and what it skipped.

## API can't reach Postgres after restart

**Symptom:** API starts but every workspace-scoped call returns `500 Internal Server Error`. Logs show Npgsql connection errors.

**Cause:** Connection string is wrong, Postgres is down, network/firewall blocks the connection, or SSL is misconfigured.

**Fix:**

```bash
# Try psql with the same connection string the API uses
PGPASSWORD=… psql -h db.internal -U pal pal -c "SELECT 1"

# If that fails, the API can't connect either — fix the underlying issue.
```

For Docker compose: ensure the `api` service depends_on the `postgres` service with `condition: service_healthy`. The shipped compose file does this.

## CLI gets `unknown command`

**Symptom:** `pal remote <verb>` returns `unknown command: <verb>`.

**Cause:** Either you're on an older CLI version that doesn't have the verb, or you're calling a verb that's actually nested under `pal remote <group> <subcommand>` (e.g., `pal remote alerts ack` not `pal remote ack`).

**Fix:**

```bash
# See all top-level remote verbs
pal remote --help

# For nested groups (alerts, schedules, webhooks, baselines, packs)
pal remote alerts --help
```

The CLI reference lists every command — see **[Reference — CLI](../reference/cli/index.md)**.

## Related

- **[Monitoring](monitoring.md)** — surfaces the symptoms above.
- **[Backup and restore](backup-and-restore.md)** — when restore is the right answer.
- **[Upgrading](upgrading.md)** — most cross-version failures.
