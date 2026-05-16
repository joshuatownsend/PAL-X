---
title: Contributing
description: How to contribute to PAL-X — workflow, branching, PR conventions, code of conduct.
---

# Contributing

PAL-X welcomes contributions. This page covers the workflow; subsequent pages drill into specific contribution types (code, docs, ADRs).

## Code of conduct

Be kind. Treat others with respect. Disagree with ideas, not people.

There's no separate CoC document today — the project follows the implicit norm of the broader .NET open source ecosystem.

## Where to start

| Want to... | Start at... |
|---|---|
| Fix a bug | An open issue, or open one describing it before submitting a PR |
| Add a feature | Open an issue first to discuss scope before writing code |
| Author a pack | **[Write a pack](../guides/write-a-pack.md)** — no contribution needed if it's site-specific |
| Improve docs | The page in question — every page has a "Edit on GitHub" link in the DocFX template |
| Add an architecture decision | **[Adding an ADR](adding-an-adr.md)** |
| Author a migration | **[Adding a migration](migrations.md)** |

## Repo structure

Authoritative project conventions live in **[`CLAUDE.md`](https://github.com/joshuatownsend/PAL-X/blob/main/CLAUDE.md)** in the repository root. That file is the source of truth for project rules; this section restates the parts that matter for contributors.

```text
PAL-X/
├── dotnet/                    # All .NET source and tests
│   ├── src/                   # Eight projects (see Architecture index)
│   ├── tests/                 # xUnit test projects
│   ├── schemas/               # JSON Schema files (authoritative)
│   └── Pal.sln                # Solution file
├── packs/thresholds/          # Shipped packs (windows-core, iis-core, sql-host-core)
├── fixtures/                  # Test inputs (CSV, BLG)
├── docs/                      # This documentation site
├── infra/                     # Docker, infra-as-code
├── legacy/                    # Read-only submodule of PAL v2 (do not modify)
├── CLAUDE.md                  # Project conventions
├── LICENSE                    # MIT
└── README.md                  # Quickstart
```

The `legacy/` directory is a git submodule containing the original PAL v2 PowerShell tool. **Do not modify it.** It exists solely to inform port decisions.

## PR workflow

1. **Fork and branch.** Branch off `main` with a descriptive name (`fix/csv-encoding`, `feat/alert-suppression`, `docs/clarify-baselines`).
2. **Make small commits.** One logical change per commit. Use Conventional Commits style: `type(scope): summary`.
3. **Run the test suite before pushing.** See **[Testing](testing.md)** for the unit-vs-integration split.
4. **Open a PR.** Include:
   - **Summary** — what changed and why.
   - **Test plan** — what you ran to verify.
   - Reference to any issue this closes.
5. **Wait for CI.** GitHub Actions runs unit tests on Windows; integration tests require Docker and are excluded from the Windows runner.
6. **Iterate on review.** Squash-merge is the standard merge style.

## Commit message conventions

Conventional Commits style:

```text
feat(engine): rolling-window aggregations for v1.1 schema
fix(api): correctly resolve workspace id from route in /tokens endpoint
docs(site): clarify the 3-of-5 alert escalation policy
test(cli): add golden fixture for BLG ingestion
chore(deps): bump Microsoft.AspNetCore to 8.0.5
```

Common scopes: `engine`, `ingestion`, `packs`, `reporting`, `api`, `cli`, `persistence`, `site`, `deps`.

The PR title becomes the squashed commit message, so make the title meaningful.

## When NOT to contribute

There are a few things the project deliberately doesn't accept:

- **New language runtimes.** Phase 1 is .NET-only. No JavaScript, TypeScript, Node — the docs site uses DocFX (.NET) precisely to avoid that toolchain. See [`CLAUDE.md`](https://github.com/joshuatownsend/PAL-X/blob/main/CLAUDE.md) for the policy.
- **Expression DSL features.** Rule conditions are declarative by design. If you want richer expressivity, the answer is "write the rule differently" — see **[ADR 0002](../architecture/adr/0002-declarative-rule-schema.md)**.
- **Numeric health scores.** Tri-state status only. See **[ADR 0001](../architecture/adr/0001-deviations-from-seed-docs.md)**.
- **Black-box inference.** Diagnostics and analytics cite their evidence; no opaque models. See **[Concepts — Analytics surfaces](../concepts/analytics-surfaces.md)**.

Proposing changes that violate these is fine — but expect an ADR-level discussion before code.

## License

PAL-X is **[MIT-licensed](../license.md)**. By contributing, you agree your contributions are MIT-licensed under the same terms.

## Pages in this section

- **[Development setup](development-setup.md)** — local prerequisites and getting the build green.
- **[Testing](testing.md)** — unit vs integration, Testcontainers, BLG caveats.
- **[Adding a migration](migrations.md)** — EF Core workflow with the Windows PATH quirk.
- **[Adding an ADR](adding-an-adr.md)** — when, how, what to write.
- **[Docs workflow](docs-workflow.md)** — DocFX setup and local preview.
- **[Code style](code-style.md)** — conventions enforced by review.
