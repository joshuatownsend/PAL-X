---
name: validate-packs
description: Validate all three production rule packs (windows-core, iis-core, sql-host-core) against the pal.pack.v1 schema
disable-model-invocation: true
---

Run `pal validate-pack` against all three production packs and report results.

```bash
dotnet run --project dotnet/src/Pal.Cli -c Release -- validate-pack --path packs/thresholds/windows-core
dotnet run --project dotnet/src/Pal.Cli -c Release -- validate-pack --path packs/thresholds/iis-core
dotnet run --project dotnet/src/Pal.Cli -c Release -- validate-pack --path packs/thresholds/sql-host-core
```

Report the exit code and any validation errors for each pack. If all three exit 0, confirm all packs are valid. If any exit non-zero, show the error output and stop.
