# Pluggable Notification Channels Design

> **Status**: spike / pre-ADR  
> **Author**: design spike (plan 009), 2026-06-13  
> **Next step**: maintainer acceptance promotes the recommended staging to an ADR under
> `docs/architecture/adr/000N-notification-channels.md`, after which the staged
> implementation plans (payload formatters → SMTP transport → CLI parity) can be written

---

## 1. Current State

PAL-X's Phase 4 alerting fans every alert event out to operator-configured **sinks**, but
delivery is **HTTP-POST only**. The delivery abstraction and payload model are already
separated from transport, which is what makes multi-channel a tractable change — but no
sink carries a channel/type discriminator, and there is exactly one hard-coded transport.

### Delivery — `NotificationService`

`dotnet/src/Pal.Api/Services/NotificationService.cs` is the whole delivery surface. Two
public methods (`NotifyAsync`, `TestAsync`) both funnel through a single private
`DeliverAsync`, and the body is built once by `BuildPayload`:

```csharp
// NotificationService.cs:65-88
private async Task<System.Net.HttpStatusCode> DeliverAsync(WebhookSinkDto sink, byte[] payload, CancellationToken ct)
{
    using var req = new HttpRequestMessage(HttpMethod.Post, sink.Url)
    {
        Content = new ByteArrayContent(payload)
    };
    req.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json; charset=utf-8");

    if (!string.IsNullOrEmpty(sink.Secret))
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sink.Secret));
        req.Headers.Add("X-PAL-Signature",
            $"sha256={Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant()}");
    }

    using var resp = await _httpFactory.CreateClient("pal-webhook").SendAsync(req, ct);
    if (!resp.IsSuccessStatusCode)
        _logger.LogWarning("Sink {SinkName} returned HTTP {StatusCode}", sink.Name, (int)resp.StatusCode);
    return resp.StatusCode;
}

private static byte[] BuildPayload(string @event, AlertDto alert) =>
    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
        new { @event, timestamp = DateTimeOffset.UtcNow, alert }, _json));
```

What is **shared** infrastructure (and should survive any redesign):

- **Sink resolution + fan-out loop** — `NotifyAsync` (`:29-47`) calls
  `_repo.ListEnabledForEventAsync(@event, alert.WorkspaceId, ct)`, returns early if no
  sink subscribes, builds the payload once, then loops over sinks.
- **Per-sink error isolation** — each `DeliverAsync` call is wrapped in try/catch that logs
  a warning and moves on (`:41-45`). One failing sink never blocks the others. This is
  fire-and-log: no retry, no dead-lettering.
- **Test path reuse** — `TestAsync` (`:49-63`) constructs a synthetic `webhook.test`
  `AlertDto` and reuses the identical `DeliverAsync`, returning the HTTP status (or `null`
  when the sink id is unknown).

What is **hard-coded to HTTP** (and is the entire scope of this spike):

- `HttpMethod.Post` to `sink.Url` (`:67`) — there is no branch on a channel/type field.
- The single `{ @event, timestamp, alert }` JSON body (`:86-88`) is emitted verbatim
  regardless of destination; Slack and Teams would reject this shape.
- HMAC `X-PAL-Signature` signing (`:73-78`) is an HTTP-webhook concept; Slack/Teams
  incoming webhooks and SMTP have no equivalent.

### The sink model is generic — and has no channel discriminator

`WebhookSinkDto` (`dotnet/src/Pal.Application/Persistence/Dtos.cs:170-180`) and its EF
entity `WebhookSinkEntity` (`dotnet/src/Pal.Persistence/Entities/WebhookSinkEntity.cs:3-14`)
carry the same fields:

| Field | DTO type | Entity column | Notes |
|-------|----------|---------------|-------|
| `Id` | `Guid` | key | |
| `WorkspaceId` | (entity only) | tenant FK | global query filter (`PalDbContext.cs:175-180`) |
| `Name` | `string` | `Name` | |
| `Url` | `string` | `Url` | HTTP(S) endpoint |
| `Secret` | `string?` | `Secret` | HMAC key, plaintext (see secrets note §5) |
| `Enabled` | `bool` | `Enabled` | |
| `Events` | `IReadOnlyList<string>` | `Events` (comma-joined `string`) | subscribed events |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | same | |

