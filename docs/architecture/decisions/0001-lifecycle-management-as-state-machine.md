# ADR-0001 — `LifecycleManagement` as state-machine for initialize/shutdown
**Status:** Accepted · 2026-04-26

## Context

Many components are long-lived stateful objects (services, runtimes, registries) that need a controlled startup, an in-flight working phase, an orderly shutdown, and finally disposal. The .NET base class library offers `IDisposable` and `IAsyncDisposable`, which solve the *release* problem but leave the surrounding orchestration to the caller.

The forces that shape the decision:

- **Initialization can be expensive** (open files, connect to dependencies, prepare caches) and must run **at most once per active life**, even when several callers race to initialize the same instance.
- **Shutdown must be cooperative**: in-flight operations need a chance to finish, late callers need to be told the object is going away, and disposal must not race with operations that are still executing.
- **The object should be reusable** — components legitimately need to be shut down and re-initialized within the same process (test fixtures, configuration reloads). A simple `bool mDisposed` makes this awkward and error-prone.
- **State must be observable** — both `IsInitialized` for callers and richer hooks (pending-operations counters, cancellation tokens) for derived classes.
- **Disposal from synchronous code** must work without blocking the thread pool, while async disposal must be the preferred path. Mixing the two naively leads to deadlocks (sync-over-async).
- **Failure recovery must be guaranteed.** If initialization fails, the compensating shutdown must restore a clean state — unconditionally. If the compensating shutdown itself fails, leaving the object in an indeterminate state is worse than terminating: a corrupted long-lived object produces silent, hard-to-diagnose bugs. The correct response is controlled process termination via `FailFast`.

A bare `IDisposable`/`IAsyncDisposable` plus a manual `bool mInitialized` field is the path of least resistance. It does not survive contact with concurrency: races between `Initialize`/`Shutdown`/`Dispose` quickly produce torn state, and there is no safe way to wait for *currently running* operations to complete before disposing.

## Decision

`LumaCore.Core.LifecycleManagement` is an abstract base class that models the lifecycle as a five-phase state machine, with explicit transitions and concurrency primitives:

- **Phases**: Uninitialized → Initializing → Initialized → ShuttingDown → Disposed (with re-entry into Initializing legal after a clean shutdown).
- **Single-source-of-truth state** in `LifecycleState`, guarded by a shared `Lock`. Transitions happen inside short critical sections; long-running work (`OnInitializingAsync`, `OnShuttingDownAsync`, `OnDisposingAsync`) executes outside the lock.
- **Concurrent caller arbitration**: when a caller arrives during an in-flight initialization or shutdown, it does not race; it waits on a per-transition `AsyncManualResetEvent` and re-evaluates the state once the event fires. This collapses the matrix of concurrent caller scenarios into a small set of well-defined waits.
- **Operation tracking** via `BeginOperation` / `BeginAsyncOperation` returns a disposable scope. Pending-operation count is exposed to the runtime so that shutdown can wait for in-flight work and disposal cannot run while a scope is open.
- **Cooperative cancellation** via `LifecycleState.ShutdownToken`. Long-running operations subscribe to this token; when shutdown begins, the token cancels and operations are expected to wind down voluntarily.
- **Synchronous `Dispose()`** delegates to `DisposeAsync().GetAwaiter().GetResult()` (with a documented warning about the deadlock risk on captured synchronization contexts). `DisposeAsync()` is the supported path.
- **Failure during `OnInitializingAsync`** triggers an automatic compensating `OnShuttingDownAsync` to restore a clean state, and the original exception is re-thrown so the caller sees the failure. If the compensating shutdown also fails, `FailFast.TerminateApplication` is called immediately — running with an inconsistent object would produce silent, hard-to-diagnose failures.

## Consequences

**Upside**

- Initialize/Shutdown/Dispose semantics are *the same* across every component that derives from `LifecycleManagement`. No subclass invents its own variant of "did I already start?".
- Concurrent callers behave predictably: at most one logical initialization runs at a time; latecomers either reuse the result or correctly observe the failure.
- Disposal is not a race condition — pending-operation tracking guarantees that no work is silently torn off mid-flight.
- Re-initialization after shutdown is a first-class scenario, which simplifies testing significantly.
- Cancellation flows through `ShutdownToken`, giving subclasses a clean signal to wind down without polling state flags.

**Downside**

- The base class is non-trivial — ~650 lines, one `Lock`, several `AsyncManualResetEvent` collections, a state object, operation scopes. Subclasses must understand it before they can override the right hooks.
- Synchronous `Dispose()` will deadlock on a captured synchronization context if disposed via `using` from an async method that ran on, e.g. a Blazor UI thread. The method is marked `[Obsolete]` so any direct call produces a compiler warning; the supported path is `await using`.
- The pattern enforces a particular ordering of hooks (`OnInitializingAsync` → `OnShuttingDownAsync` → `OnDisposingAsync`). Subclasses that need a non-standard order do not fit the pattern; they have not been observed in practice.
- The `LifecycleState` object exposes more surface than a typical "is initialized" flag. This is necessary for the derived classes that need it, but new users see more API than they will use day-to-day.

## Alternatives Considered

### Plain `IAsyncDisposable` + `bool mInitialized`

The most lightweight option. Each component would track its own state, throw `InvalidOperationException` on misuse, and trust callers to coordinate.

Rejected because it does not address the core problems: concurrent initialization races, "wait for in-flight operations before disposal", and re-initialization after shutdown. Every component would invent its own (probably buggy) version of these guarantees.

### Init/Shutdown via factory + immutable handle

A factory creates the component in already-initialized state and returns a handle; shutting down means dropping the handle. No "uninitialized" phase ever exists.

Rejected because LumaCore components legitimately have heavy, asynchronous setup that fails on bad configuration. Failure during construction collapses into a constructor exception, which conflicts with `IAsyncDisposable` ergonomics and forbids re-initialization.

### `Microsoft.Extensions.Hosting.IHostedService`

The DI-host equivalent (`StartAsync` / `StopAsync`). It is shaped for hosted services that the .NET host orchestrates, not for arbitrary `LumaCore.Core` objects.

Rejected because:
- It binds the lifecycle to a `HostedService` registration, dragging in `Microsoft.Extensions.Hosting`. `LumaCore.Core` is intentionally hosting-agnostic.
- It does not provide pending-operation tracking or operation scopes; consumers would still need to roll their own.
- Restart after stop is not a supported scenario in `IHostedService`.

### `Nito.AsyncEx.AsyncLazy<T>` for one-shot initialization

A natural fit for "initialize once, then read forever". Rejected because it covers only the initialization phase. Shutdown, disposal, operation tracking, and cancellation propagation remain for us to solve, and integrating `AsyncLazy<T>` does not simplify the rest.

## See also

- Implementation
  - `src/LumaCore.Core/LifecycleManagement.cs`
  - `src/LumaCore.Core/LifecycleState.cs`
- Tests: `src/LumaCore.Core.Tests/LifecycleManagementTests.*.cs`
- Related: [ADR-0002 — Custom async primitives](0002-custom-async-primitives.md) (provides the `AsyncManualResetEvent` used by the wait paths), [ADR-0004 — `FailFast` with cooperative subscribers](0004-failfast-cooperative-subscribers.md) (used as the escalation path when a compensating shutdown also fails)
