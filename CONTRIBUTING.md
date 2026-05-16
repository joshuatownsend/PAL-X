# Contributing to PAL-X

Thanks for your interest in PAL-X. Contributing guidelines and developer setup live in the docs site:

- **[How to contribute](https://joshuatownsend.github.io/PAL-X/contributing/)** — workflow, branching, PR conventions.
- **[Development setup](https://joshuatownsend.github.io/PAL-X/contributing/development-setup.html)** — local environment, prerequisites, build, test.
- **[Testing](https://joshuatownsend.github.io/PAL-X/contributing/testing.html)** — unit vs integration tests, Testcontainers, the BLG-on-CI caveat.
- **[Adding a migration](https://joshuatownsend.github.io/PAL-X/contributing/migrations.html)** — EF Core migration workflow.
- **[Adding an ADR](https://joshuatownsend.github.io/PAL-X/contributing/adding-an-adr.html)** — when and how.
- **[Docs workflow](https://joshuatownsend.github.io/PAL-X/contributing/docs-workflow.html)** — DocFX setup, local preview.
- **[Code style](https://joshuatownsend.github.io/PAL-X/contributing/code-style.html)** — conventions enforced by review.

For source-of-truth project conventions, the canonical reference is **[`CLAUDE.md`](CLAUDE.md)** in the repository root.

## Quick start

```bash
# Build the solution
dotnet build dotnet/Pal.sln -c Release

# Run unit tests (no Docker required)
dotnet test dotnet/Pal.sln -c Release --filter "FullyQualifiedName!~Pal.Api.Tests"

# Run integration tests (requires Docker Desktop)
dotnet test dotnet/tests/Pal.Api.Tests -c Release
```

## License

PAL-X is released under the **[MIT License](LICENSE)**. By contributing, you agree your contributions are licensed under the same terms.