There is **no `channel`/`type`/`deliveryType` field** on the DTO, the entity, or anywhere
in `Pal.Application/Webhooks` (`grep -rniE 'channel|sinktype|delivery.?type'
dotnet/src/Pal.Application/Webhooks` → no matches). The table is `webhook_sinks`
(`PalDbContextModelSnapshot.cs:938`) and columns follow PostgreSQL snake_case.

The service/repository contract is small and channel-agnostic:
`IWebhookSinkService` (`IWebhookSinkService.cs:5-12`) and `IWebhookSinkRepository`
(`IWebhookSinkRepository.cs:3-11`). `ListEnabledForEventAsync`
(`WebhookSinkRepository.cs:68-81`) is the one method on the hot alert path; it
`IgnoreQueryFilters()` and scopes by `workspaceId` manually because it runs in the worker
context where no tenant is set.

### CRUD + UI surface

`dotnet/src/Pal.Api/Endpoints/WebhookEndpoints.cs` exposes list/get/create/update/delete
plus `POST /webhooks/{id}/test` (`:58`). The create/update request records
(`CreateWebhookRequest`/`UpdateWebhookRequest`, `:99-100`) and the validator
`ValidateRequest` (`:81-90`) are where a `channel` field would surface to the API. The
validator currently **requires `Url` to be an absolute http(s) URI** (`:85-87`) — a rule
that must become channel-aware (email sinks have no URL).

The Blazor page `Components/Pages/Webhooks.razor` (`@page "/webhooks"`) is the only UI:
a single form with Name / URL / Secret / Enabled / event checkboxes (`:17-44`), already
warning operators that *"Secrets are stored in plaintext"* (`:9`). `NotifyAsync` is invoked
from `AlertService` for four events — `alert.created`, `alert.escalated`,
`alert.acknowledged`, `alert.resolved` (`AlertService.cs:53,70,91,103`).

### No CLI surface for webhooks

There is **no `RemoteWebhook*` command** under
`dotnet/src/Pal.Cli/Commands/Remote/` (directory listing shows none). Webhook/sink
management is API + Blazor only. This asymmetry is **noted, not fixed here** — adding CLI
parity is a separate item (see Open Questions §8 and Non-Goals §9).

### DI wiring

`Pal.Api/Program.cs:112-120` registers the repository, service, the named
`pal-webhook` `HttpClient` (10s timeout), and `INotificationService → NotificationService`
as singletons. Any new channel transport would register alongside these.

---

## 2. Goal

Adding a non-HTTP alert destination — first **Slack**, **Microsoft Teams**, and **email** —
should cost an operator one sink row with a `channel` selection, and cost a contributor:

- **One channel implementation** (a payload formatter for HTTP-relay channels; a real
  transport for SMTP) behind a small interface, resolved by the sink's `channel` value.
- **One registration point** in the notification layer where the dispatcher learns the
  new channel — no edits to `AlertService`, the four `NotifyAsync` call sites, the
  repository, or the fan-out loop.
- **Zero behavioural change for existing sinks**: a row with no/`http` channel keeps
  POSTing the current `{ @event, timestamp, alert }` body with HMAC signing exactly as
  today.

A concrete target used in the option evaluation below: an operator pastes a Slack
**incoming-webhook URL**, sets `channel = "slack"`, and PAL renders an alert as a Slack
message card colored by severity — without the operator standing up a translation relay.

---

## 3. Design Options (the channel abstraction)

The core decision is **how a sink selects its delivery mechanism**, and how much of the
transport actually changes. Slack and Teams incoming webhooks are themselves
`HTTP POST <url>` with a JSON body — only the **body shape** differs from today's payload.
Email is the outlier: SMTP, not HTTP, so it forces a genuine transport branch. The three
options trade backward-compat simplicity against extensibility.

### Option A — `channel` discriminator + `IChannelNotifier` per channel (full abstraction)

Add a `channel` field to the sink (`http` | `slack` | `teams` | `email`, default `http`).
Introduce an `IChannelNotifier` resolved by channel id; both payload formatting **and**
transport move behind it. `NotifyAsync` resolves a notifier per sink and delegates.

