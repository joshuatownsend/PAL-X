---
title: Code style
description: Conventions enforced by review — naming, layering, error handling, comments.
---

# Code style

C# project conventions, learned by reading the existing code. Reviewers will push back on PRs that violate these.

For project rules that go beyond style (no JS in Phase 1, no expression DSL, etc.), see **[Contributing — When NOT to contribute](index.md#when-not-to-contribute)** and **[`CLAUDE.md`](https://github.com/joshuatownsend/PAL-X/blob/main/CLAUDE.md)** at the repo root.

## Language features

- **Target: .NET 8 / C# 12.** Use modern features (records, primary constructors, pattern matching, collection expressions) where they aid clarity.
- **`sealed` by default on classes** unless designed for inheritance. Most types in PAL-X are `sealed`.
- **`record` for DTOs and value-equality types.** `class` for behavioural types.
- **Nullable reference types are enabled solution-wide.** Resolve nullable warnings; don't disable them.

## Naming

Standard .NET conventions:

| Element | Style |
|---|---|
| Types, methods, properties | `PascalCase` |
| Local variables, parameters | `camelCase` |
| Private fields | `_camelCase` with leading underscore |
| Constants | `PascalCase` (not `SCREAMING_SNAKE`) |
| Async methods | `Suffix...Async` |
| Interfaces | `IPascalCase` |

Database column names are an exception — Postgres uses `snake_case` via `UseSnakeCaseNamingConvention`. EF Core handles the mapping.

Canonical metric IDs are also snake_case (`processor.percent_processor_time`) per **[ADR 0001](../architecture/adr/0001-deviations-from-seed-docs.md)**.

## File organisation

- **One public type per file.** Filename matches the type name. Private nested helpers can share a file with the type they support.
- **Namespace matches directory.** `dotnet/src/Pal.Engine/Rules/RuleEngine.cs` is in `namespace Pal.Engine.Rules`.
- **File-scoped namespaces.** `namespace Foo;` rather than `namespace Foo { … }`.

## Async

- **Use `async`/`await` end-to-end.** Don't block with `.Result` or `.GetAwaiter().GetResult()`.
- **CancellationToken parameters end the method signature.** Plumb them through; don't drop them in middle layers.
- **`Task` for void-returning async methods. `Task<T>` for value-returning. `ValueTask` only with measurement.**

## Layering

The engine doesn't reference surface concerns. From **[Architecture — Layer dependencies](../architecture/index.md#layer-dependencies)**:

- `Pal.Engine` depends on nothing else in the solution.
- `Pal.Application` defines interfaces; `Pal.Persistence` implements them; `Pal.Api` composes both.
- The engine does no I/O beyond reading inputs and writing outputs handed to it. No `HttpClient`, no DbContext.

If you find yourself wanting to add a `Pal.Engine` → `Pal.Application` reference, you're putting surface concerns where they don't belong.

## Error handling

- **Throw exceptions for unexpected failures**, not control flow.
- **Use specific exception types**: `ArgumentException`, `InvalidOperationException`, `FileNotFoundException`. Avoid raw `Exception`.
- **Catch only what you can handle.** A `catch (Exception)` that just logs and rethrows adds little — let it propagate.
- **At public API boundaries**, translate exceptions to result objects (or HTTP problem details) so callers get structured failure modes.

The CLI's exit codes (see **[Reference — Exit codes](../reference/exit-codes.md)**) are the canonical example: internal exceptions get caught at the top-level command handler and mapped to a specific exit code.

## Comments

Prefer **self-documenting code**. Reach for comments when:

- **WHY is non-obvious.** A hidden constraint, a subtle invariant, a workaround for a specific bug.
- **The reader needs to know something not in the code.** A reference to a spec, an ADR, an issue.

Avoid:

- **Comments that restate the code.** `// Iterate over the findings` above `foreach (var f in findings)` is noise.
- **Stale comments.** Keep comments accurate or delete them.
- **Multi-paragraph docstrings on private methods.** Save the writing for public API surface.

XML doc comments (`/// <summary>`) are appropriate for public types and methods that will appear in the .NET API reference. Today's coverage is patchy and will improve incrementally with Step 10 of the docs plan.

## Logging

- **Use `ILogger<T>`.** Inject; don't construct.
- **Use structured logging.** `{Param}` placeholders, not string interpolation.

```csharp
// Good
_logger.LogInformation("RetentionWorker: purged {Count} jobs", count);

// Bad
_logger.LogInformation($"RetentionWorker: purged {count} jobs");
```

The placeholder form lets log shippers extract `count` as a structured field; the interpolated form bakes it into a string.

- **Level discipline:**
  - `Debug` — fine-grained internal state, only useful when debugging.
  - `Information` — lifecycle events, completed work, decisions taken.
  - `Warning` — unexpected but non-fatal; worth seeing in normal operation.
  - `Error` — failures the caller couldn't recover from.

## Tests

- **xUnit.** `[Fact]` for parameter-less; `[Theory]` with `[InlineData]` for parameterised.
- **FluentAssertions** for non-trivial assertions; `Assert.Equal` for the simple cases.
- **Arrange / Act / Assert** sections, separated by a blank line. No comment headers needed.
- **One assertion per test, ideally.** Multiple assertions are fine when they're verifying related properties of one outcome; split if they're verifying different outcomes.

```csharp
[Fact]
public void RuleEngine_Skips_RuleWhenAppliesWhenFails()
{
    var dataset = MakeDataset(metrics: ["other_metric"]);
    var pack = MakePackWith(rule: new() { AppliesWhen = new() { RequiresAll = ["missing_metric"] } });

    var findings = new RuleEngine().Evaluate(dataset, [pack]);

    findings.Should().BeEmpty();
}
```

## Constraints from project rules

A few non-negotiables, from `CLAUDE.md`:

- **UTF-8 without BOM** for all JSON and HTML artifacts. Use `new UTF8Encoding(false)`. Never bare `Encoding.UTF8` for file writes.
- **Finding sort order is total**: severity desc → category asc → rule_id asc → finding_id asc.
- **`host_context` unknown = informational warning + rule skipped.** Don't fail the run.
- **CLI output naming**: `<input-stem>.pal-report.json` and `<input-stem>.pal-report.html`. Charts go in `<output>/charts/<report-name>-<chart-id>.svg`.
- **No JavaScript in Phase 1.** No `package.json`, no `node_modules`, no JS tooling.
- **Tenant query filter uses `!HasValue || == GetValueOrDefault()`**, not `!= null`. Changing this triggers an EF parameter-extraction crash.

## Editor configuration

There's no committed `.editorconfig` — defaults from your IDE are fine. If you're using JetBrains Rider, its built-in C# defaults match the codebase. VS Code with C# Dev Kit follows OmniSharp defaults; also compatible.

Format-on-save is recommended but not required. Reviewers will note egregious whitespace inconsistencies.

## Related

- **[Architecture — Project map](../architecture/index.md#project-map)** — the layering this style applies to.
- **[`CLAUDE.md`](https://github.com/joshuatownsend/PAL-X/blob/main/CLAUDE.md)** — canonical project conventions.
- **[Testing](testing.md)** — how the test suite reflects these conventions.
