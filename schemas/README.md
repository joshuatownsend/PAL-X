# PAL Schemas

JSON Schemas for PAL artifacts live in [`../dotnet/schemas/`](../dotnet/schemas/).

This directory exists as a repo-root discovery point for external tooling (editors, CI schema validators, documentation generators).

| Schema | Description |
|--------|-------------|
| [pal.pack.v1.json](../dotnet/schemas/pal.pack.v1.json) | Rule pack definition — declarative conditions, host_context thresholds, recommendations |
| [pal.report.v1.json](../dotnet/schemas/pal.report.v1.json) | Analysis report output — findings, evidence, series index, tri-state status |

Additional schemas (evidence-bundle, baseline, alert, compare) will land with the phases that need them.