```csharp
public interface IChannelNotifier
{
    string Channel { get; }                                  // "http" | "slack" | "teams" | "email"
    Task<DeliveryResult> SendAsync(WebhookSinkDto sink, NotificationContext ctx, CancellationToken ct);
}

// NotificationContext carries the immutable inputs the formatter needs:
//   string Event, AlertDto Alert, DateTimeOffset Timestamp.
```

**Pros**:
- Cleanest separation: each channel owns both its body shape and its transport. Email's
  SMTP path and Slack's HTTP-relay path are siblings, not special cases.
- The fan-out loop becomes `notifier = _resolver.For(sink.Channel); await notifier.SendAsync(...)`
  — one polymorphic call replaces the `DeliverAsync` switch.
- HMAC signing, content-type, and `X-PAL-Signature` collapse into the `http` notifier where
  they belong; other channels never see them.

**Cons**:
- Largest first commit: every existing line in `DeliverAsync`/`BuildPayload` is relocated
  into an `HttpChannelNotifier`, and `TestAsync`'s `int? status` return value (HTTP status)
  must generalize to a `DeliveryResult` (some channels have no HTTP status — see §6).
- More moving parts to land before *any* new channel works.

**Verdict**: The correct end state, but heavier than necessary for the first useful
increment (Slack/Teams are HTTP under the hood).

### Option B — `channel` swaps the **payload formatter** only; transport stays HTTP except email (recommended first stage)

Add the same `channel` field, but in stage one the only thing `channel` selects is an
`IChannelPayloadFormatter`. Transport stays the existing single HTTP POST for `http`,
`slack`, and `teams` — because all three are `POST <url>` with a JSON body. Email is
explicitly **deferred to stage two**, where it adds the one real transport branch (SMTP).

```csharp
public interface IChannelPayloadFormatter
{
    string Channel { get; }                              // "http" | "slack" | "teams"
    bool SignsPayload { get; }                            // only "http" → true
    byte[] BuildBody(string @event, AlertDto alert, DateTimeOffset timestamp);
}
```

`DeliverAsync` changes minimally: pick the formatter by `sink.Channel`, build the body,
and apply HMAC signing only when `formatter.SignsPayload` (i.e. the `http` channel):

```csharp
var formatter = _formatters[sink.Channel];          // keyed lookup, default "http"
var body = formatter.BuildBody(@event, alert, DateTimeOffset.UtcNow);
using var req = new HttpRequestMessage(HttpMethod.Post, sink.Url) { Content = new ByteArrayContent(body) };
req.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json; charset=utf-8");
if (formatter.SignsPayload && !string.IsNullOrEmpty(sink.Secret)) { /* existing HMAC block */ }
```

**Pros**:
- Smallest change that ships real value: Slack + Teams land by adding two formatters and a
  keyed dictionary. `NotifyAsync`'s loop, error isolation, and `TestAsync` are untouched.
- Backward-compat is trivial: `http` formatter reproduces today's exact body, so existing
  sinks are byte-identical.
- The `BuildPayload`-once optimization in `NotifyAsync` (`:34`) must move *inside* the loop
  (different sinks → different bodies), but that is a one-line relocation.

**Cons**:
- Email cannot be expressed as a formatter — it has no HTTP request — so stage one ships
  three of four channels and stage two adds the transport seam.
- Does not yet generalize transport; when email arrives, `DeliverAsync` grows an
  `if (sink.Channel == "email")` branch (or graduates to Option A).

**Verdict**: The right **first** stage. It is the cheapest path to Slack/Teams and it
leaves a clean upgrade to Option A when email's SMTP transport is added.

### Option C — full transport plugin registry (`IChannelTransport`, DI-discovered)

Generalize beyond named channels to a DI-discovered set of transports
(`IEnumerable<IChannelTransport>`), each declaring the channel(s) it serves, mirroring the
collector-extensibility Option B/C registry pattern. Operators (or third parties) could
register PagerDuty, OpsGenie, SMS, etc. without touching core code.

**Pros**:
- Most extensible; new channels are pure additions (one class + one DI registration).
- Aligns with the direction sketched for collectors
  (`docs/architecture/design/collector-extensibility.md` §3) — one consistent extensibility
  story across the codebase.

**Cons**:
- Heaviest. Requires a channel-id ↔ transport resolution contract, a config-binding story
  per transport, and a versioned interface if out-of-tree plugins are ever a goal.
