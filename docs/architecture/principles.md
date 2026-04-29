# Design Principles

**Audience:** Architects and developers who need to understand *why* LumaCore is built the way it is.

LumaCore is opinionated. This document captures the values behind those opinions — short by design, so that when a decision needs to be made or revisited, the principles to weigh against fit on a page. Patterns, inventories, and decision histories live in the documents linked at the bottom.

---

## 1. Feature-First Design

LumaCore organizes code by feature, not by technical layer. Everything related to a capability — endpoints, services, configuration, contracts — lives in one folder.

The traditional split into `Controllers/`, `Services/`, `Models/` scatters one capability across five places. Feature-first reverses that: open the feature folder, and you see the capability whole. New developers find what they need; reviewers see the blast radius of a change at a glance; modules can grow or be removed without leaving residues across the tree.

For the concrete shape this takes in code, see the [Feature Pattern](feature-pattern.md).

---

## 2. Separation of Concerns

Within that feature-first organization, the codebase is sliced into three architectural layers:

```
Transport     → Handles communication with the outside world
Foundation    → Provides hosting-agnostic building blocks
Presentation  → Provides the user interface
```

Transport, foundation, and presentation are fundamentally different concerns, and mixing them makes each one harder to understand and test. Foundation code that does not know about HTTP can be unit-tested without spinning up a server. Transport code that does not know about domain logic can be replaced — by a CLI, a desktop app, an alternative frontend — without touching the foundation. The clean cut between them is what allows each layer to evolve at its own pace.

This is a **logical** separation. Foundation is shared: both transport and presentation can take a dependency on the same hosting-agnostic building blocks instead of reimplementing or duplicating them. A host may still bundle all three in one process and one deployment, or the presentation may be built and served separately. The split is about how the source is organized, not about how many deployables you run in production.

---

## 3. Fail-Fast

LumaCore prefers a deployment that refuses to start over a deployment that runs subtly broken. Configuration is validated at startup; missing values, drifted section names, or bypassed registration patterns all cause the application to abort before it accepts a single request.

This is strict — you cannot run with partial configuration "just to see what happens" — but the trade is deliberate. A misconfigured signing key that surfaces at 3 AM under real load is far more expensive than a misconfigured signing key that surfaces in CI or during deploy. The principle moves that cost from operations to engineering, where it belongs.

The same idea extends beyond configuration. Endpoints that forget to declare their API version, or forget to declare whether they require authorization, block the boot rather than silently slipping into production. Validation at the latest responsible moment, not the earliest convenient one.

---

## 4. Explicit Over Implicit

LumaCore avoids "magic". Dependencies are declared in constructors, not pulled from ambient context or service locators. Configuration is registered visibly in `Program.Services.cs`, not discovered through reflection or attribute scanning. Features are wired up by code you can read top-to-bottom, not by convention that activates whenever an assembly happens to be in `bin/`.

The cost of this is a handful of extra lines of registration per feature. The benefit is that one can answer *"what does this class need?"* by reading its constructor, and *"what features are enabled?"* by reading one file. No hidden static dependencies to mock in tests, no surprise plug-ins lit up by mere assembly presence, no future maintainer who has to discover behavior by accident.

This explicitness is also what makes the fail-fast guarantees of the previous principle practical. Convention-based or reflective wiring *can* be discovered and validated while the host is building — before the first request — if you make that work part of startup. The common failure mode with *implicit* composition is not an iron rule that boot *cannot* see the graph; it is that the graph is hard to read, checks are easy to skip or to duplicate, and truly late work (lazy resolution, config that appears after the process is live, or code paths that only run under some traffic) can still slip past unless you design for it. Explicit registration lines up *what* is enabled and *what* is asserted before load arrives.

---

## 5. Foundation First

Production-grade infrastructure — logging, health checks, error handling, security headers, fail-fast configuration — is established **before** domain features mature on top. The opposite order has a notorious failure mode: a quick prototype ships to production, and *"we'll add proper logging later"* never quite happens.

The trade is that the first few features take longer than they would in a lean spike, because patterns and infrastructure are being established alongside functionality. The payoff is that every subsequent feature inherits a solid base: it does not need to reinvent logging, error handling, or its own configuration story, and operational concerns stay uniform across the codebase as it grows.

---

## Where to Read Further

- [Architecture Overview](README.md) — High-level architecture, layers, request flow.
- [Project Structure](project-structure.md) — Repository layout and per-project responsibilities.
- [Feature Pattern](feature-pattern.md) — The concrete shape of feature modules.
- [Architecture Decision Records](decisions/README.md) — Why specific patterns were chosen, and what was deliberately rejected.

---

© 2026 LumaCoreTech • MIT License
