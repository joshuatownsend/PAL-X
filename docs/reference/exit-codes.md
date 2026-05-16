---
title: Exit codes
description: Every exit code emitted by the pal CLI, mapped to its meaning and where it can fire.
---

# Exit codes

The `pal` CLI emits one of seven exit codes. All command pages link back here for tie-breaking when the meaning is ambiguous.

The authoritative definition lives in `dotnet/src/Pal.Cli/ExitCodes.cs`.

## Codes

| Code | Constant | Meaning |
|---|---|---|
| `0` | `Success` | Command completed normally. Findings may still be present in the output — exit code `0` means *the run succeeded*, not *the system is healthy*. Read the report's `summary.overall_status` to distinguish. |
| `1` | `GeneralFailure` | Anything else: unhandled exception, network failure on `pal remote *`, output write failure, unrecognised verb. This is the catch-all. |
| `2` | `InvalidArguments` | The CLI parser rejected the invocation — missing required flag, invalid value, mutually-exclusive flags both supplied. Re-run with `--help` to see the contract. |
| `3` | `InputCollectorFailure` | The input collector (CSV or BLG) couldn't read or parse the input file. Common causes: missing file, malformed header, BLG on a non-Windows host, file currently being written. |
| `4` | `PackValidationFailure` | A loaded pack failed schema validation. `pal validate-pack` returns this; `pal analyze` also returns this if `--strict` or signature enforcement rejects a pack at load time. |
| `5` | `AnalysisExecutionFailure` | The rule engine encountered a fatal error during evaluation — corrupt dataset, an internal invariant violation, etc. **This is not the code for "findings were found."** Findings are an expected outcome of analysis, not a failure. |
| `6` | `ReportGenerationFailure` | Analysis completed but writing the report (JSON, HTML, Markdown, or chart SVG) failed — most often a permissions or disk-space issue at `--output`. |

## Which commands emit which codes

| Command | `0` | `1` | `2` | `3` | `4` | `5` | `6` |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| `pal analyze` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `pal validate-pack` | ✓ | ✓ | ✓ | — | ✓ | — | — |
| `pal inspect-dataset` | ✓ | ✓ | ✓ | ✓ | — | — | — |
| `pal list-packs` | ✓ | ✓ | — | — | — | — | — |
| `pal packs sign` | ✓ | ✓ | ✓ | — | — | — | — |
| `pal remote <verb>` | ✓ | ✓ | ✓ | — | ✓¹ | — | — |

¹ `pal remote validate-pack` is the only remote subcommand that returns `4`. All other remote subcommands return `1` for HTTP failures, not `5` — the analysis ran on the server, not the CLI.

## "I got a finding, but the exit code is 0 — is that right?"

Yes. PAL-X separates **operational success** (the run completed) from **system health** (what the run found). Non-zero exit codes are reserved for cases where PAL-X itself couldn't do its job.

If you want a non-zero exit when findings exceed a severity threshold, wrap the CLI in your own gate — read `summary.overall_status` from the JSON report and choose your own exit code. A `jq` one-liner:

```bash
pal analyze --input cpu.csv --output out --pack-dir packs/thresholds
status=$(jq -r '.summary.overall_status' out/cpu.pal-report.json)
case "$status" in
  critical) exit 2 ;;
  warning)  exit 1 ;;
  healthy)  exit 0 ;;
esac
```

This pattern is intentionally external — PAL-X stays a deterministic reporter; your CI decides what fails the pipeline.

## Related

- **[`pal analyze`](cli/pal-analyze.md)** — the command most likely to surface every exit code.
- **[`pal validate-pack`](cli/pal-validate-pack.md)** — primary source of `4`.
- **[Report schema](report-schema.md)** — `summary.overall_status` is the post-analysis health signal.
- **[Configuration](configuration.md)** — the API side has its own error model (HTTP status codes); the CLI translates remote errors back to `1`.