- Premature: there are exactly four target channels and no evidence of a third-party
  channel requirement yet. Over-abstracting the dispatcher before the second SMTP channel
  even exists risks designing the wrong seam.

**Verdict**: The aspirational end state if community-contributed channels become a product
goal, but out of scope now. See Non-Goals §9.

### Recommendation: **B → A staging**

Ship **Option B first** (payload formatters for `http`/`slack`/`teams`; `http` is the
backward-compat default), then **graduate to Option A** when email lands — at which point
the formatter interface absorbs into `IChannelNotifier` and the SMTP transport becomes the
first non-HTTP notifier. Keep **Option C explicitly deferred** to a future ADR if/when a
third-party channel model is required. The channel-id vocabulary stays fixed at
`http` / `slack` / `teams` / `email` across all stages so the migration, the API request
shape, and the formatters/notifiers always agree.

**Needs maintainer decision**: whether to land stage one (B) and stage two (A→email) as two
separate implementation plans, or as one larger plan that introduces `IChannelNotifier`
from the start and treats Slack/Teams as HTTP notifiers (skipping the intermediate
formatter interface). The former is lower-risk per commit; the latter avoids an
interface rename.

---

## 4. Channel id vocabulary

| `channel` | Transport | Body shape | Signing | New config |
|-----------|-----------|------------|---------|------------|
| `http` (default) | HTTP POST `sink.Url` | `{ @event, timestamp, alert }` (today) | HMAC `X-PAL-Signature` when `sink.Secret` set | none |
| `slack` | HTTP POST `sink.Url` (incoming-webhook URL) | Slack message JSON (attachments) | none | none (URL is the secret) |
| `teams` | HTTP POST `sink.Url` (incoming-webhook URL) | Teams MessageCard / Adaptive Card JSON | none | none (URL is the secret) |
| `email` | SMTP | text/HTML body | n/a | SMTP host/port/from + recipients (see §5) |

For `slack`/`teams` the `sink.Url` **is** the credential (Slack/Teams mint a secret
incoming-webhook URL), so `sink.Secret` is unused for those channels. For `email` there is
no per-sink URL; the recipient list and SMTP settings replace it (see §5).

---

## 5. Per-channel payload mapping + config/secrets

The shared inputs are the event name and `AlertDto` (`Dtos.cs:197-215`): `Title`,
`Severity` (`critical`/`warning`/`informational` — note PAL's tri-state status per
`CLAUDE.md`, no numeric score), `Category`, `Status`, `RuleId`, `TriggeringJobId`,
`LatestJobId`, `TriggeredAt`. A shared severity→color map keeps Slack and Teams consistent:

| Severity | Color (hex) |
|----------|-------------|
| `critical` | `#d93025` (red) |
| `warning` | `#f9ab00` (amber) |
| `informational` | `#1a73e8` (blue) |

### 5a. Slack (incoming webhook)

Slack incoming webhooks accept a JSON body with `attachments[]`, each having a `color`
sidebar and `fields[]`. PAL maps:

- `attachments[0].title` ← `alert.Title`
- `attachments[0].color` ← severity→color map above
- `attachments[0].fields` ← `[{Category}, {Status}, {Severity}, {Rule: alert.RuleId}, {Job: alert.LatestJobId}]`
- `attachments[0].ts` ← `alert.TriggeredAt` (unix seconds)

```json
{
  "attachments": [{
    "color": "#d93025",
    "title": "High processor time on WEB-01",
    "fields": [
      { "title": "Severity", "value": "critical", "short": true },
      { "title": "Category", "value": "cpu", "short": true },
      { "title": "Status", "value": "open", "short": true },
      { "title": "Rule", "value": "cpu.sustained_high", "short": true }
    ],
    "ts": 1760000000
  }]
}
```

**Config / secrets**: the incoming-webhook URL is the only credential and is stored in
the existing `sink.Url` column (already plaintext, already flagged in `Webhooks.razor:9`).
No new config. `sink.Secret`/HMAC is unused for Slack.

### 5b. Microsoft Teams (incoming webhook)

Teams connectors accept a legacy `MessageCard` (`@type`/`@context`/`themeColor`/`sections`)
or an Adaptive Card. PAL maps to `MessageCard` for breadth:

- `themeColor` ← severity→color (hex without `#`)
- `summary` / `title` ← `alert.Title`
- `sections[0].facts` ← `[{Severity}, {Category}, {Status}, {Rule}, {Job}]`

