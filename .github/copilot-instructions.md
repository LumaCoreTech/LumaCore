# Copilot Instructions

## Table of Contents

1. [General](#general)
   - [Repository Constraints](#repository-constraints)
   - [Build Configuration](#build-configuration)
   - [Terminal Usage](#terminal-usage)
2. [Working with Code](#working-with-code)
   - [Code Style & Changes](#code-style--changes)
   - [XML Documentation](#xml-documentation)
   - [Core Principles](#core-principles)
   - [Code Patterns](#code-patterns)
   - [Verification & Review](#verification--review)
3. [Workflow](#workflow)
4. [Testing](#testing)
   - [Authoring Rules](#authoring-rules)
   - [Test Structure](#test-structure)
   - [Coverage](#coverage)
5. [Data Layer / EF Core](#data-layer--ef-core)
   - [Entity Documentation](#entity-documentation)
   - [Entity Layout](#entity-layout)
   - [Migrations](#migrations)
   - [Data Layer Guidelines](#data-layer-guidelines)
6. [Privacy & Data Minimization](#privacy--data-minimization)
7. [Logging & Configuration](#logging--configuration)
8. [Code Layout & Naming](#code-layout--naming)
9. [Commit Messages](#commit-messages)

---

## General

### Repository Constraints

> [!IMPORTANT]
> **Target Framework:** .NET 10

- Do **not** perform any **write** operations using Git (e.g., commit, push, reset, rebase, merge, tag, branch creation/deletion).
- Localization is only active in the UI (Blazor) — API responses and validation messages remain in **English**.

### Terminal Usage

> [!WARNING]
> **Avoid complex PowerShell scripts** in the Copilot terminal — they tend to hang and require manual cancellation.

- Prefer using `replace_string_in_file` or `multi_replace_string_in_file` for file modifications.
- Use simple, single-command terminal calls only when necessary (e.g., `dotnet build`, `Get-Content`).
- Do **not** use loops, pipelines with multiple stages, or scripts that modify many files at once via the terminal.

### Build Configuration

- **Avoid behavior differences** between Debug and Release builds.
- **For unreachable guards** (code that should never execute because all cases are covered):
  - Use `throw new UnreachableException()` from `System.Diagnostics` instead of `#if DEBUG` / `#if RELEASE` blocks.
  - Always add an explicit comment explaining **why** the path is unreachable (e.g., "All enum values handled above").

---

## Working with Code

> [!IMPORTANT]
> These principles apply to **all** code interactions — writing, modifying, reviewing, or debugging.

### Code Style & Changes

- **Keep changes minimal** and focused on the task at hand.
- **Match the existing code style** of the surrounding code.
- **Line length limit: 120 characters.** Use the available width — don't break lines unnecessarily short.
- When you notice opportunities for improvement (refactoring, better patterns, code quality):
  - **Explicitly mention them** and start a dialog.
  - After user approval, improvements can be implemented.

> [!NOTE]
> When a task is explicitly about repository instructions (like this file), it is OK to update instruction files immediately without waiting for an explicit start signal.

### XML Documentation

- Add complete XML documentation (`<summary>`, `<param>`, `<returns>`, `<exception>`) for **all** methods and properties you add or change.
- Use `<remarks>` for additional details, `<example>` for usage samples when helpful.
- XMLDocs should be written for **all members**, not just public APIs — internal and private members are important for developers.
- **Use the full 120-character line width** in XML documentation. Break at logical points (end of sentence) rather than at arbitrary positions.

#### Common Inline Tags

| Tag | Usage |
|-----|-------|
| `<see cref="..."/>` | Link to types, methods, properties |
| `<see langword="..."/>` | Language keywords (`null`, `true`, `false`, etc.) |
| `<c>...</c>` | Inline code |
| `<code>...</code>` | Multi-line code block |
| `<paramref name="..."/>` | Reference a parameter |
| `<typeparamref name="..."/>` | Reference a type parameter |

#### Style Guidelines

- Prefer the **shortest possible** type names (use `using` directives to enable short names).
- Ensure proper list and paragraph structure in XMLDocs.
- When updating XML documentation, prioritize improved readability.

### Core Principles

| Principle | Meaning |
|-----------|---------|
| **Verify, don't assume** | Inspect actual implementation before stating how something works |
| **Code > Comments** | Trust what the code does over what comments claim |
| **Ask > Assume** | When unclear, ask instead of guessing |
| **Quality > Speed** | Take time to be thorough; rushing causes false positives |

### Code Patterns

> [!IMPORTANT]
> These patterns are **mandatory**. For full details, see [Coding Standards](../docs/development/coding-standards.md).

#### Async/Await

- **Use `ConfigureAwait(false)` on every `await` in library code** to prevent deadlocks and keep code portable.
  - **Exception:** Blazor components using `IJSRuntime` must use `ConfigureAwait(true)` (or omit it).
- Returning a `Task` without `await` is OK; do not introduce `async` only to add `ConfigureAwait(false)`.
- **Async all the way** — always use async APIs in async methods (e.g., `File.ReadAllTextAsync` instead of `File.ReadAllText`).

#### Task vs ValueTask

- **Default to `Task`** for all async operations.
- **Use `ValueTask`** only in profiled hot paths where synchronous completion is common (e.g., cache hits).

#### Span<T> Support

- Provide `Span<T>` / `ReadOnlySpan<T>` overloads for performance-critical APIs where applicable.
- Example: `Parse(string value)` → also offer `Parse(ReadOnlySpan<char> value)`.

#### Thread Safety

> [!WARNING]
> Using `ConfigureAwait(false)` means continuations may run on **different threads**. Library code with mutable shared state **must be thread-safe**.

### Verification & Review

#### Always verify behavior (no assumptions)

- Do not make assumptions about existing behavior.
- Before stating how something works (or before changing tests/implementation), **inspect the current implementation**.
- If the observed implementation appears incorrect or ambiguous, **call it out explicitly** and start a discussion.

#### Proactively report inconsistencies

Point out issues when you notice them:
- Implementation exists but interface/contract is missing
- API is defined in the wrong service interface
- XMLDocs/`cref` points to non-existent members
- Duplicated/contradicting behavior

When possible, suggest the smallest consistent fix.

#### Triple-Check Before Reporting Issues

Before reporting ANY finding, verify it **three times**:

| Check | Question |
|-------|----------|
| **Check 1** | Is this really a problem? Read the code again, check related files, look for comments. |
| **Check 2** | Could this be intentional? Known pattern? Matches BCL behavior? Defensive reasoning? |
| **Check 3** | What if I'm wrong? Would "fixing" this break something? Do I have concrete evidence? |

> [!CAUTION]
> **If ANY doubt remains → ASK, don't claim**

#### Code vs. Comments: Trust the Code
Priority of truth:
1. What the code actually does  ← The ultimate truth
2. What tests verify
3. What comments claim          ← Can be outdated/wrong
4. What you assume
When comment contradicts code:
- The code is probably right, comment is probably outdated
- BUT: Ask first! Sometimes comment reveals a bug
- Report: "Line X comment says Y, but code does Z. Which is correct?"

#### When Unclear — Ask

| ❌ DON'T | ✅ DO |
|----------|-------|
| Assume the comment is correct | Ask: "Comment says Y, but code does Z. Is this intentional?" |
| Assume the code is correct | Ask: "I don't understand why X is done. Can you explain?" |
| Assume you understand | Ask: "Is this comment outdated or is there a bug?" |
| Report speculation | Provide specific line numbers and evidence |

#### Red Flags (Stop and Restart)

If you notice ANY of these, **stop and slow down**:

- ⚠️ Reporting issue in < 5 minutes
- ⚠️ Using words like "might", "could", "possibly"
- ⚠️ Can't provide specific line numbers
- ⚠️ Haven't read all related files
- ⚠️ Trusting comments without verifying code

> [!TIP]
> **Your job: Find real issues AND clarify confusion through dialog.**
>
> False positives waste time and destroy trust. Better to take 20 minutes and ASK when unsure, than 2 minutes and create false work.

---

## Workflow

### Features / Fixes / Test Changes

> [!IMPORTANT]
> When you are asked to implement a feature or fix a bug:
>
> 1. **Think through** the intended implementation first.
> 2. **Communicate a detailed plan:**
>    - What will change
>    - Which files are expected to be touched and why
>    - How correctness will be validated
> 3. **Wait for an explicit start signal** before making any code changes.

This is required so reviewers can influence the approach before code is changed.

---

## Testing

### Authoring Rules

> [!WARNING]
> These rules are treated as **hard requirements**.

#### Naming

Test method names must follow: **`Method_State_Expectation`**

| Pattern | Example |
|---------|---------|
| Regular method | `SomeMethod_WhenInputIsNull_ThrowsArgumentNullException` |
| Constructor | `Constructor_WhenInputIsNull_ThrowsArgumentNullException` |

#### AAA Pattern

Each test must follow the AAA pattern with inline comments:
// Arrange
var sut = new MyClass();

// Act
var result = sut.DoSomething();

// Assert
Assert.Equal(expected, result);
**Special cases:**
- If Act and Assert coincide (e.g., `Assert.Throws(...)`), use: `// Act + Assert`
- For exception tests: `var ex = Assert.Throws<...>(() => ...)` then assert on `ex`.
- For `ArgumentException` and derived types (e.g., `ArgumentNullException`, `ArgumentOutOfRangeException`): **always assert `ParamName`** matches the expected parameter name.

#### Staged Approach for Test Implementation

When implementing tests (especially for complex classes), use a staged approach:

| Stage | Focus | Checkpoints |
|-------|-------|-------------|
| **1. Implementation** | Functionality, test logic, AAA pattern | Compiles, tests green |
| **2. Coverage Review** | Run Coverlet, identify gaps, close with targeted tests | 100% coverage |
| **3. Structure** | Test ordering (Normal → Error), `#region` | Order correct |
| **4. XMLDocs** | `<see cref=""/>`, `<paramref/>`, `<see langword=""/>` | All tags correct |

Coverage review comes **before** structure/XMLDocs to avoid rework — once all tests exist, structure and documentation can be finalized in one pass.

#### XML Documentation

- Every test method must have complete XMLDocs.
- For `[Theory]` tests, include `<param>` for each method parameter.

#### Assertions

- Validate the **complete expected state** of the object — prefer exact string matches over substring checks.
- For data structures with multiple observable properties, use state verification helpers to ensure all properties are checked consistently.

#### State Verification Helpers

For data structures with multiple observable properties, create a helper method:
private static void AssertDequeState<T>(
    Deque<T> deque, int expectedCount, int expectedCapacity, T[] expectedElements)
{
    Assert.Equal(expectedCount, deque.Count);
    Assert.Equal(expectedCount == 0, deque.IsEmpty);
    Assert.Equal(expectedCapacity, deque.Capacity);
    Assert.Equal(expectedElements, deque.ToArray());
}
Place helpers in `FooTests.Helpers.cs`.

### Test Structure

#### Organization Model

| File Type | Content |
|-----------|---------|
| `FooTests.cs` | Anchor file: `partial class`, `[Trait(...)]`, no test methods |
| `FooTests.Bar.cs` | Tests for method `Bar()` |
| `FooTests.Bar.TestData.cs` | Test data (`MemberData`, `TheoryData`) for `Bar()` tests |
| `FooTests.Properties.cs` | All property tests, each in `#region` |
| `FooTests.Construction.cs` | Constructor tests |
| `FooTests.Equality.cs` | `Equals()` / `GetHashCode()` tests |
| `FooTests.Helpers.cs` | Helper methods |
| `FooTests.TestModels.cs` | Test-only data models |

#### Test Ordering (within a file)

1. **Normal operation** — all valid inputs, successful operations, expected validation failures
2. **Error / exception cases** — `ArgumentNullException`, `InvalidOperationException`, etc.

#### `#region` Usage

- **Avoid** `#region` for "Happy Path" vs "Error Cases" grouping — test ordering handles this.
- **Always use** `#region` when a single test file contains:
  - **Multiple method overloads** (e.g., `Ignore()` and `Ignore<T>()`)
  - **Multiple properties** in `FooTests.Properties.cs`
  - **Multiple distinct methods** being tested in the same file

Example:#region Ignore()
// Tests for non-generic Ignore(Task)
#endregion

#region Ignore<T>()
// Tests for generic Ignore<T>(Task<T>)
#endregion
#### Async Tests & Deadlock Prevention

Tests use a **two-tier timeout strategy**:

1. **Global timeout (10 seconds)** — Configured in `xunit.runner.json` as a safety net for unexpected hangs
2. **Explicit timeout helper** — For intentional waits where precise error messages help debugging

> [!IMPORTANT]
> Use `AwaitWithTimeoutAsync` **only for explicit waits** — operations where you intentionally wait for something.
// Add to test file:
using static LumaCore.Core.Tests.AsyncTestHelpers;
**When to use the timeout helper:**

| Operation | Use Helper? | Example |
|-----------|-------------|---------|
| `await are.WaitAsync()` | ✅ Yes | Explicit wait on async primitive |
| `await Assert.ThrowsAsync(() => waitTask)` | ✅ Yes | Waiting for task to throw |
| Synchronous `.Wait()` on primitive | ✅ Yes | Wrap in `Task.Run` + helper |
| `queue.Enqueue()`, `are.Set()` | ❌ No | Not intentional waits — global timeout covers |
| Property access, assertions | ❌ No | Not intentional waits — global timeout covers |

**Examples:**
// ✅ Explicit wait - use helper
await AwaitWithTimeoutAsync(are.WaitAsync(), "WaitAsync timed out");

// ✅ Waiting for exception - use helper
Task assertion = Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);
await AwaitWithTimeoutAsync(assertion, "Cancellation did not throw");

// ✅ Synchronous wait - wrap in Task.Run + helper
Task waitTask = Task.Run(() => are.Wait());
await AwaitWithTimeoutAsync(waitTask, "Wait timed out");

// ❌ Not a wait - global timeout handles unexpected hangs
queue.Enqueue();  // Just use directly
are.Set();        // Just use directly
The helper is defined in `src/LumaCore.Core.Tests/AsyncTestHelpers.cs`:
- `AwaitWithTimeoutAsync(Task, message?, timeout?)` — for void tasks
- `AwaitWithTimeoutAsync<T>(Task<T>, message?, timeout?)` — for tasks with results
- Default timeout: 1 second

#### Refactoring Tests

- Ensure **all existing test cases remain present**.
- **Do not** delete and regenerate — only move/copy existing blocks.

#### Theory Consolidation

- Consolidate similar edge cases into `[Theory]` with `MemberData`.
- Add `caseName` parameter and short comment per data row.

> [!NOTE]
> In this repo, tests are currently being built up and mostly use `[Fact]` (no xUnit `[Theory]`/`MemberData` yet), so testing rules about theories should be treated as forward-looking rather than enforced via current audits.

### Coverage

> [!NOTE]
> Target **100% line and branch coverage** for all reachable production code.

Use **Coverlet** to measure coverage. Focus on uncovered lines/branches, edge cases, error paths.

#### Coverage Exceptions

| Pattern | Reason |
|---------|--------|
| Static initializers, logging infrastructure | e.g., `LogWriter.Get<T>()` |
| `VersionNotSupportedException` in serialization | Defensive checks for future versions |
| `NotImplementedException` in default cases | Guards against invalid enum values |
| `throw new UnreachableException(...)` | By-design unreachable (see [Build Configuration](#build-configuration)) |

Do **not** hide reachable logic behind `ExcludeFromCodeCoverage` just to satisfy coverage.

#### Unit Tests

- Do **not** use `await using` for the SUT in unit tests — if the implementation is buggy, `DisposeAsync()` may hang, causing the test to hang instead of failing with a clear error message.

---

## Data Layer / EF Core

### Entity Documentation

- Prefer verbose XML documentation focusing on DB/EF semantics.
- Use `LumaCoreDbContext` as single source of truth for constraints and indexes.
- Remove UI-specific wording, keep EF Core usage hints (e.g., `Include(...)`).
- Document indexed properties using `<b>Index:</b> ...`, wrap structural tokens in `<c>...</c>`.

### Entity Layout

#### Standard Entities (single primary key)

| Order | Content |
|-------|---------|
| 1 | `Id` — Primary key |
| 2 | `PublicId` — Public identifier (if present) |
| 3 | Foreign Keys + Navigation Properties — grouped as pairs |
| 4 | Timestamps — `CreatedAtUtc`, `UpdatedAtUtc`, etc. |
| 5 | Scalar domain fields — required before optional |
| 6 | Collection navigation properties — at the end |

#### Join Entities (composite primary key)

| Order | Content |
|-------|---------|
| 1 | First FK + Navigation (grouped) |
| 2 | Second FK + Navigation (grouped) |
| 3 | Timestamps |
| 4 | Other properties — enums, flags, etc. |

### Migrations

Since nothing is released yet, **fold all changes into the existing initial migration/snapshot**.

### Data Layer Guidelines

- Control persisted-data pruning/policy via options in the data layer.
- Place service-like infrastructure under `LumaCore.Data.Services`.
- Track login and token refresh timestamps, but avoid `LastActivity` updates on every request.
- Usernames: store as entered, but lookups are case-insensitive.

---

## Privacy & Data Minimization

- **Prioritize privacy** — retain only legally required data.
- Design deletion and anonymization flows accordingly.

---

## Logging & Configuration

### Configuration Discoverability

- Surface new configuration keys in `appsettings.json`.
- Follow the `JwtOptions` pattern for secrets: Required + min length + `SecretAttribute` + env vars for production.

### Logging Guidance

- Startup log messages (not noisy warnings) for important configuration trade-offs.
- Integrity cleanup: **Information** when nothing found, **Warning** when deleting data.

---

## Code Layout & Naming

### Terminology

- Use **"Conversation"** consistently (not "Chat").
- Rename methods promptly when semantics change.

### File Headers

| Project | Header |
|---------|--------|
| `LumaCore.Data` | `Copyright (c) 2026 ...` |
| New files (2026) | `Copyright (c) 2026 ...` |
| Existing files (2025) modified | `Copyright (c) 2025-2026 ...` |

### Collection/Data Structure Layout

For classes implementing `IList<T>`, `ICollection<T>`, etc.:
// 1. Public core API methods (no region) - prominently visible at top
public void Insert(int index, T item) { }
public bool Remove(T item) { }

// 2. Private helper methods (no region)
private void CopyToArray(...) { }

// 3. #region for interface glue code only
#region Implementation of IList<T>
bool ICollection<T>.IsReadOnly => false;
void ICollection<T>.Add(T item) { }
public struct Enumerator { }
#endregion
---

## Commit Messages

> [!IMPORTANT]
> Follow the **Conventional Commits** specification for all commit messages.

### Format
<type>(<scope>): <subject>

<body>- **Header line is mandatory** (max 72 characters)
- **Body is optional** but required for non-trivial changes
- **Type and scope** MUST be lowercase
- **Subject** MUST start with capital letter and use imperative mood
- **Body** wraps at 72 characters

### Commit Types

| Type | Meaning |
|------|---------|
| `feat` | New user-visible feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `refactor` | Code restructuring without behavior change |
| `test` | Adding/modifying tests |
| `perf` | Performance improvement |
| `style` | Formatting only |
| `chore` | Tooling, build, CI, dependencies |
| `revert` | Reverts a previous commit |

### Common Scopes

**Core:** `api`, `auth`, `core`, `data`, `health`, `openapi`  
**Infrastructure:** `build`, `ci`, `deps`, `docker`, `github`, `tools`  
**Docs:** `architecture`, `deployment`, `development`, `docs`, `features`, `guides`, `roadmap`  
**Features:** `cors`, `errors`, `https`, `logging`, `proxy`, `security`, `system`, `ui`, `validation`

> [!TIP]
> See [docs/development/commit-message-guidelines.md](../docs/development/commit-message-guidelines.md) for complete reference.

### Breaking Changes

Mark breaking changes with `!` suffix:
feat(auth)!: Change token response structure

BREAKING CHANGE: Response field "token" renamed to "accessToken".
Clients must update their JSON parsing logic.
### Example
test(core): Add unit tests for ExceptionHelpers.PrepareForRethrow

Introduces comprehensive test coverage for the PrepareForRethrow
method, including verification of stack trace preservation and proper
exception unwrapping from AggregateException.
