---
title: Configure alerts
description: Acknowledge, resolve, and snooze alerts; understand the 3-of-5 escalation policy.
---

# Configure alerts

Goal: triage open alerts emitted by the policy evaluator. There is no "create an alert" action — alerts are emitted from findings on completed jobs.

For the lifecycle model, see **[Concepts — Alerting and notification](../concepts/alerting-and-notification.md)**. For the API shape, see **[HTTP API — Alerts](../reference/http-api/alerts.md)**.

## List open alerts

```bash
pal remote alerts list

# Filter by status
pal remote alerts list --status open

# Filter by severity
pal remote alerts list --severity critical
```

Each row carries an id, title, severity, status, rule_id, job_id, and timestamps. The CLI sorts by `createdAt` desc.

## Acknowledge an alert

When an operator picks up an alert for triage:

```bash
pal remote alerts ack <alertId>
```

This moves the alert from `open` to `acknowledged`. It doesn't mean the underlying problem is fixed — it means a human has seen it and is investigating. Returns `204` from the API; the CLI prints a one-line confirmation.

## Resolve

When the underlying issue is closed:

```bash
pal remote alerts resolve <alertId>

# With a note for the audit trail
pal remote alerts resolve <alertId> --note "Recycled the app pool; CPU back to normal."
```

Resolution is final — there's no `unresolve`. The note is optional but recommended for postmortem context.

## Snooze and un-snooze

To silence an alert until a future time without resolving:

```bash
# Snooze for 4 hours
pal remote alerts snooze <alertId> --duration 4h

# Or until a specific timestamp
pal remote alerts snooze <alertId> --until 2026-05-16T08:00:00Z
```

Constraints enforced by the API:

- `until` must be in the future (a 30-second skew window is tolerated).
- `until` cannot be more than 30 days in the future. This prevents the "I'll fix it later" forever-snooze pattern.

To clear a snooze (return to active state):

```bash
pal remote alerts unsnooze <alertId>
```

A snoozed alert can still be acknowledged or resolved — snooze is orthogonal to the lifecycle.

## The 3-of-5 escalation policy

Alerts are emitted by the `PolicyEvaluator` according to a single rule in Phase 4 v1:

- **Any current `critical` finding** → an alert at `critical` severity.
- **A current `warning` finding** that has also fired in 3 or more of the last 5 completed jobs (current run inclusive) → an alert escalated to `critical` severity.
- Otherwise → no alert.

The escalation policy's intent: surface persistent warnings as criticals, while silencing one-off blips. A warning that fires once isn't an alert; a warning that fires 4 out of 5 times *is*, and is escalated.

The policy is hard-coded today. Per-rule mute lists, maintenance windows, and customisable thresholds are not yet implemented.

## Common triage patterns

| Situation | Action |
|---|---|
| Alert from a one-off load test | Resolve with a note explaining it was synthetic |
| Alert during a known maintenance window | Snooze with `--until <end-of-window>` |
| Alert you can't fix today | Acknowledge (don't snooze) — keeps it visible |
| Alert from a noisy rule | Resolve, then consider tightening the rule's threshold (see **[Write a pack](write-a-pack.md)**) |

There's deliberately no "mute this rule forever" CLI action. If a rule is noisy, the right fix is in the pack, not in the alert layer.

## Where alerts come from

Walking back the chain:

1. A schedule (or a manual submit) creates an analysis job.
2. The job completes; the rule engine produces findings.
3. `PolicyEvaluator` runs on the findings; if the policy fires, an alert is created.
4. `NotificationService` sends `alert.created` (or `alert.escalated`) to any subscribed webhook.

If alerts aren't firing when you expect them to:

- **No finding fired** — the rule's threshold wasn't crossed. Check the report.
- **Finding fired but no alert** — only one warning in isolation; doesn't meet the 3-of-5 escalation bar.
- **Alert was created but webhook didn't deliver** — see **[Configure webhooks](configure-webhooks.md)** for the test workflow.

## Related

- **[Concepts — Alerting and notification](../concepts/alerting-and-notification.md)** — the full lifecycle model.
- **[Configure webhooks](configure-webhooks.md)** — delivering alerts to chat or paging.
- **[HTTP API — Alerts](../reference/http-api/alerts.md)** — endpoint shapes and status codes.
- **[CLI — `pal remote alerts`](../reference/cli/pal-remote-alerts.md)** — flag reference.