```json
{
  "@type": "MessageCard",
  "@context": "http://schema.org/extensions",
  "themeColor": "d93025",
  "summary": "High processor time on WEB-01",
  "sections": [{
    "activityTitle": "High processor time on WEB-01",
    "facts": [
      { "name": "Severity", "value": "critical" },
      { "name": "Category", "value": "cpu" },
      { "name": "Status",   "value": "open" },
      { "name": "Rule",     "value": "cpu.sustained_high" }
    ]
  }]
}
```

**Config / secrets**: same as Slack — the connector URL lives in `sink.Url`, no new
fields, no per-sink secret.

### 5c. Email (SMTP)

Email is the only channel needing new configuration, because there is no per-sink URL and
SMTP requires server settings shared across all email sinks.

- **Per-sink** (on the sink row): a recipient list. Reuse the existing comma-joined-string
  pattern already used for `Events` (`WebhookSinkEntity.cs:11`) rather than inventing a new
  column type — e.g. a `Recipients` text column, or repurpose `Url` to hold the recipient
  list for `email` sinks (a maintainer decision; a dedicated column is cleaner).
- **Server-wide SMTP** (host, port, from-address, and credentials): these are *not*
  per-sink and must live in configuration, **never** on the sink row.

**Where SMTP secrets live** — reuse the project's existing config mechanism exactly as the
Postgres connection string does today. `appsettings.json` already binds sections via
`builder.Configuration[...]` / `GetConnectionString(...)` (`Program.cs:40,102,167`), and
the README documents env-var overrides with the `Section__Key` convention
(`README.md:145`: `$env:ConnectionStrings__Postgres = ...`) plus `dotnet user-secrets` for
local dev (`README.md:128-132`). An SMTP section would follow the same pattern:

```jsonc
// appsettings.json — NON-SECRET defaults only; password injected via env/user-secrets
"Smtp": {
  "Host": "smtp.example.com",
  "Port": 587,
  "From": "pal-alerts@example.com",
  "UseStartTls": true
  // "Password": NEVER committed — set via Smtp__Password env var or `dotnet user-secrets set "Smtp:Password" ...`
}
```

No secret value is committed: the password is supplied at runtime through
`Smtp__Password` (env) or user-secrets, mirroring how `POSTGRES_PASSWORD` is handled in
the README. **The `webhook_sinks` table never gains an SMTP-password column.**

**Needs maintainer decision**: whether email recipients are a per-sink column or a
reuse of `Url`, and whether email uses `System.Net.Mail.SmtpClient` or a maintained
library (e.g. MailKit) given `SmtpClient` is documented as obsolete-for-new-code by
Microsoft.

---

## 6. Migration & backward compatibility

### Schema change

Add **one nullable column** `channel` to `webhook_sinks`, defaulting existing rows to
`http`. This is the same shape as the precedent `Phase4AlertPolicyColumn` migration
(`Migrations/20260428030045_Phase4AlertPolicyColumn.cs`), which added a nullable `text`
column to `alerts`:

```csharp
// future migration Up() — modeled on Phase4AlertPolicyColumn
migrationBuilder.AddColumn<string>(
    name: "channel",
    table: "webhook_sinks",
    type: "text",
    nullable: true,
    defaultValue: "http");      // existing rows backfill to "http"
```

The migration is **future implementation work** — this spike does **not** run `dotnet-ef`.
`WebhookSinkEntity` gains `public string Channel { get; set; } = "http";` and
`WebhookSinkDto` gains `public string Channel { get; init; } = "http";`, with the
repository's `ToDto`/`CreateAsync`/`UpdateAsync` (`WebhookSinkRepository.cs:31-88`) mapping
it through. EF treats absent/null as `http`.

### API / UI request changes

- `CreateWebhookRequest`/`UpdateWebhookRequest` (`WebhookEndpoints.cs:99-100`) gain an
  **optional** `string? Channel`, defaulting to `http` when absent — existing API clients
  keep working unchanged.
- `ValidateRequest` (`WebhookEndpoints.cs:81-90`) becomes channel-aware: the
  absolute-http(s)-URL rule (`:85-87`) applies to `http`/`slack`/`teams` (whose `Url` is a
  POST endpoint) but **not** to `email` (which has no URL). Email validation instead
  requires at least one recipient and relies on server SMTP config being present.
