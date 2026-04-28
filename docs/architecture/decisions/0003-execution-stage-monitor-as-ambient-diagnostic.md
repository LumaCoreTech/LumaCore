# ADR-0003 — `ExecutionStageMonitor` as ambient diagnostic API
**Status:** Accepted · 2026-04-26

## Context

Several `LumaCore.Core` components need to be tested for behaviors that are hard or impossible to trigger from the outside:

- Canceling a long-running operation **at a specific point** in its execution (e.g. between two `await`s, after a partial state mutation).
- Injecting a fault (`IOException`, `OperationCanceledException`, …) **at a specific point** to exercise catch-blocks and recovery paths deterministically.
- Asserting that a method actually reached a specific checkpoint, without resorting to log scraping or wall-clock timing.

The obvious alternatives — extracting the relevant logic behind a mockable interface and injecting test doubles — would require the production code to *know* about its own testability. Every method that needs a stage-level checkpoint would have to take an additional parameter, hold a reference to a service, or accept an `Action<string>` hook. The instrumentation surface would scale linearly with the number of testable checkpoints, and most of it would only ever be exercised from tests.

The forces that shape the decision:

- **Production overhead must be near-zero.** The mechanism cannot impose allocations or virtual dispatch on every checkpoint when no test is observing.
- **No leakage into the production API.** Adding test parameters to public method signatures is unacceptable; it conflates "what the code does" with "how the code is tested".
- **Determinism in tests.** Mechanisms based on wall-clock timing or polling are unreliable under load (CI agents, debug builds).
- **Async-flow correctness.** `await` continuations may run on a different thread than the original call, so a thread-static or `[ThreadStatic]` approach is unsafe.

## Decision

Introduce `ExecutionStageMonitor` as an **ambient** diagnostic API:

- Production code emits checkpoints with a single static call: `ExecutionStageMonitor.ReportStage("MyClass.MyMethod.BeforeQuery")`. Stage names are plain strings, namespaced by convention (`{Type}.{Method}.{CheckpointId}`, e.g. `ConnectionManager.StartAsync.BeforeHandshake`). Including the type name keeps stages unambiguous when multiple components participate in the same async flow.
- Tests opt in with `using var monitor = ExecutionStageMonitor.Configure().CancelAt(...).ThrowAt(...).OnStage(...)`. The monitor flows through `await` boundaries via `AsyncLocal<T>`.
- Without an active monitor, `ReportStage` resolves to one `AsyncLocal<T>` read plus a `null` check — no allocations, no dispatch.
- Configuration is *additive and unique*: registering the same stage name twice on the same monitor is a configuration error and throws `ArgumentException` with `ParamName="stage"`.
- **Nesting is not supported.** Calling `Configure()` while another monitor is active in the current async flow throws `InvalidOperationException`. See [Alternatives Considered](#alternatives-considered).

## Consequences

**Upside**

- Production method signatures remain unchanged. The instrumentation surface is one static call.
- The monitor is invisible at runtime: zero allocations, no virtual dispatch, no extra `Task` continuations.
- Tests express intent declaratively (*"cancel when the code reaches stage X"*) instead of imperatively (*"set up a fake repository that throws on the third call"*). Cancellation tests in particular become deterministic.
- The `AsyncLocal<T>` design works correctly across `await`, `Task.Run`, `ConfigureAwait(false)` and `Parallel.ForEachAsync` without explicit propagation.

**Downside**

- Stage names are stringly-typed and cannot be checked at compile time. We accept this in exchange for the simplicity of the API; in practice the names appear in two places (the `ReportStage` call site and the test) and a typo surfaces as an obviously failing test.
- The mechanism is non-discoverable from the production call site — readers must know to look for `ReportStage`. Mitigation: the call sites are intentionally rare, and grep is sufficient.
- A forgotten `Dispose()` stays active for the rest of the current async flow. If a helper or fixture later tries to configure its own monitor in the same flow, the fail-fast nesting check throws `InvalidOperationException` instead of silently overlaying. The pattern is `using var monitor = …` and the test guidelines enforce it.

## Alternatives Considered

### Mockable interface + DI

The "textbook" alternative. Extracting each checkpointed method behind an interface, injecting a test double, and asserting on the double's interactions.

Rejected because:

- It bloats production signatures and DI registrations with infrastructure that exists only for testability.
- It does not solve "cancel at a specific point inside a method" — the test double would need to call back into the cancellation token of a still-running parent operation, which is exactly the ambient-state problem we are trying to avoid.
- The ratio of test infrastructure to production code becomes uncomfortable for what should be a few targeted fault-injection tests.

### `[ThreadStatic]` / direct field

Considered for the lower overhead. Rejected because `await` continuations may resume on different threads, so `[ThreadStatic]` would silently drop the monitor at the first asynchronous boundary. `AsyncLocal<T>` is the only correct choice.

### Stack-based (broadcast or fall-through) nesting

The original audit recommendation suggested supporting nested monitors, with the stack restored on disposal. Two variants were prototyped:

1. **Innermost wins** — only the innermost monitor observes `ReportStage`.
2. **Broadcast** — every active monitor observes; outer monitors continue to fire after the inner has consumed the stage.
3. **Fall-through** — inner monitors shadow stages they explicitly configure; unhandled stages fall through to the outer monitor.

Each variant raises non-trivial questions about precedence, side-effect ordering, exception propagation, and reasoning about the system from a single test. The only legitimate use case (one fault-injection scope per test) does not require nesting at all. Supporting it would impose conceptual cost on every reader for a benefit no test actually needs.

The current design therefore **prohibits** nesting: `Configure()` throws `InvalidOperationException` if another monitor is already active. This trades flexibility we do not use for a guarantee that *every* test sees a single, well-defined monitor — and surfaces leaked monitors from earlier tests immediately instead of producing confusing test interactions.

### `Activity.Current` / `DiagnosticSource`

Standard .NET diagnostic plumbing was considered as a substrate. Rejected because:

- These APIs are designed for cross-process telemetry (sampling, propagation through HTTP headers, vendor exporters), not for in-process deterministic fault injection.
- The fluent `CancelAt`/`ThrowAt`/`OnStage` API would need to be built on top of them anyway; the substrate adds dependencies and indirection without reducing complexity.

## See also

- Implementation: `src/LumaCore.Core/Diagnostics/ExecutionStageMonitor.cs`
- Tests: `src/LumaCore.Core.Tests/Diagnostics/ExecutionStageMonitorTests.*.cs` (split per method by file)
- Related: [ADR-0001 — `LifecycleManagement` as state-machine](0001-lifecycle-management-as-state-machine.md) (uses `ReportStage` checkpoints in its initialize/shutdown choreography)
