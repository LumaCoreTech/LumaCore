# ADR-0004 — `FailFast` with cooperative subscribers
**Status:** Accepted · 2026-04-26

## Context

LumaCore components occasionally reach a state where continuing execution is unsafe — a `LifecycleManagement` shutdown that itself failed during a recovery path, an invariant that has been irrecoverably violated, or a system condition that the runtime cannot reconcile. The .NET base class library exposes `Environment.FailFast(string)` and `Environment.FailFast(string, Exception)` for exactly this scenario: write a Windows Error Reporting record and terminate the process immediately, bypassing finalizers and `try`/`finally`.

Calling `Environment.FailFast` directly works, but loses information that we want to preserve:

- **Buffered log output disappears.** `Microsoft.Extensions.Logging` providers and Serilog sinks routinely buffer asynchronously (channels, file streams, network sinks). A direct `Environment.FailFast` terminates before the buffer is drained, which is precisely the case where the operator most needs the logs.
- **Tests cannot inspect the call.** A test that wants to verify "this code path terminates the application with the right message" cannot do so against a method that actually terminates the test process.
- **Multiple shutdown concerns are coupled.** Telemetry flush, metrics emission, and orderly resource hand-off all want to participate just before termination, but `Environment.FailFast` offers no extension point.

The forces that shape the decision:

- **The application *must* terminate.** Any extension mechanism must not let well-meaning subscribers turn a fatal-error path into a silent one in production.
- **Subscribers may be slow or hung.** A flushing log sink that tries to reach a network socket may block. The termination path must remain bounded — a hung subscriber cannot prevent the process from going down.
- **Test observability requires a deterministic hook.** Tests need a way to assert "FailFast was reached" without actually terminating the process, but the hook must be unambiguously *test-only*.
- **Subscriber registration races.** `add`/`remove` of event handlers is not thread-safe in the auto-generated form; for static state on a `static class`, this becomes process-wide shared state and needs explicit locking.

## Decision

Introduce `LumaCore.Core.FailFast` — a `static class` wrapping `Environment.FailFast` with two narrowly scoped extension hooks:

- **`event Action<string, Exception?> TerminationRequested`** — invoked just before `Environment.FailFast`. Subscribers (typically logging/telemetry plumbing) get a last chance to drain buffers. Subscribers cannot prevent termination; whatever they do, the next call after the hook is `Environment.FailFast`.
- **`event EventHandler<FailFastEventArgs> BeforeTermination`** — invoked *before* `TerminationRequested`. The event arguments carry a `Cancel` flag; when set to `true`, `FailFast.TerminateApplication` throws `FailFastCanceledException` instead of terminating. This hook exists **for unit tests only** — production subscribers must not subscribe to `BeforeTermination`. The XML doc and the type name make this explicit.
- **Subscribers run outside the registration lock.** Registration is thread-safe, but invocation happens against a snapshot of the delegate chain. A slow or hung subscriber can therefore not block another thread from registering, nor deadlock with the subsequent `Environment.FailFast` call.

## Consequences

**Upside**

- Logs are flushed before termination in the typical configuration — operators get the diagnostic context they need.
- The `BeforeTermination`/`Cancel` hook makes `FailFast` testable end-to-end. Production code paths that call `FailFast.TerminateApplication` can be verified in a normal test run.
- Subscribers do not run under the registration lock, so a hung subscriber affects only itself; the process still terminates.
- Subscriber registration is thread-safe.

**Downside**

- `BeforeTermination` is a sharp tool. Subscribing to it from production code converts every fatal error into a recoverable exception, which is precisely the wrong behavior. The XML doc warns about this in plain terms; we accept the residual misuse risk.
- Tests that subscribe to `BeforeTermination` must unsubscribe again — a leaked subscription turns the *next* genuine fail-fast in the same process into a swallowed exception. The convention is `try`/`finally` around the subscription, and the test guidelines enforce it.
- Subscribers that *do* hang silently delay termination until the OS or operator notices. We accept this trade-off because the alternative — running subscribers under the lock or with a timeout — has its own pathologies (registration deadlock; killed-mid-flush log writes).

## Alternatives Considered

### Use `Environment.FailFast` directly at every call site

The minimal-surface approach. Rejected because it loses the log-flush hook, defeats unit testing of the failure path, and makes "what subscribers participate at termination time" a non-question — there is nowhere to put them.

### `AppDomain.CurrentDomain.UnhandledException` / `AppDomain.CurrentDomain.ProcessExit`

These hooks fire automatically on uncaught exceptions or normal process exit. Rejected as the primary mechanism because:
- They are reactive, not declarative — code cannot say "I have decided to terminate now". `LumaCore.Core` needs an explicit, intentional escalation point that is callable from a known location.
- `UnhandledException` does not always result in process termination (it depends on the runtime configuration), so the post-hook handlers do not actually run in every "we are about to die" scenario.
- These hooks are perfectly fine as *additional* safety nets and remain available; they simply do not replace an explicit fail-fast API.

## See also

- Implementation: `src/LumaCore.Core/FailFast.cs`, `src/LumaCore.Core/FailFastEventArgs.cs`, `src/LumaCore.Core/FailFastCanceledException.cs`
- Tests: `src/LumaCore.Core.Tests/FailFastTests.cs`
- Related: [ADR-0001 — `LifecycleManagement` as state-machine](0001-lifecycle-management-as-state-machine.md) (escalates to `FailFast.TerminateApplication` when a compensating shutdown also fails)