- `Webhooks.razor` gains a channel `<select>` (default `http`) and conditionally
  shows URL vs. recipient inputs. The existing plaintext-secret warning (`:9`) stays.

### Test path per channel

`TestAsync` (`NotificationService.cs:49-63`) must keep working for every channel. It returns
`int?` today (an HTTP status). For `http`/`slack`/`teams` the HTTP status is still
meaningful. For `email` there is no HTTP status, so the return type must generalize to a
`DeliveryResult { bool Delivered; int? HttpStatus; string? Error }` (a small,
back-compatible change to `INotificationService.TestAsync` and the
`POST /webhooks/{id}/test` handler `WebhookEndpoints.cs:58-78`, which currently branches on
`status is >= 200 and < 300`).

### Call sites that must keep compiling

| Call site | File | Why it is touched |
|-----------|------|-------------------|
| `NotifyAsync` fan-out loop | `NotificationService.cs:29-47` | `BuildPayload`-once moves inside loop; per-sink formatter/notifier resolution |
| `TestAsync` | `NotificationService.cs:49-63` | return type generalizes to `DeliveryResult` |
| 4× `NotifyAsync(...)` | `AlertService.cs:53,70,91,103` | **no change** — signature is unchanged |
| `ListEnabledForEventAsync` | `WebhookSinkRepository.cs:68-81` | **no change** — channel does not affect event subscription |
| CRUD endpoints + validator | `WebhookEndpoints.cs:24-90` | optional `Channel` field + channel-aware validation |
| Blazor management page | `Webhooks.razor` | channel `<select>` + conditional inputs |
| DI registration | `Program.cs:112-120` | register formatters/notifiers (+ SMTP options binding for email) |

### Recommended ordering

1. **Schema/migration** — add nullable `channel` column, default existing rows to `http`;
   thread `Channel` through entity → DTO → repository.
2. **Notifier abstraction** — introduce `IChannelPayloadFormatter` (Option B), keyed by
   `channel`, with the `http` formatter reproducing today's exact body + HMAC.
3. **Endpoints / UI** — optional `Channel` request field, channel-aware validation, Blazor
   `<select>`.
4. **Per-channel notifiers** — Slack then Teams formatters (stage one); then email's SMTP
   transport (stage two), at which point the formatter interface graduates to
   `IChannelNotifier` (Option A) and `DeliveryResult` lands.

This ordering keeps every commit green and ships Slack/Teams before email's larger SMTP
surface.

---

## 7. Recommended First Step

A scoped outline for the first follow-up implementation plan (not executed here).

**Title**: "Add `channel` discriminator + Slack/Teams payload formatters (Option B stage one)"

**Scope**:

1. **Migration**: add nullable `channel text` column to `webhook_sinks`, `defaultValue:
   "http"`, modeled on `Phase4AlertPolicyColumn`. Add `Channel` to `WebhookSinkEntity` and
   `WebhookSinkDto` (default `"http"`); map it through `WebhookSinkRepository`.
2. **Formatter seam**: introduce `IChannelPayloadFormatter` with `Channel`, `SignsPayload`,
   `BuildBody`. Implement `HttpChannelPayloadFormatter` that reproduces the current
   `{ @event, timestamp, alert }` body **byte-for-byte** (lock with a golden test so
   existing sinks are provably unchanged). Refactor `NotificationService.DeliverAsync` to
   resolve the formatter by `sink.Channel` and apply HMAC only when `SignsPayload`.
3. **Slack + Teams formatters**: `SlackPayloadFormatter` and `TeamsPayloadFormatter` per the
   mappings in §5a/§5b, sharing a severity→color map.
4. **Endpoints + UI**: optional `Channel` on create/update requests (default `http`);
   `ValidateRequest` skips the URL rule for `email` (no email yet, but make the seam);
   `Webhooks.razor` channel `<select>`.
5. **DI**: register the three formatters keyed by channel in `Program.cs:112-120`.
6. **Tests**: formatter unit tests with golden JSON for Slack/Teams; a back-compat test
   asserting the `http` body is identical to the pre-change `BuildPayload` output;
   `TestAsync` per channel.

