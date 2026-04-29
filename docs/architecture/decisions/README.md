# Architecture Decision Records

**Audience:** Architects, Developers, and contributors (human or AI) reviewing or changing core patterns

This folder collects short, focused records of specific design decisions in LumaCore. Where the rest of [`docs/architecture/`](../README.md) explains *how the system works* in tutorial form, decision records explain **why a particular pattern was chosen and what was deliberately rejected**.

---

## When to use what

| If you want to … | Read |
|---|---|
| Understand the overall architecture and the pillars it stands on | [Architecture Overview](../README.md), [Design Principles](../principles.md) |
| Learn how a specific feature is built | [Feature Pattern](../feature-pattern.md), the relevant `docs/features/` page |
| Understand how the codebase is laid out | [Project Structure](../project-structure.md) |
| **Know why pattern X was chosen over the obvious alternative Y** | An ADR in this folder |

ADRs are intentionally short — one printed page is the target. They are not tutorials; they are the receipt for a decision.

---

## Format

Each record follows a lightweight [MADR](https://adr.github.io/madr/) variant:

```
# ADR-NNNN — Title
**Status:** Accepted · YYYY-MM-DD

## Context
What problem are we solving? What forces are at play?

## Decision
What did we choose?

## Consequences
What follows from this — both the upside and the price we pay.

## Alternatives Considered
What did we look at and reject? Why?

## See also
Code paths, tests, and related ADRs.
```

If a section has nothing meaningful to say, it is omitted rather than padded.

---

## What belongs in each section

The template above is intentionally short. The bullet lists below define what each section is *for*, so ADRs stay comparable and don't drift into tutorials, API references, or pro/contra wish-lists.

**Context**

- States the *problem*, not the solution. The reader should understand what is being decided before the Decision section names a chosen approach.
- Describes what the BCL or the existing codebase already offers, and where it falls short.
- Closes with an explicit "*The forces that shape the decision*" block of 3–6 bullets: hard requirements, constraints, non-goals.
- Does not name types from *our* solution. ("We need an `AsyncManualResetEvent`" is a Decision-section claim, not a Context one.)

**Decision**

- Describes the chosen design at the architecture level: mechanisms, responsibilities, and the contracts between them.
- May reference type names from the chosen design (`LifecycleManagement`, `AsyncManualResetEvent`, …).
- **Does not** show method signatures, parameter lists, event signatures, or code blocks. Those age fast; designs do not. Detail of that kind belongs in the XML documentation on the type itself.
- Names deliberate hardness ("nesting is forbidden", "subscribers cannot prevent termination") openly — these are part of the decision, not footnotes.

**Consequences**

- Two sub-sections, **Upside** and **Downside**, both as bullet lists.
- Each bullet explains *why* something is a benefit or a cost — not just *that* it is.
- Downsides are honest: known footguns, maintenance burden, API surface, performance assumptions, residual misuse risk.
- If nothing here hurts, the decision probably doesn't deserve an ADR.

**Alternatives Considered**

- At least 2–3 alternatives, each with its own `### Heading`.
- Every alternative answers two questions: **(a)** what would it look like, and **(b)** why was it rejected. The rejection ties back to a concrete *force* listed in the Context section.
- "Too complex" or "we didn't like it" are not rejection reasons.

**See also**

- **Implementation** — repository-relative paths to the central types/files.
- **Tests** — path or glob to the relevant test files.
- **Related** — links to other ADRs, each with a short parenthetical that explains the relationship (*"primary consumer of these primitives"*, *"escalation path on failed compensating shutdown"*).

---

## Lifecycle

- **Accepted** — current decision, in effect.
- **Superseded by ADR-NNNN** — replaced; kept on file for history. The superseding ADR explains the change.
- **Deprecated** — no longer applies, but no replacement exists yet.

ADRs are not silently rewritten when reality changes. If a decision is revised, a new ADR is added and the old one is marked superseded with a back-link. This preserves the audit trail.

---

## Index

| ADR | Title | Status |
|---|---|---|
| [ADR-0001](0001-lifecycle-management-as-state-machine.md) | `LifecycleManagement` as state-machine for initialize/shutdown | Accepted |
| [ADR-0002](0002-custom-async-primitives.md) | Custom async primitives (`AsyncManualResetEvent`, `AsyncWaitQueue`) | Accepted |
| [ADR-0003](0003-execution-stage-monitor-as-ambient-diagnostic.md) | `ExecutionStageMonitor` as ambient diagnostic API | Accepted |
| [ADR-0004](0004-failfast-cooperative-subscribers.md) | `FailFast` with cooperative subscribers | Accepted |

---

## What is *not* in scope

ADRs are reserved for decisions that are non-obvious, costly to reverse, or otherwise prone to being "corrected" by future contributors who lack the original context. We deliberately do **not** create ADRs for:

- Tooling configuration with a foreseeable expiry date (analyzer severity downgrades, coverage thresholds during ramp-up). Those live with the configuration itself, e.g. as inline comments in `.editorconfig`.
- Implementation details that the XML documentation already covers adequately.
- Decisions that follow directly from a higher-level decision already recorded.

---

© 2025-2026 LumaCoreTech • MIT License
