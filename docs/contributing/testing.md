---
title: Testing
description: Unit vs integration split, Testcontainers, BLG-on-CI caveat, golden fixtures.
---

# Testing

PAL-X has two test tiers: **unit tests** (no Docker, run anywhere) and **integration tests** (Docker required, run a Postgres container per fixture). Plus golden-fixture tests that verify byte-identical output across runs.

## Test projects

Under `dotnet/tests/`:

| Project | Tier | Notes |
|---|---|---|
| `Pal.Engine.Tests` | unit | Rule engine, statistics, classifier |
| `Pal.Ingestion.Tests` | unit | Collectors. **1 skipped test on non-Windows** (BLG path). |
| `Pal.Packs.Tests` | unit | Loader + validator |
| `Pal.Reporting.Tests` | unit | JSON/HTML/Markdown writers, ScottPlot SVG charts |
| `Pal.Application.Tests` | unit | Analytics, alerts, diagnostics, comparison |
| `Pal.Cli.Tests` | unit | CLI command shape, exit codes |
| `Pal.Api.Tests` | integration | Full request/response with Testcontainers Postgres |

Total: 146 unit tests + ~25 integration tests as of this writing.

## Run the suite

### Unit tests only — works anywhere

```bash
dotnet test dotnet/Pal.sln -c Release --filter "FullyQualifiedName!~Pal.Api.Tests"
```

Runs all six unit projects. ~10 seconds wall-clock. The `--filter` excludes `Pal.Api.Tests` because those need Docker.

Expected output: `Passed: 146, Failed: 0, Skipped: 1`.

### Integration tests only — needs Docker Desktop

```bash
dotnet test dotnet/tests/Pal.Api.Tests -c Release
```

Spins up a `postgres:16-alpine` container per test fixture via Testcontainers. ~30-60 seconds. Excluded from the Windows CI runner; runs on the Linux CI job.

### Everything

```bash
# With Docker Desktop running
dotnet test dotnet/Pal.sln -c Release
```

## The BLG-on-non-Windows skip

`Pal.Ingestion.Tests` has one test that exercises the BLG collector. It's gated:

```csharp
[Fact(Skip = "BLG ingestion requires Windows PDH (issue #41)")]
public void BlgCollector_IngestsCounters() { … }
```

On non-Windows, the test is skipped (counts as one of the "1 skipped" in the output). On Windows CI, it's also skipped pending issue #41 (the workflow setup needed for Windows-PDH tests on the GHA Windows runner). Track via the issue.

Don't unskip locally on Windows without checking — there are sample BLG fixtures under `fixtures/cpu-pressure-blg/` for ad-hoc testing.

## Golden fixtures

The most important shape of test: **byte-identical output**.

`Pal.Cli.Tests` and `Pal.Reporting.Tests` keep golden JSON reports under `fixtures/<scenario>/golden.pal-report.json`. Each test:

1. Runs the analysis pipeline against `fixtures/<scenario>/input.csv` (or `.blg` on Windows).
2. Compares the produced JSON byte-for-byte with `golden.pal-report.json`.
3. Fails on any difference.

The pipeline is deterministic by design (see **[Architecture — Data flow](../architecture/data-flow.md)**), so a golden test failure means **something is wrong**, not that the fixture is stale. To regenerate a golden after an intentional change:

```bash
dotnet run --project dotnet/src/Pal.Cli -- analyze \
  --input fixtures/cpu-pressure/input.csv \
  --output /tmp/regen \
  --pack-dir packs/thresholds \
  --now 2026-05-15T10:23:14Z

cp /tmp/regen/input.pal-report.json fixtures/cpu-pressure/golden.pal-report.json
```

Re-run the suite; the golden test should now pass. **Commit the new golden alongside the code change** so reviewers can see what changed.

The `--now <iso>` flag is critical — without it, `generated_at_utc` differs every run and the byte-comparison fails.

ScottPlot SVG charts are similarly byte-checked. `SvgCanonicalizer` normalises ScottPlot's internal IDs so two renders of the same data are identical; see `Pal.Reporting/Charts/SvgCanonicalizer.cs`.

## Test fixtures

```text
fixtures/
├── broken-pack/           # Malformed pack.yaml — tests validator errors
├── cpu-pressure/          # Synthetic CSV with sustained CPU
├── cpu-pressure-blg/      # Same data, BLG format
├── disk-latency/          # Synthetic CSV with disk pressure
├── healthy-server/        # No findings expected
└── memory-pressure/       # Memory rule fires
```

Each scenario has `input.csv` (or `.blg`) plus `golden.pal-report.json`. Some scenarios also have `host_context.json` sidecars for testing RAM-relative thresholds.

When adding a feature that affects output, add or update a fixture and its golden so the suite covers the new behaviour.

## Multi-tenancy in tests

Test entity factories (`MakeJob`, `MakeUpload`, etc., in `Pal.Api.Tests`) must set `WorkspaceId = DefaultTenant.WorkspaceId`. Forgetting this causes FK violations once DB-level constraints exist.

```csharp
// Correct
var job = new AnalysisJobEntity {
    Id = Guid.NewGuid(),
    WorkspaceId = DefaultTenant.WorkspaceId,
    UploadId = uploadId,
    Status = "queued",
    // …
};

// Wrong — FK violation
var job = new AnalysisJobEntity {
    Id = Guid.NewGuid(),
    // WorkspaceId omitted!
    // …
};
```

See `CLAUDE.md` for the canonical guidance.

## CI pipeline

GitHub Actions runs on every PR:

- **Linux job**: full unit + integration test suite. Docker available; runs `dotnet test dotnet/Pal.sln`.
- **Windows job**: unit tests only (Docker on the hosted Windows runner is unreliable). Runs `dotnet test … --filter "FullyQualifiedName!~Pal.Api.Tests"`.

The workflow enumerates each test project rather than using a solution-level `--filter`. Don't change that — there's a known issue with solution-level filters and the matrix strategy.

The BLG-on-non-Windows skip leaves one test skipped on every run, which is expected.

## Writing a new test

Keep tests in the project corresponding to the code under test:

- A test for `Pal.Engine.Rules.RuleEngine` lives in `Pal.Engine.Tests`.
- A test for an HTTP endpoint lives in `Pal.Api.Tests` (always integration, always Testcontainers).
- A test for a CLI command lives in `Pal.Cli.Tests`.

Use xUnit (`[Fact]` / `[Theory]`) — that's what the existing suite uses. Use `FluentAssertions` if the assertion is non-trivial; `Assert.Equal` for the simple cases.

For golden-fixture tests, the test method is typically a couple of lines:

```csharp
[Fact]
public void Analyzes_CpuPressureFixture_MatchesGolden()
{
    var golden = File.ReadAllText("fixtures/cpu-pressure/golden.pal-report.json");
    var actual = RunPipeline("fixtures/cpu-pressure/input.csv");
    Assert.Equal(golden, actual);
}
```

The pipeline runner lives in test infrastructure; lift it from an existing test.

## Related

- **[Development setup](development-setup.md)** — what to install before running tests.
- **[Adding a migration](migrations.md)** — integration tests catch most schema regressions.
- **[Architecture — Data flow](../architecture/data-flow.md)** — determinism is why golden fixtures work.