**What this plan does NOT include**:
- Email / SMTP transport (stage two — adds `IChannelNotifier` + `DeliveryResult`).
- The webhook CLI parity gap.
- Retry/backoff or rate limiting.
- A DI-discovered transport plugin registry (Option C).

**Estimated effort**: M (the formatter refactor + golden back-compat lock is the careful
part; Slack/Teams bodies are controlled JSON shapes).

---

## 8. Open Questions

### 8a. Retry / backoff policy per channel

**Current evidence**: delivery is fire-and-log — `NotifyAsync` catches and logs per sink
(`NotificationService.cs:41-45`) with no retry, no dead-letter, and a fixed 10s HTTP
timeout (`Program.cs:118-119`). Slack/Teams rate-limit (HTTP 429 with `Retry-After`); SMTP
has its own transient-failure semantics.
**Needs maintainer decision** on whether multi-channel ships with the existing
fire-and-log behavior or introduces a retry policy (e.g. Polly) — and whether retry is
per-channel. Recommend keeping fire-and-log for the first stage and treating retry as its
own plan, since it touches the worker path, not just notification.

### 8b. Rate limiting

**Current evidence**: no throttling exists; every matching sink is hit once per event. A
storm of `alert.created` events fans out unbounded. Slack/Teams enforce per-webhook rate
limits and will 429.
**Needs maintainer decision** on whether to add per-sink coalescing/throttling. Out of
scope for the channel abstraction itself, but worth flagging before Slack users hit 429s.

### 8c. Add the missing webhook CLI commands now, or later?

**Current evidence**: there is no `RemoteWebhook*` command under
`dotnet/src/Pal.Cli/Commands/Remote/`; sink management is API + Blazor only. Adding
`channel` to the API without CLI parity widens that gap.
**Needs maintainer decision**: ship CLI parity as part of the channel work, or keep it a
separate item. This spike's plan keeps it separate (Non-Goals §9), but the maintainer may
prefer to close the gap and add `channel` to the CLI in one motion.

### 8d. PagerDuty / OpsGenie / SMS as future channels

**Current evidence**: the four target channels are HTTP-relay (`slack`/`teams`) or SMTP
(`email`). PagerDuty/OpsGenie are HTTP-relay with their own Events API JSON; SMS needs a
provider (Twilio etc.).
**Proposed answer**: defer. Once Option A's `IChannelNotifier` exists, PagerDuty/OpsGenie
are "just" new formatters/notifiers. SMS implies an external provider + cost model and is a
larger product decision. Revisit under Option C if a third-party channel model is wanted.

### 8e. `TestAsync` return contract for non-HTTP channels

**Current evidence**: `TestAsync` returns `int?` (HTTP status) and the endpoint branches on
`status is >= 200 and < 300` (`WebhookEndpoints.cs:72`). Email has no HTTP status.
**Proposed answer**: generalize to a `DeliveryResult { bool Delivered; int? HttpStatus;
string? Error }` when email lands (stage two). For stage one (HTTP/Slack/Teams), the
existing `int?` still works, so this can be deferred to the email plan.

---

## 9. Non-Goals

Explicitly out of scope for the notification-channel work; none should be folded in without
a separate ADR:

- **Inbound webhooks**: PAL receiving callbacks from external systems (e.g. Slack
  interactivity, acknowledge-from-Slack). This is a different security and routing problem.
- **Per-user notification preferences**: routing alerts to individual users by identity,
  quiet hours, digests. Sinks today are workspace-scoped destinations, not user
  subscriptions, and this design keeps it that way.
- **A templating DSL**: per-channel bodies are code-built formatters with a fixed mapping
  (§5), not an operator-editable template language. PAL deliberately avoids expression DSLs
  elsewhere (`CLAUDE.md`: declarative comparators, no parser); the same restraint applies
  here.
- **Out-of-tree / plugin channels** (Option C realized): assembly-scanned or
  NuGet-distributed channel transports. Requires a stable versioned interface and a
  distribution story that does not exist in Phase 1. Revisit only if community-contributed
  channels become a product goal.
- **Retry/backoff redesign and rate limiting**: deferred to their own plan (§8a/§8b) —
  they touch the delivery/worker path, not the channel abstraction.
- **Webhook CLI parity**: noted as an asymmetry (`Pal.Cli/Commands/Remote` has no webhook
  command), but designing/adding those commands is a separate item (§8c).
