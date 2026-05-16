---
title: Adding an ADR
description: When and how to write an Architecture Decision Record.
---

# Adding an ADR

ADRs document decisions that constrain future work. This page covers when to write one and the mechanics.

For the existing ADRs and the practice background, see **[Architecture — ADR index](../architecture/adr/index.md)**.

## When to write an ADR

Write an ADR when **all three** of these are true:

1. **The decision shapes more code than the diff itself** — adding a new auth scheme, a new schema version, a new storage backend, a new constraint type.
2. **A future contributor needs to know the reasoning to avoid relitigating** — without the ADR, someone six months from now will ask "why did we do it this way?" and have to reconstruct the trade-offs from scratch.
3. **The alternatives considered are non-obvious** — if "do the natural thing" was the only option, an ADR adds little.

If only two are true, a thorough commit message or PR description is enough.

Examples of decisions that warrant an ADR:

- Picking a multi-tenancy model (org / workspace vs flat tenant vs per-instance).
- Picking a signing scheme (RSA-PSS vs Ed25519 vs detached signatures).
- Adding a schema version with breaking changes.
- Choosing a queue (in-process channel vs Postgres LISTEN/NOTIFY vs external broker).

Examples that don't:

- Picking a logging library.
- Renaming a method.
- Adding a new endpoint that follows existing conventions.
- Tactical refactors that preserve external behaviour.

## When NOT to write an ADR

Reread **[Architecture — ADR index — Where ADRs are NOT the answer](../architecture/adr/index.md#where-adrs-are-not-the-answer)** before opening a new one.

ADRs are heavyweight. The cost is mostly in writing — once written, they sit. But if you write five ADRs for tactical choices, future contributors get desensitised and stop reading. Reserve them.

## Format

Match the existing ADRs (`0001` through `0004`). The structure:

```markdown
# ADR <NNNN> — <Title>

**Status:** Proposed | Accepted | Deprecated | Superseded by ADR-####
**Date:** YYYY-MM-DD
**Deciders:** <Names of people who ratified the decision>

## Context

What problem is being solved? What constraints apply? What's the current
state, and what makes it inadequate?

## Decisions

What was chosen. Often split into sub-decisions if the overall decision has
several parts. Be specific — name the API, library, format, parameter.

## Consequences

What changes as a result. What got easier, what got harder, what's now
forbidden, what's now possible. Both positive and negative.

## Alternatives considered

What we didn't pick, and why. Important — without this section, the ADR
reads like "we did the obvious thing"; with it, the reader sees the
choice was deliberate.
```

Each section should have at least one paragraph. The shortest acceptable ADR is one page of prose. If it's shorter, you probably don't need an ADR.

## File and naming

```text
docs/architecture/adr/<NNNN>-<kebab-case-title>.md
```

- `NNNN` is zero-padded to four digits. Use the next available number — read the existing index to find it.
- Kebab-case title matches the H1 in the body.

The existing ADRs:

```text
docs/architecture/adr/
├── 0001-deviations-from-seed-docs.md
├── 0002-declarative-rule-schema.md
├── 0003-pack-signing-format.md
└── 0004-schema-v1.1-rolling-windows.md
```

Your new ADR would be `0005-<title>.md`.

## Workflow

1. **Draft as a PR.** Branch off `main`, write the ADR, open a PR with status `Proposed`.
2. **Discuss in review.** ADR reviews are about the decision, not the prose. Reviewers should challenge the trade-offs, point out alternatives the draft missed, and verify the "context" section accurately represents reality.
3. **Iterate.** Refine the decision based on feedback. Edit the ADR.
4. **Decide.** When the team has reached agreement (or the deciders have ratified), flip status to `Accepted`. Set the `Date:` to the date of ratification.
5. **Merge.** The PR merges with the ADR at `Accepted` status. Never merge a `Proposed` ADR — proposed decisions live in the PR, not in main.

## Updating an ADR

Once `Accepted`, an ADR is **frozen**. Don't edit the body except for typo fixes that don't change meaning.

If the decision changes:

- For a small revision (clarification, new alternative considered): add a postscript at the bottom of the ADR with the date and what's new. Don't rewrite the body.
- For a major revision: write a new ADR that supersedes the old. Update the old one's status to `Superseded by ADR-NNNN`. Link forward.

The history is the value. An ADR that's been edited five times is useless because no one knows what's current — but an ADR superseded by another is informative (the trail of decisions tells a story).

## Linking from code

When you implement the decision documented in an ADR, **link to it from the code**. Comment, XML doc, or README — anywhere a future maintainer will read.

```csharp
// See ADR 0002 — declarative rule schema. No expression DSL.
public sealed class RuleEngine
{
    // …
}
```

And from the ADR back to the implementation:

```markdown
## Consequences

…implemented in `Pal.Engine/Rules/RuleEngine.cs`…
```

Bidirectional references catch drift. If the code changes the contract the ADR documented, both ends are visible.

## Adding to the index

After the ADR merges, update **[Architecture — ADR index](../architecture/adr/index.md)** with a new row in the Accepted ADRs table. Include status, date, and a one-line summary.

Don't add the ADR before merging — the index should match `main`.

## Related

- **[Architecture — ADR index](../architecture/adr/index.md)** — every existing ADR.
- **[ADR 0001](../architecture/adr/0001-deviations-from-seed-docs.md)** — the most-read ADR; a good model for tone and depth.
