# ADR-0002 — Custom async primitives (`AsyncManualResetEvent`, `AsyncWaitQueue`)
**Status:** Accepted · 2026-04-26

## Context

`LumaCore.Core` components need to coordinate asynchronous operations — waiting for state changes, broadcasting signals to multiple waiters, and canceling those waits deterministically. The .NET base class library covers synchronous primitives (`ManualResetEvent`, `AutoResetEvent`) and some async-capable types (`SemaphoreSlim`), but none of them cover the full set of requirements that arise in `LifecycleManagement` and other components that need cooperative asynchronous waiting.

The forces that shape the decision:

- **`await`-friendly waiting.** `ManualResetEvent` / `AutoResetEvent` block a thread; `SemaphoreSlim.WaitAsync` is async but has the wrong release semantics for "broadcast all waiters". The base class library has no async equivalent of a manual-reset event.
- **Cancellation must be a first-class participant.** Waiters must be able to pass a `CancellationToken` and observe `OperationCanceledException` deterministically — including when the token cancels after the waiter has already started waiting.
- **Continuations must run asynchronously.** A waiter that signals completion under a held lock must not also run the awaiter's continuation under that lock; the result is exactly the kind of inversion-deadlock that asynchronous code is supposed to avoid. `TaskCreationOptions.RunContinuationsAsynchronously` is the only safe default.
- **Predictable ordering.** Waiters that arrive while the event is not yet signaled must be released in FIFO order. Implicit ordering changes are surprising to debug.
- **Minimal dependency surface.** `LumaCore.Core` deliberately avoids pulling in third-party concurrency libraries (e.g. `Nito.AsyncEx`) for what amounts to a few primitives that we want to control end-to-end.

## Decision

Implement the async primitives in-house as a small, layered family in `LumaCore.Core.Threading`:

- **`TaskCompletionSourceExtensions`** is a centralized factory that enforces `TaskCreationOptions.RunContinuationsAsynchronously` repo-wide. Ad-hoc `new TaskCompletionSource<T>()` is a code-review red flag — the factory is the only sanctioned construction path.
- **`IAsyncWaitQueue<T>` / `DefaultAsyncWaitQueue<T>`** is the shared substrate for both event types. It is intentionally **not** thread-safe; locking discipline belongs to the owning event, keeping the locking hierarchy explicit and visible.
- **`CancellationTokenTaskSource<T>`** bridges a `CancellationToken` into an awaitable `Task<T>` that completes with `OperationCanceledException` when the token fires. This makes cancellation a composable building block rather than something each event has to wire up inline.
- **`AsyncManualResetEvent`** and **`AsyncAutoResetEvent`** sit on top of the wait queue and use `CancellationTokenTaskSource<T>` for cancellation support. The manual-reset variant broadcasts (all waiters released at once); the auto-reset variant is unicast (exactly one waiter released per signal).
- **`TaskExtensions.OrderByCompletion`** reorders a set of tasks so they can be awaited in the order they actually finish, regardless of the order they were started — useful for progressive UI updates where the fastest result should appear first. It is included alongside the event primitives as a related async coordination utility.

## Consequences

**Upside**

- Every primitive in `LumaCore.Core.Threading` shares the same `RunContinuationsAsynchronously` discipline. Whole categories of "completion under lock causes deadlock" bugs are designed away.
- The owning event class controls its own synchronization. The wait queue does not paper over locking with internal `Monitor` calls — a deliberate "thread-unsafe by design" choice that makes the locking hierarchy visible.
- FIFO ordering is observable (and tested), which makes the primitives suitable for fairness-sensitive scenarios.
- The dependency footprint of `LumaCore.Core` stays at "BCL only" for synchronization. Versioning, security advisories, and breaking changes in third-party concurrency libraries are not our problem.

**Downside**

- We carry the maintenance and testing cost of code that is (in spirit) a subset of `Nito.AsyncEx`. The implementations are short and the test coverage is high, but it is still our code to debug.
- The primitives are easy to misuse if a caller forgets the locking discipline — `IAsyncWaitQueue<T>` looks like a normal collection at first glance. The XML doc states the lock requirement, but new contributors must read it.
- `AsyncManualResetEvent.Reset` allocates a fresh `TaskCompletionSource<object?>` per cycle. For event-heavy hot paths this is measurable. We accept it because the primitives are used in coarse-grained orchestration, not in inner loops.

## Alternatives Considered

### `SemaphoreSlim` + manual broadcast

A `SemaphoreSlim` initialized to 0 would mimic a manual-reset event by releasing N permits, and a `Reset` would consist of draining outstanding permits. Rejected because:
- "Drain outstanding permits" has no atomic operation in the BCL — there is no way to reset back to 0 while waiters may still be unblocking.
- Cancellation semantics are awkward; cancelling a `SemaphoreSlim.WaitAsync` does not communicate downstream the way a fail-with-`OperationCanceledException` does in our wait queue.

### `Channel<T>` for the wait queue

Considered as the substrate for `AsyncAutoResetEvent`. Rejected because `Channel<T>` is designed for value-passing pipelines; using it as "park N waiters and wake exactly one" requires unbounded channels with explicit count tracking, which yields an implementation strictly more complex than the bespoke `IAsyncWaitQueue<T>`.

### Reuse `Nito.AsyncEx` directly

`Nito.AsyncEx` provides excellent equivalents (`AsyncManualResetEvent`, `AsyncAutoResetEvent`, `IAsyncWaitQueue<T>`). Rejected because:
- `LumaCore.Core` deliberately keeps a minimal external dependency surface (BCL, `Microsoft.Extensions.Logging.Abstractions`). Adding a third-party concurrency library because we want four small types is disproportionate.
- We cannot guarantee that future versions will keep the exact `RunContinuationsAsynchronously` defaults that we rely on. Owning the implementation lets us guarantee the contract.
- Some semantics (the `OrderByCompletion` extension; the centralized `TaskCompletionSource<T>` factory) are not available out-of-the-box and would have to be written anyway.

### `TaskCompletionSource<T>` directly at every call site

The most BCL-pure option. Rejected because each call site would have to remember `RunContinuationsAsynchronously`, build its own cancellation chaining, and reproduce the FIFO contract. The primitives exist precisely so that we *don't* have to remember those details everywhere.

## See also

- Implementation
  - `src/LumaCore.Core/Threading/AsyncAutoResetEvent.cs`
  - `src/LumaCore.Core/Threading/AsyncManualResetEvent.cs`
  - `src/LumaCore.Core/Threading/CancellationTokenTaskSource.cs`
  - `src/LumaCore.Core/Threading/DefaultAsyncWaitQueue.cs`
  - `src/LumaCore.Core/Threading/IAsyncWaitQueue.cs`
  - `src/LumaCore.Core/Threading/TaskCompletionSourceExtensions.cs`
  - `src/LumaCore.Core/Threading/TaskExtensions.cs`
- Tests: `src/LumaCore.Core.Tests/Threading/`
- Attribution: [`THIRD-PARTY-NOTICES.md`](../../../THIRD-PARTY-NOTICES.md) — the primitives in this namespace were originally adapted from Nito.AsyncEx (MIT) by Stephen Cleary.
- Related: [ADR-0001 — `LifecycleManagement` as state-machine](0001-lifecycle-management-as-state-machine.md) (primary consumer of these primitives)
