---
title: License
description: PAL-X is MIT-licensed. Full text plus what you can do with it.
---

# License

PAL-X is released under the **MIT License**.

## What this means

You can:

- **Use** PAL-X for any purpose, commercial or non-commercial.
- **Copy** it.
- **Modify** it.
- **Distribute** modified or unmodified copies.
- **Sublicense** it.
- **Sell** it (it's already free, but you can charge for distribution if you want).

You must:

- **Include the copyright notice and license text** in any substantial portion you redistribute.

You cannot:

- Hold the authors liable for what happens when you use it. The license disclaims warranties — "AS IS, no guarantees."

## Full text

```text
MIT License

Copyright (c) 2026 Josh Townsend

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

This text matches `LICENSE` at the repository root. If the two ever disagree, the root file wins — it's what GitHub displays, what package metadata references, and what the licence-detection tools read.

## Contributions

By contributing code, documentation, or other content to PAL-X, you agree that your contributions are licensed under the same MIT terms. There is no separate Contributor License Agreement.

## Third-party content

PAL-X depends on third-party libraries through NuGet. Each carries its own licence. The notable ones:

| Library | Licence |
|---|---|
| ASP.NET Core | MIT |
| Entity Framework Core | MIT |
| Npgsql | PostgreSQL License (BSD-style) |
| Spectre.Console.Cli | MIT |
| ScottPlot | MIT |
| YamlDotNet | MIT |
| Testcontainers for .NET | MIT |

All are compatible with MIT distribution. Run `dotnet list package --include-transitive` for the full dependency tree.

The `legacy/pal-v2` submodule is the original PAL v2 PowerShell tool. It carries its own licence; do not redistribute the submodule under PAL-X's MIT licence.

## Why MIT

A few alternatives we considered and didn't pick:

- **Apache 2.0** — more explicit patent grant, more verbose. Reasonable, but MIT's brevity wins for a small project.
- **GPL-3.0** — copyleft. Inappropriate for a tool we want consumed widely, including by closed-source operations teams.
- **"All rights reserved"** — closed source. Inappropriate for a tool we want consumed at all.

MIT is the minimum-friction open-source licence. We want people to use this; MIT removes obstacles.
