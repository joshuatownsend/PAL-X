---
title: Generate a Markdown report
description: Produce a GFM-flavoured Markdown rendering of an analysis run, suitable for PRs, issues, and chatops.
---

# Generate a Markdown report

Goal: get a Markdown version of an analysis report alongside (or instead of) the JSON and HTML outputs. Useful for embedding findings in pull requests, GitHub issues, Slack pastes, or any place HTML rendering isn't available.

## Local — `pal analyze --markdown`

Add `--markdown` to a local analysis:

```bash
pal analyze \
  --input capture.csv \
  --output out \
  --pack-dir packs/thresholds \
  --markdown
```

You get one extra artifact:

```text
out/
├── capture.pal-report.json
├── capture.pal-report.html
└── capture.pal-report.md      ← new
```

## Remote — `?format=markdown`

For analysis runs submitted to the API, request the markdown rendering on the report endpoint:

```bash
curl -s \
  -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5043/api/workspaces/$WS/analysis/$JOB_ID/report?format=markdown" \
  -o report.md
```

The API's `AnalysisWorker` writes the Markdown rendering for **every completed job**, alongside the JSON and HTML. No opt-in needed. The endpoint streams the on-disk file at `<Storage:LocalRoot>/reports/<jobId>/report.md`.

The CLI is different: `pal analyze` only writes Markdown when you pass `--markdown` explicitly. The remote-vs-local asymmetry is intentional — server runs are billed disk, so writing all three formats unconditionally amortises well; CLI runs are short-lived where the user opts in per invocation.

## What the Markdown looks like

GFM tables with one row per finding:

```markdown
# PAL Report — capture.csv

**Status:** ⚠️ warning · 0 critical · 3 warning · 7 informational

## Findings

| Severity | Category | Title | Summary |
|---|---|---|---|
| warning | cpu | Sustained high CPU utilization | Total processor time averaged above 80% for more than 20% of the capture window. |
| warning | memory | Low available physical memory | Available memory dropped below 10% of installed RAM. |
| …       |          |                                |                                                                                  |

## Evidence

### high-cpu-sustained
| Metric | Avg | Max | P95 | Trigger |
|---|---|---|---|---|
| `processor.percent_processor_time` | 87.5 | 99.2 | 95.1 | avg(processor.percent_processor_time) > 80 for >= 20% of samples |

…
```

The Markdown writer (`MarkdownReportWriter` in `Pal.Reporting/Markdown/`) uses the same `JsonReportWriter.WriteInput` shape as the HTML and JSON writers — same data, different rendering.

## Use in a PR

Stash the markdown into a comment on a PR:

```bash
pal analyze --input capture.csv --output out --pack-dir packs/thresholds --markdown

# GitHub CLI to comment on a PR
gh pr comment "$PR_NUMBER" --body-file out/capture.pal-report.md
```

If the report is large (many findings), trim or split before commenting — GitHub's body limit is 65k characters.

## Use in chatops

For Slack / Teams, the same markdown often renders close enough. The key tables (status banner, finding list) survive; expanded evidence sections may render less well depending on the chat client. For consistently good rendering, screenshot the HTML.

## Trade-offs vs JSON and HTML

| | Markdown | JSON | HTML |
|---|---|---|---|
| Human-readable in raw form | ✓ | — | — |
| Embeddable in chat / PRs | ✓ | — | — |
| Machine-readable | partial | ✓ | — |
| Charts | — | — | ✓ (SVG) |
| Self-contained | ✓ | ✓ | ✓ |

For programmatic downstream tooling always use JSON — Markdown is for humans, JSON is for code.

## Related

- **[Analyze a CSV](analyze-csv.md)** — the basic analysis flow.
- **[Reference — Report schema](../reference/report-schema.md)** — what all three formats render from.
- **[HTTP API — Reports](../reference/http-api/reports.md)** — the `?format=markdown` query parameter.
