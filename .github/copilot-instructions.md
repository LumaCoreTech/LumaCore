# Copilot Instructions

## Table of Contents

1. [General](#general)
   - [Repository Constraints](#repository-constraints)
   - [Third-Party Notices](#third-party-notices)
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
   - [Common Test Topics](#common-test-topics)
   - [Coverage](#coverage)
5. [Data Layer / EF Core](#data-layer--ef-core)
   - [Entity Documentation](#entity-documentation)
   - [Entity Layout](#entity-layout)
   - [DbContext Configuration](#dbcontext-configuration)
   - [Migrations](#migrations)
   - [Data Layer Guidelines](#data-layer-guidelines)
   - [Service API Conventions](#service-api-conventions)
6. [Privacy & Data Minimization](#privacy--data-minimization)
7. [Logging & Configuration](#logging--configuration)
8. [Code Layout & Naming](#code-layout--naming)
9. [Commit Messages](#commit-messages)
10. [Member Ordering](#member-ordering)

---

## General

### Repository Constraints

> [!IMPORTANT]
> **Target Framework:** .NET 10

- Do **not** perform any **write** operations using Git (e.g., commit, push, reset, rebase, merge, tag, branch creation/deletion).
- Localization is only active in the UI (Blazor) — API responses and validation messages remain in **English**.

### Third-Party Notices

When **adding, removing, or updating** a third-party dependency (NuGet package, npm package, bundled JS library, or any other external asset), **always update `THIRD-PARTY-NOTICES.md`** in the repository root accordingly:

- **Adding:** Insert a new entry with Author, License, and URL.
- **Removing:** Delete the corresponding entry.
- **Updating:** Adjust the entry if the license or author changed (version bumps with no license change need no update).

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
- **Comment non-obvious decisions.** When a choice isn't immediately self-evident (empty templates, suppressed defaults, workarounds, defensive guards, intentional no-ops), add a comment explaining *why* — not *what*. If someone might later ask "why was this done this way?", a comment is mandatory.
- When you notice opportunities for improvement (refactoring, better patterns, code quality):
  - **Explicitly mention them** and start a dialog.
  - After user approval, improvements can be implemented.

> [!NOTE]
> When a task is explicitly about repository instructions (like this file), it is OK to update instruction files immediately without waiting for an explicit start signal.

### XML Documentation

- Add complete XML documentation (`<summary>`, `<param>`, `<returns>`, `<exception>`) for **all** methods and properties you add or change, including private and internal members.
- Use `<remarks>` for additional details, `<example>` for usage samples when helpful.
- XMLDocs should be written for **all members**, not just public APIs — internal and private members are important for developers.
- **Use the full 120-character line width** in XML documentation. Break at logical points (end of sentence) rather than at arbitrary positions.
- For `cancellationToken` parameters, use the standard phrasing: `A token to cancel the operation.` Context-specific phrasings are allowed as exceptions.

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
- **Keep public XMLDoc consumer-focused.** Public `<summary>`, `<remarks>`, and `<example>` must describe *what* the type/member does for its consumers — not *why* it is implemented a certain way. Implementation rationale (e.g., "attributes must be on properties because the validation filter reads them") belongs in **code comments**, not in XMLDoc visible to API consumers.
  - **Observable behavior IS contract** and must stay in public docs. If the consumer can see, measure, or react to a consequence, document it — even if it sounds implementation-flavored. Examples that **must** appear in public XMLDoc:
    - **Side effects** ("a file written to disk during the unit of work remains on disk after the rollback completes if a compensation fails").
    - **Process termination** ("aborts the process immediately rather than risk silently corrupting data") — describe the consequence, not the mechanism (do **not** name internal helpers like `FailFast.TerminateApplication`).
    - **Ordering guarantees** ("compensations run in LIFO order"), **best-effort vs. all-or-nothing semantics**, **cancellation policy** (honoured / pre-flight only / not honoured), and **state transitions** the caller can observe.
  - **Mechanism stays out of public docs.** Internal types, private field names, counter bookkeeping, why a `List<T>` was chosen over a `Dictionary<TKey, TValue>`, etc., belong in code comments or in XMLDoc on private/internal members. The litmus test: *"Could the consumer act differently based on this information?"* If yes, it is contract; if no, it is mechanism.
  - **`<inheritdoc/>` warning:** `<remarks>` and `<exception>` on a method marked `<inheritdoc/>` are aggregated into the inherited public documentation by most doc-generation tools. Do **not** put implementation rationale in `<remarks>` on `<inheritdoc/>` methods — move it to body comments. Define the public contract (including all observable consequences) on the **interface or base class**, then let the implementation inherit it cleanly.

#### `<see cref="..."/>` Brevity

Omit the parameter list in `cref` attributes when the method has **no overloads** — the compiler resolves it unambiguously, and the tooltip stays short:

```xml
<!-- ✅ Single overload — omit parameters for a clean tooltip -->
<see cref="WriteTableAsync"/>

<!-- ❌ Unnecessary parameter list — tooltip explodes -->
<see cref="WriteTableAsync(TableSnapshot, ILogger, IProgress{Int32}, Int32, Int32, CancellationToken)"/>
```

When **multiple overloads** exist and disambiguation is required, the full parameter list is unavoidable — the compiler demands it. Accept the longer tooltip in those cases; do **not** use display-text overrides (`<see cref="...">short name</see>`) because they introduce staleness risk on renames (the `cref` warns, the display text silently stays wrong).

#### Tag Ordering

Tags must appear in this order: `<summary>` → `<typeparam>` → `<param>` → `<returns>` → `<exception>` → `<remarks>` → `<example>`.

#### Exception Tag Style

Use **bare condition** style — do **not** prefix with "Thrown when" or "Thrown if". The `<exception>` tag already implies "thrown when", making the prefix redundant (same principle as `<returns>` not starting with "Returns").

```xml
<!-- ✅ Correct: bare condition -->
/// <exception cref="ArgumentNullException">
/// <paramref name="value"/> is <see langword="null"/>.
/// </exception>

<!-- ❌ Wrong: redundant "Thrown when" -->
/// <exception cref="ArgumentNullException">
/// Thrown when <paramref name="value"/> is <see langword="null"/>.
/// </exception>
```

#### `OperationCanceledException` Convention

Do **not** document `OperationCanceledException` for methods that accept a `CancellationToken` — this is an implicit contract of the parameter itself (same BCL convention as not documenting `OutOfMemoryException`).

**Do** document it when the cancellation behavior is **non-obvious**:

| Document? | Scenario | Example |
|---|---|---|
| ❌ No | Standard `CancellationToken` pass-through | Import/export methods, data service methods |
| ✅ Yes | Method throws it **without** a `CancellationToken` parameter | `FailFastCanceledException` |
| ✅ Yes | Cancellation races with another signal and the **priority matters** | "Token canceled before the event is signaled" |
| ✅ Yes | Cancellation has **observable side effects** (partial state) | Document in `<remarks>`, not `<exception>` |

### Core Principles

| Principle | Meaning |
|-----------|---------|
| **Verify, don't assume** | Inspect actual implementation before stating how something works |
| **Code > Comments** | Trust what the code does over what comments claim |
| **Ask > Assume** | When unclear, ask instead of guessing |
| **Quality > Speed** | Take time to be thorough; rushing causes false positives |
| **No implicit assumptions** | A class must be self-contained and correct regardless of external operating conditions |

#### No Implicit Assumptions About Operating Conditions

Classes must never silently rely on external context (e.g., "this only runs during shutdown", "the caller always disposes first"). Either:

1. **Implement defensively** — handle all code paths (including edge cases) so the class behaves correctly regardless of how or where it is called.
2. **Document and assert** — if an assumption is truly required, document it explicitly in XMLDoc and enforce it with a guard/assert.

> [!WARNING]
> Implicit assumptions are technical debt that causes hard-to-diagnose bugs when someone changes the calling code months later. Every exception handler, state transition, and resource cleanup must leave the object in a consistent state — **unconditionally**.

### Code Patterns

> [!IMPORTANT]
> These patterns are **mandatory**. For full details, see [Coding Standards](../docs/development/coding-standards.md).

#### Async/Await

- **Use `ConfigureAwait(false)` on every `await` in library code** to prevent deadlocks and keep code portable.
- **Exception:** Blazor components using `IJSRuntime` must use `ConfigureAwait(true)` (or omit it).
- **Test projects** follow a two-tier rule ([xUnit1030](https://xunit.net/xunit.analyzers/rules/xUnit1030)):
  - `[Fact]`/`[Theory]` **test methods** — do **not** use `ConfigureAwait(false)` (xUnit controls scheduling via `SynchronizationContext`).
  - **Test infrastructure** (harnesses, helper classes, `IDatabaseTestOperations` implementations) — **do** use `ConfigureAwait(false)`, same as production library code. `ConfigureAwait(false)` inside a helper does not propagate up the call stack; the test method's own `await` still resumes on the xUnit context.
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

#### Disposal

Disposal follows a **three-tier model** to balance `ConfigureAwait(false)` consistency with test ergonomics:

| Context | Pattern | Rationale |
|---|---|---|
| **Library / production code** | `try/finally` + `DisposeAsync().ConfigureAwait(false)` | Ensures `ConfigureAwait(false)` on the disposal `await`. |
| **Test infrastructure** (harnesses, helpers) | `try/finally` + `DisposeAsync().ConfigureAwait(false)` | Infrastructure is library-like code — same rule. |
| **Test methods** (`[Fact]` / `[Theory]`) | `await using` allowed | `ConfigureAwait(false)` is forbidden in test methods (xUnit1030), so `try/finally` adds no value — it becomes pure boilerplate. `await using` is concise and safe here. |

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
| `Validate()` (Options) | `Validate_AllowedOrigins_WhenEmpty_Fails` |

**`Validate()` tests** for `IValidatableObject` options classes use a **4-part** name: `Validate_<Property>_<Condition>_<Expectation>`. The `<Property>` segment identifies which property the test targets. Cross-cutting tests that exercise the overall validation gate (e.g., disabled feature, fully configured options) may omit the property segment and use the standard 3-part name.

#### One Member per Test Method

- Each test method must test **exactly one** target member (method, property, or constructor overload).
- Do **not** consolidate tests for different members into a single `[Theory]` test.
- Different constructor overloads count as **separate members** and require their own dedicated test methods.
- A `[Theory]` with `MemberData` is appropriate when testing the **same member** with different input values, not when testing different members.

#### Prefer Meaningful Unit Tests

- Implement unit tests that validate meaningful behavior and invariants — not trivial or tautological tests.
- Avoid tests that only restate implementation details without asserting observable behavior.

#### AAA Pattern

Each test must follow the AAA pattern with inline comments:

```csharp
// Arrange
var sut = new MyClass();

// Act
var result = sut.DoSomething();

// Assert
Assert.Equal(expected, result);
```

**Special cases:**
- If Act and Assert coincide (e.g., `Assert.Throws(...)`), use: `// Act + Assert`
- For exception tests: `var ex = Assert.Throws<...>(() => ...)` then assert on `ex`.
- **Always verify observable exception state** — asserting only the exception type is insufficient. Capture the exception and assert on its domain-relevant properties:

| Exception type | Property to assert |
|---|---|
| `ArgumentException` (and derived types) | `ParamName` |
| `ObjectDisposedException` | `ObjectName` |
| `InvalidOperationException` (custom message) | `Message` (exact match) |
| Any exception with a **custom** domain-specific message | `Message` (exact match) |

> [!WARNING]
> **Always assert on `Message` precisely** when the message is a **custom string set by production code**. Verifying only the exception type is insufficient — the message carries domain intent.
>
> **Never assert on default BCL exception messages** (e.g., `"Value cannot be null."` from `ArgumentNullException`, `"The value cannot be an empty string..."` from `ArgumentException.ThrowIfNullOrWhiteSpace`). These messages are **localized** by .NET satellite assemblies and will fail on non-English systems — especially German dev machines where Visual Studio installs language packs automatically.

#### Semantic Inline Comments for Complex Tests

For complex tests (multi-step scenarios, fault injection, OS-specific behavior, cross-component interactions), add inline comments that explain **why** something is done, not **what** the code does. Focus on:

| Area | Example |
|---|---|
| **Detection mechanisms** | Explain that `FileShare.None` is how `CleanupOrphanedFolders()` distinguishes active from orphaned folders |
| **Execution order in fault-injection** | Which step succeeds before the fault fires (e.g., "Directory.Delete already ran, then the monitor intercepts File.Delete") |
| **Logger setup** | Why `logger.Entries.Clear()` is needed (clearing constructor-time entries to isolate explicit call output) |
| **Timing dependencies** | Why setup must happen before/after a specific call (e.g., "orphans must exist before manager construction because the constructor triggers cleanup") |
| **Cross-references** | Link to counterpart tests that verify the same scenario from a different perspective |
| **Dead code in setups** | If a variable is created only as scaffolding (not part of the assertion), explain its purpose — or remove it |

#### Staged Approach for Test Implementation

When implementing tests (especially for complex classes), use a staged approach:

| Stage | Focus | Checkpoints |
|-------|-------|-------------|
| **1. Implementation** | Functionality, test logic, AAA pattern | Compiles, tests green |
| **2. Coverage Review** | Run Coverlet, identify gaps, close with targeted tests | 100% coverage |
| **3. Structure** | Test ordering (Valid → Invalid), `#region` | Order correct |
| **4. XMLDocs** | `<see cref=""/>`, `<paramref/>`, `<see langword=""/>` | All tags correct |

Coverage review comes **before** structure/XMLDocs to avoid rework — once all tests exist, structure and documentation can be finalized in one pass.

#### XML Documentation

- Every test method must have complete XMLDocs.
- For `[Theory]` tests, include `<param>` for each method parameter.

#### Assertions

- Validate the **complete expected state** of the object — prefer exact string matches over substring checks.
- For data structures with multiple observable properties, use state verification helpers to ensure all properties are checked consistently.
- **Verify all elements in small collections** — when a list/array has a known, small number of elements (e.g., 3–10), assert on **every** element. Checking only the first element or the count alone is insufficient — it leaves uncertainty about unchecked elements.
  - For large collections (e.g., chunk-sized datasets), verifying count + first + last element is acceptable.
- Test both:
  - the *positive expectation* (what must happen)
  - and a *negative expectation* (what must **not** happen), to rule out side effects.

#### Strongest Assertion Principle

> [!WARNING]
> **Always use the strongest assertion the scenario allows.** Weak assertions that merely confirm existence or non-emptiness when the exact expected state is known are considered defects in the test — they give false confidence without catching regressions.

| ❌ Weak (avoid) | ✅ Strong (prefer) | When |
|---|---|---|
| `Assert.NotEmpty(collection)` | `Assert.Single` + `Assert.Equal` | Exactly 1 element expected |
| `Assert.Contains(item, collection)` | `Assert.Single` / `Assert.Equal` | Collection size is known |
| `Assert.NotNull(x)` alone | `Assert.NotNull` + property assertions | Object has observable state |
| `Assert.True(x > 0)` | `Assert.Equal(expected, x)` | Exact value is known |
| `Assert.Contains("substring", text)` | `Assert.Equal(exactExpected, text)` | Full string is deterministic |
| `Assert.True(x)` / `Assert.False(x)` | Typed assertion (`Assert.Equal`, `Assert.Single`, etc.) | A more specific overload exists |

The guiding question: *"Do I know the exact expected value?"* If yes, assert it exactly. If the test only checks *part* of a known value, future regressions in the unchecked part go undetected.

#### State Verification Helpers

For data structures with multiple observable properties, create a helper method:

```csharp
private static void AssertDequeState<T>(
    Deque<T> deque, int expectedCount, int expectedCapacity, T[] expectedElements)
{
    Assert.Equal(expectedCount, deque.Count);
    Assert.Equal(expectedCount == 0, deque.IsEmpty);
    Assert.Equal(expectedCapacity, deque.Capacity);
    Assert.Equal(expectedElements, deque.ToArray());
}
```

Place helpers in `FooTests.Helpers.cs`.

#### Theory-First Planning

> [!IMPORTANT]
> Plan `[Theory]` vs. `[Fact]` **before** writing test code. Do **not** create individual `[Fact]` methods first and consolidate later.

- **Plan consolidation up front:** Group planned scenarios by their Act+Assert pattern. If multiple scenarios share the **same** Act call and Assert structure (differing only in inputs and expected outputs), write them as `TheoryData` rows from the start.
- When multiple edge cases follow the same Arrange/Act/Assert structure and only differ in input/output combinations, prefer consolidating them into a single `[Theory]` with `MemberData` / `TheoryData`.
  - Keep each scenario as its own data row.
  - Prefer adding a `caseName`/`scenario` parameter (string) as the first data element to keep test output readable.
  - Always add a short comment per data row describing what is being tested (what branch/special case the row is meant to cover).
  - The goal is to reduce boilerplate while preserving **line and branch coverage**.
  - Do not over-generalize: keep dedicated `[Fact]` tests when a scenario needs special setup/teardown, distinct assertions, or improved readability.
- Prefer `TheoryData<…>` over `IEnumerable<object[]>` for type safety.
- **Test data placement:** When a `TheoryData` / `MemberData` method lives in the **same file** as its test, place it **before** the test method that references it. If test data grows too large, move it to a dedicated `FooTests.Bar.TestData.cs` file (see [Organization Model](#organization-model)).

#### Test Models

- Minimize the number of test-only model types. Do not create a dedicated model for a scenario that an existing model already covers.
- Each test model must have XMLDocs explaining its purpose and which test scenario it supports.
- Place test models in a dedicated `FooTests.TestModels.cs` partial file.

### Test Structure

#### Organization Model

| File Type | Content |
|-----------|---------|
| `FooTests.cs` | Anchor file: `partial class`, `[Trait(...)]`, optionally trivial property tests |
| `FooTests.Construction.cs` | Constructor and factory method tests |
| `FooTests.Bar.cs` | Tests for method `Bar()` |
| `FooTests.Bar.TestData.cs` | Test data (`MemberData`, `TheoryData`) for `Bar()` tests |
| `FooTests.Properties.cs` | All property tests, each in `#region` |
| `FooTests.Comparison.cs` | `Equals()`, `GetHashCode()`, equality/relational operators, `CompareTo` |
| `FooTests.Helpers.cs` | Helper methods (factory, setup, assertion helpers) |
| `FooTests.TestModels.cs` | Test-only data models |

**Splitting guideline:** Create a separate file when a feature scope has **5 or more** tests. Scopes with fewer than 5 tests should be placed in the **anchor file** (`FooTests.cs`) using `#region` blocks. Avoid one-file-per-method granularity — it fragments navigation without adding clarity.

#### Test Ordering (within a file)

When a test file contains tests for **exactly one** method/property, order test methods top-to-bottom as:

1. **Valid scenarios** — typical usage, boundary conditions, edge cases that produce expected results
2. **Invalid scenarios** — contract violations, invalid inputs that throw exceptions

To reduce ambiguity when classifying tests:

- **Valid scenarios**: Any test where the method behaves correctly for the given input, including:
  - Typical usage scenarios (e.g., "filter matches" → `true`)
  - Expected negative outcomes (e.g., "filter does not match" → `false`)
  - Boundary conditions and edge cases (e.g., empty collections, default values, type mismatches)
- **Invalid scenarios**: Contract violations and invalid inputs that are expected to throw (e.g., null argument guards).

If a file contains tests for **multiple** methods/properties, group them using `#region` blocks (one region per method/property), and apply the same ordering *within* each region.

When adding new tests, immediately insert them in the correct position. Do not append out of order even temporarily.

#### Options / Validation Tests

For `IValidatableObject` options classes whose `Validate()` method checks multiple properties, **group tests by the property or concern being validated** instead of a flat valid/invalid split:

1. **General** — cross-cutting tests (disabled feature skips validation, fully configured options pass).
2. **One section per property** — ordered to match the `Validate()` implementation flow.
3. **Valid → invalid** within each section.

Use numbered section dividers (`// --- 1. General ---`, `// --- 2. AllowedOrigins ---`, …) and a file-level narrative comment that lists all sections. The narrative should reference test method name suffixes (the condition part) in parentheses so readers can locate each test by scanning the overview (e.g., `wildcard accepted (WhenWildcardAndNoCredentials), empty origins rejected (WhenEmptyAndEnabled)`).

This structure keeps related scenarios together and makes it easy to verify that each property has both positive and negative coverage.

#### Narrative Structure (Test Files)

Test files whose tests follow a **multi-step progression or escalation path** should tell a coherent story. Readers opening a 500-line test file need a map — not a random collection of methods. (The 5-test splitting guideline is a useful signal, but the real trigger is *narrative content* — a file with 8 independent property tests needs no escalation story.)

**File-level narrative comment** — Place a block comment directly above the `partial class` declaration that:
- Names the theme in one line.
- Lists the numbered sections with a one-line summary each, referencing test method name suffixes in parentheses.
- Ends with a cross-reference to related test files if the subject is split across partials.

```csharp
// Connection lifecycle: from first connect through repeated failures.
//
// These tests follow the connection from its initial handshake to the edge cases
// that arise when things go wrong — and how it recovers (or doesn't):
//
//   1. Happy path: connect, authenticate, ready (ConnectsSuccessfully).
//      Connecting twice on the same instance is idempotent (CalledTwice).
//
//   2. Configuration gates: missing credentials → ConfigurationRequired.
//      No retry — the operator must fix the configuration.
//
//   3. Error classification: transient errors → auto-retry;
//      permanent errors → manual intervention.
//      ...
//
// For reconnection-specific scenarios, see Reconnection.
public sealed partial class ConnectionManagerTests
```

**Section dividers** — Between groups of related tests, insert a numbered divider comment:

```csharp
// --- 1. Happy path: connect and idempotency ---
```

The number matches the file-level overview. The title is short and descriptive. When inserting a new section, renumber subsequent dividers and update the file-level overview to match.

**Ordering** — Tests within a file should follow an **escalation path** or **logical story** (e.g., simplest success → configuration gates → transient errors → permanent failures → double failures). The valid → invalid ordering rule (see above) still applies *within* each numbered section.

**Inline narrative comments** — When one test is the logical counterpart or continuation of another, say so in the Arrange comment (e.g., "same schema as the previous test, but this time we inject a timeout instead of an I/O error").

**Cross-file narrative (partials)** — When a test class is split across multiple partial files:
- The **anchor file** (`FooTests.cs`) carries the overall plot: a numbered `<list>` in XMLDoc that maps each partial file to its chapter in the story, plus a "Reading order" paragraph.
- Each **partial file** has its own file-level narrative for its internal sections, plus cross-references to neighbor files and the anchor.
- Keep cross-references to **file suffixes only** (e.g., "see StartAsync()", "see Reconnection"), not to specific sections or line numbers — suffixes are stable across refactors.

#### `#region` Usage

- **Avoid** `#region` for "Happy Path" vs "Error Cases" grouping — test ordering handles this.
- **Avoid** redundant `#region` in files that contain tests for **exactly one** member and are already clearly named.
- **Always use** `#region` when a single test file contains:
  - **Multiple method overloads** (e.g., `Ignore()` and `Ignore<T>()`)
  - **Multiple properties** in `FooTests.Properties.cs`
  - **Multiple distinct methods** being tested in the same file

Example:

```csharp
#region Ignore()
// Tests for non-generic Ignore(Task)
#endregion

#region Ignore<T>()
// Tests for generic Ignore<T>(Task<T>)
#endregion
```
#### Async Tests & Deadlock Prevention

Tests use a **two-tier timeout strategy**:

1. **Global timeout (10 seconds)** — Configured in `xunit.runner.json` as a safety net for unexpected hangs
2. **Explicit timeout helper** — For intentional waits where precise error messages help debugging

> [!IMPORTANT]
> Use `AwaitWithTimeoutAsync` **only for explicit waits** — operations where you intentionally wait for something.

```csharp
// Add to test file:
using static LumaCore.TestUtilities.Async.AsyncTestHelpers;
```

**When to use the timeout helper:**

| Operation | Use Helper? | Example |
|-----------|-------------|---------|
| `await are.WaitAsync()` | ✅ Yes | Explicit wait on async primitive |
| `await Assert.ThrowsAsync(() => waitTask)` | ✅ Yes | Waiting for task to throw |
| Synchronous `.Wait()` on primitive | ✅ Yes | Wrap in `Task.Run` + helper |
| `queue.Enqueue()`, `are.Set()` | ❌ No | Not intentional waits — global timeout covers |
| Property access, assertions | ❌ No | Not intentional waits — global timeout covers |

**Examples:**

```csharp
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
```

The helper is defined in `src/LumaCore.TestUtilities/Async/AsyncTestHelpers.cs`:
- `AwaitWithTimeoutAsync(Task, message?, timeout?)` — for void tasks
- `AwaitWithTimeoutAsync<T>(Task<T>, message?, timeout?)` — for tasks with results
- Default timeout: 1 second

#### Refactoring Tests

- Ensure **all existing test cases remain present**.
- **Preserve test count:** Run tests before and after refactoring. The discovered test count must remain unchanged. If it changes, explain which cases were added/removed/merged.
- **Do not** delete and regenerate — only move/copy existing blocks.
- Prefer consolidating valid-scenario cases into existing `Theory`/`MemberData` tests instead of adding new `Fact` tests.

#### Integration Tests

Integration tests verify the **interaction between multiple components** rather than a single member in isolation. Because they are inherently cross-cutting, the unit-test rules above are relaxed:

| Rule | Unit tests | Integration tests |
|------|-----------|-------------------|
| One member per test | **Required** | **Relaxed** — a test may exercise a workflow spanning multiple types |
| `Method_State_Expectation` naming | **Required** | **Relaxed** — use `Scenario_Expectation` (e.g., `Roundtrip_WriteAndRead_PreservesAllData`) |
| `FooTests` class naming | Mirrors the SUT type | Use descriptive names: `*RoundtripTests`, `*IntegrationTests`, `*EndToEndTests` |
| Organization model (`FooTests.Bar.cs`) | **Required** | Optional — keep all tests in one file unless the file grows large |

**When to write an integration test instead of (or in addition to) a unit test:**

- The behavior under test only emerges from the **cooperation** of two or more components (e.g., writer → reader roundtrip).
- Mocking the collaborator would remove the very thing you want to verify (e.g., SQLite format compatibility).

**Trait annotation:** Mark integration tests with `[Trait("Category", "<scope>")]` (e.g., `"DataPort"`, `"Initialization"`) so they can be filtered in CI.

### Common Test Topics

#### Equality / GetHashCode

- When testing `GetHashCode()`, always compare **multiple equal instances**.
  - Rationale: this detects accidental reference-based hash implementations.
  - Example: create `a` and `b` with the same logical contents and assert `a.Equals(b)` and `a.GetHashCode() == b.GetHashCode()`.
- Additionally, test that `GetHashCode()` is stable across multiple calls on the same instance.

#### Comparison / Ordering

- Do **not** write tests that merely exercise .NET framework sorting infrastructure (`List<T>.Sort()`, `Array.Sort()`, LINQ `OrderBy`) as a wrapper around an already-tested `CompareTo` / `IComparable` implementation. Such tests do not cover additional production code paths.
- Focus comparison tests on the mathematical properties of the ordering:
  - **Reflexivity**: `a.CompareTo(a) == 0` (same instance).
  - **Antisymmetry**: if `a < b` then `b > a` (verify both directions inline).
  - **Transitivity**: if `a < b` and `b < c` then `a < c` (requires 3+ elements — use dedicated `[Fact]` tests).
- When a type implements multiple comparison-related members (`Equals`, `==`, `!=`, `CompareTo`, `<`, `>`, `<=`, `>=`), prefer a **single shared `TheoryData`** dataset containing `(scenario, valueA, valueB, expectedSign)` rows that all members consume. This validates that all members agree on the same ordering.
  - Use `expectedSign = -1` for ordered pairs and `expectedSign = 0` for equal pairs.
  - Each Theory verifies **both directions** (A vs B and B vs A) to cover antisymmetry / commutativity inline — no need for separate `+1` rows.
- Assert on `Math.Sign(result)` rather than exact `CompareTo` return values. The `IComparable<T>` contract only guarantees the **sign**, not a specific value like `-1`/`0`/`1`.
- Prefer `TheoryData<…>` over `IEnumerable<object[]>` for type safety.

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

#### Abstract Base Classes

- Prefer testing via concrete derived classes when that provides meaningful coverage.
- If the abstract base class is part of the public API and its behavior cannot be adequately exercised through existing derived types, add **direct tests** using a minimal test-only derived type.
- Prefer tests that are explicitly written for the type/member under test — avoid relying on incidental/indirect coverage only.

#### Test Data

- Prefer neutral English test data strings/examples in tests.

#### Unit Tests

- Do **not** use `await using` for the **SUT** in unit tests — if the implementation is buggy, `DisposeAsync()` may hang, causing the test to hang instead of failing with a clear error message.
- When asserting exception messages in tests, prefer exact checks (`Assert.Equal`) over substring checks (`Assert.Contains`). When the message contains dynamic parts (e.g., file paths), use `Assert.Matches` with a regex that validates the full message structure while tolerating the dynamic segments.

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
| 1 | `ToTable(...)` and the composite `HasKey(...)` |
| 2 | First FK + Navigation (grouped) |
| 3 | Second FK + Navigation (grouped) |
| 4 | Timestamps |
| 5 | Other properties — enums, flags, etc. |

#### Section Markers

To make the section boundaries visible in the source — especially when timestamps are pushed downward
or when sections are skipped entirely — every entity class **and** the corresponding `Configure*()` method
in `LumaCoreDbContext` carries inline section-divider comments.

**Style:**

| Case | Marker style | Example |
|---|---|---|
| Section has members | Numbered, using the canonical numbers from the layout table above | `// --- 4. Timestamps ---` |
| Section is empty | Numbered placeholder with `(none)` suffix — preserves the visual sequence and signals intent | `// --- 2. Public identifier (none) ---` |

**Always emit the full canonical sequence** — including Section 1. Skipping markers (even for single-section
entities or empty sections) breaks the side-by-side correspondence between the entity class and its
`Configure*()` counterpart and creates ambiguous gaps that read as oversights rather than deliberate omissions.

A `(none)` placeholder marker has no body — it sits between the surrounding sections as a single comment
line with a blank line above and below, just like a populated marker. This makes "Section 2 deliberately
empty" instantly readable instead of requiring the reader to count.

**Placement:**

- **Entity class:** marker goes directly above the XMLDoc of the section's first member. Empty-section
  placeholders sit between the neighboring sections on their own.
- **`Configure*()` method:** marker goes directly above the section's first `Property(...)` /
  `HasOne(...)` / `HasIndex(...)` call. **Section 1 is marked too** — its marker goes at the very top of
  the lambda body, above `ToTable(...)`. `ToTable` / `HasKey` / `Property(Id)` all belong to Section 1
  because the table mapping is part of *how the primary key is persisted* (table + key form one
  storage-identity unit). To make this explicit in the marker text, Section 1 is titled
  **`Table mapping & primary key`** in `Configure*()` methods for standard entities, and
  **`Table mapping & composite key`** for join entities (where the composite key is declared via
  `HasKey(e => new { ... })` rather than a single `Id` property). Entity classes keep the shorter
  **`Primary key`** title since they have no `ToTable` equivalent.

#### Classification Rules

To remove ambiguity when deciding which section a property belongs to:

- **Single-entity reverse navigations** (`T?` properties whose foreign key column lives on the *other*
  side of the relationship — e.g. `UserEntity.Preferences`, `MessageEntity.GenerationMetadata`,
  `ParticipantEntity.User` / `Persona`) are still single-entity navigations and belong to **Section 3**.
  The decisive question is the property type, not where the FK column lives (same rule as the full-fat
  include guidance under [Read APIs](#read-apis)).

- **Polymorphic foreign keys without a CLR navigation** (e.g. `ResourceReferenceEntity.OwnerKind` +
  `OwnerId`) belong to **Section 3**. They behave like FK + Nav pairs at the persistence level even
  though no `T?` navigation property exists, and grouping them with Section 5 scalars would obscure
  their role as the entity's primary join column.

- **Audit timestamps** (`CreatedAtUtc`, `UpdatedAtUtc`, `JoinedAtUtc`, `AssignedAtUtc`, `AppliedAtUtc`,
  `RevokedAtUtc`, `LastLoginAtUtc`, `LastTokenRefreshAtUtc`, `LastRunAtUtc`) belong to **Section 4**.
  They describe *when something happened to the row itself*.

- **Domain timestamps** (`RedactedAtUtc`, `ExpiresAtUtc`) belong to **Section 5** alongside the scalar
  fields they semantically pair with (e.g. `RedactedAtUtc` next to `RedactionReason`, `ExpiresAtUtc`
  next to the JWT payload columns). They describe *the data*, not the row's lifecycle.

### DbContext Configuration

The `LumaCoreDbContext` follows two distinct ordering conventions — **alphabetical** for the top-level
`Configure*()` calls in `OnModelCreating`, and the **Entity Layout order** (see above) for property/relationship
calls *inside* each `Configure*()` method.

#### `Configure*()` Method Calls in `OnModelCreating` — Alphabetical

Keep the call list alphabetical and matching the physical method ordering in the file:

- No domain-grouping debates — entities are independent, so any grouping would be arbitrary.
- New entities slot in trivially.
- A single `Configure*()` method is easy to locate by name.

#### Inside Each `Configure*()` — Mirror the Entity Layout

The `entity.Property(...)`, `entity.HasIndex(...)`, and `entity.HasOne(...)` calls inside a `Configure*()`
method follow the **same ordering as the entity class itself** (see [Entity Layout](#entity-layout)):

1. `ToTable(...)` and `HasKey(...)` first (Section 1 — *Table mapping & primary key*).
2. `Property(Id).ValueGeneratedOnAdd()` (or `ValueGeneratedNever()` for singletons).
3. `Property(PublicId)` (if present).
4. Foreign-key `Property(...)` calls — grouped with their corresponding `HasOne(...)` if it improves locality.
5. Timestamps — `CreatedAtUtc` before `UpdatedAtUtc`.
6. Scalar domain fields — required before optional, matching the entity's property order.
7. `HasIndex(...)` calls — placed after the property they reference, or grouped at the end for composite indexes.
8. Remaining `HasOne(...)` / `HasMany(...)` relationships — at the end.

**Why mirror the entity?** Side-by-side reading: opening the entity class and the `Configure*()` method shows the
same field order, so it is immediately obvious which property maps to which configuration call. Alphabetical
ordering would destroy this correspondence (e.g., `BaseUrl` would jump in front of `CreatedAtUtc`).

> [!NOTE]
> Pure alphabetical ordering inside `Configure*()` is **not** wanted — it loses the semantic correspondence to
> the entity class. The Entity Layout convention transports meaning (PK → identity → FKs → timestamps → domain),
> alphabetical ordering does not.

### Migrations

Create a **new migration** for each schema change using `dotnet ef migrations add <Name>`. **You may fold schema changes into existing migrations** instead of creating new ones, as the DB is not distributed, yet.

### Data Layer Guidelines

- Control persisted-data pruning/policy via options in the data layer.
- Place service-like infrastructure under `LumaCore.Data.Services`.
- Track login and token refresh timestamps, but avoid `LastActivity` updates on every request.
- Usernames: store as entered, but lookups are case-insensitive.
- Ensure that the nullable email unique index preserves the semantic 'allow multiple missing emails but enforce uniqueness for actual email values' across supported database providers.

### Service API Conventions

Data service APIs (`IUserDataService`, `IPersonaDataService`, `IRoleDataService`, etc.) follow these conventions to keep the surface predictable, the EF Core change tracker contained, and Compiled Queries effective.

#### Method Categories

Every method in a data service interface (and its implementing partial class) belongs to exactly one of four categories. The category determines section placement, ordering, and naming.

| Category | Returns | Examples | Section header |
|---|---|---|---|
| **Read API** | Entity (or list of entities) with full-fat includes | `GetPersonaByPublicIdAsync`, `GetAllRolesAsync`, `ListConversationsByParticipantAsync` | `#region Read APIs` |
| **Projection API** | Scalar, ID, dictionary, sub-entity, or non-entity DTO | `GetRoleNamesByUserIdAsync`, `GetParticipantCountsAsync`, `GetCurrentSystemPromptAsync`, `GetPersonaIdsWithAvatarAsync`, `GetAvatarAsync`, `GetPreferencesJsonAsync`, `GetModelEndpointCredentialsAsync`, `ListConversationIdsWithNoUsersAsync` | `#region Projection APIs` |
| **Existence Check** | `bool` (yes/no, no data) | `EmailExistsAsync`, `UsernameExistsAsync`, `IsParticipantInConversationAsync`, `HasUserParticipantsAsync`, `UserHasRoleAsync`, `ModelEndpointExistsAsync` | `#region Existence Checks` |
| **Mutation API** | `bool`/`int`/freshly-loaded detached entity | `CreatePersonaAsync`, `UpdatePersonaAsync`, `DeactivatePersonaAsync`, `AssignRoleToUserAsync`, `RedactMessageAsync`, `CleanupConversationsWithNoUsersAsync` | `#region Mutation APIs` |

#### Method Ordering

- **Section order** is fixed: Read APIs → Projection APIs → Existence Checks → Mutation APIs. Reader flow goes from "common + harmless" to "rare + state-changing".
- **Within each section**, methods are ordered **alphabetically**. Deterministic placement avoids semantic disputes (e.g. "is Update before or after Delete?").
- **Interface and implementation must match exactly.** The order in `IFooDataService.cs` mirrors the order in `LumaCoreDataService.Foo.cs`.
- **Test files mirror the same order.** `LumaCoreDataServiceTests.Foo.cs` uses `#region` blocks per method in the same sequence.
- **Empty sections are omitted.** If a service has no Existence Checks (e.g. Personas), simply skip that `#region`.

#### Read APIs

- **No tracking, ever.** All Read APIs use `AsNoTracking()`. Returned entities are detached — callers cannot mutate-and-save them.
- **Full-fat includes.** Read APIs load all navigations a typical consumer is likely to need. Callers must not have to remember which navigations are populated. See the explicit definition below.
- **Return type for collections is `Task<IReadOnlyList<T>>`.** Signals "do not mutate" at the API boundary. Internal helpers and infrastructure (DataPort, providers, test fixtures) may use `Task<List<T>>`.
- **No tracking flag.** A `bool tracked = false` parameter is forbidden — it hides intent at the call site, doubles the Compiled-Query cache footprint, and tempts callers into ad-hoc mutation patterns.
- **No include selector.** A `[Flags] enum XxxIncludes` parameter is forbidden — the combinatorial explosion defeats Compiled Queries and obscures performance characteristics.
- **If a hot-path needs a leaner shape**, add a dedicated method with a clear suffix (e.g. `GetPersonaSummaryByPublicIdAsync` returning only `Participant`). Do *not* introduce shape flags on existing methods.

##### "Full-fat includes" — explicit definition

A Read API loads:

| Navigation property type | In full-fat? | Rationale |
|---|---|---|
| `T?` or `T` (single-entity navigation) | ✅ Yes, if typically needed | 0–1 additional row, indexed FK join, cheap |
| `ICollection<T>` (one-to-many) | ❌ No | Potentially unbounded, Cartesian explosion risk |

The **FK direction does not matter** — a one-to-one reverse navigation (e.g. `UserEntity.Preferences` where the FK lives on `UserPreferencesEntity.UserId`) is **also a single-entity navigation** and belongs in full-fat. The decisive question is the property type, not where the FK column lives.

**Depth limit**: maximum `Include(...).ThenInclude(...)` (2 levels). Deeper navigation requires a dedicated method.

**Cycle avoidance**: when `A → B` is loaded full-fat, **do not** also load `B → A`. EF Core would detect this but it adds zero value and confuses the contract.

**Intentional omissions** must be documented in the method's XMLDoc with a `<remarks>` paragraph explaining why (e.g. *"`MessageEntity.Conversation` is intentionally excluded because callers reach messages through their conversation context."*).

**Code-review heuristic**: look at the navigation property type. `ICollection<T>` → out. `T?` / `T` → in, if typically needed.

#### Projection APIs

- **Same `AsNoTracking()` rule** as Read APIs.
- **Return type is intentionally lean** — scalar, ID collection, dictionary, or single sub-entity. Never the full entity graph.
- **Naming**: still `GetXxxAsync` / `ListXxxAsync`, but the return type signals the projection (e.g. `Task<List<string>>` for `GetRoleNamesByUserIdAsync`, `Task<HashSet<PersonaId>>` for `GetPersonaIdsWithAvatarAsync`, `Task<SystemPromptEntity?>` for `GetCurrentSystemPromptAsync` because it returns a single sub-entity, not the parent).
- **Sub-entity returns** (e.g. `GetCurrentSystemPromptAsync` returning a `SystemPromptEntity` instead of the parent `PersonaEntity`) belong here, not in Read APIs — they expose only a slice of the parent's graph.
- **Same Compiled Query rules** as Read APIs apply: every projection that has a compiled query must be wired with the `PreferCompiledHotPathQueries` toggle.

#### Existence Checks

- **Return type is `Task<bool>`.** No data, just yes/no.
- **No `Include(...)`** — these queries should compile to `SELECT EXISTS(...)` or equivalent.
- **Naming**: `XxxExistsAsync`, `IsXxxAsync`, `HasXxxAsync`, `UserHasXxxAsync`. Always reads as a yes/no question.
- **Argument validation rules apply** (e.g. `ArgumentOutOfRangeException` for non-positive ids, same as elsewhere).

#### Mutation APIs

- **One service method per use case.** Mutations are exposed as dedicated methods that take only the changeable fields as parameters (e.g. `UpdatePersonaAsync(publicId, displayName, description, …)`), not as a generic `UpdateAsync(entity)` overload.
- **Tracked entities never leave the service.** A mutation method loads the entity internally with tracking, validates, mutates, calls `SaveChangesAsync`, and returns either a `bool` success indicator or a freshly loaded *detached* entity. Callers never see a tracked entity.
- **Each mutation method controls its own includes.** Load only the navigations needed for that specific mutation — typically *less* than what the corresponding Read API loads.
- **No "load → mutate → update" pattern across the API boundary.** Callers must not retrieve an entity via a Read API, modify it, and pass it back. This pattern is unsafe (no field-level control over what is changeable, double validation, concurrency leaks) and breaks down across process boundaries (Blazor WASM clients have no `DbContext`).

#### Compiled Queries

- Each compiled query in `LumaCore.Data.Queries` must be wired to at least one service-API method. Unused compiled queries are dead code — either wire them up or delete them.
- The compiled query's `Include(…)` shape must match the service method's documented contract exactly so it can serve as a drop-in hot-path replacement.
- The service method chooses between the compiled hot-path and the regular EF query via `if (PreferCompiledHotPathQueries)`. Both branches must yield identical results — verify with `[Theory] [InlineData(false)] [InlineData(true)]` toggle tests.
- Compiled queries cannot accept a `CancellationToken`. The toggled hot-path branch documents this with the standard comment: *"With PreferCompiledHotPathQueries enabled, cancellation is best-effort only."*

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
- Prefer named types (records, structs) over tuples for public APIs. Avoid exposing value tuples in public surface areas.

### File Headers

| Project | Header |
|---------|--------|
| `LumaCore.Data` | `Copyright (c) 2026 ...` |
| New files (2026) | `Copyright (c) 2026 ...` |
| Existing files (2025) modified | `Copyright (c) 2025-2026 ...` |

### Method Name References

- Method names in **all textual references** (code comments, XMLDoc prose, Markdown documentation, and commit messages) always have parentheses: `HandleConnectionFailure()`, not `HandleConnectionFailure`. This makes them instantly distinguishable from type names and properties.
- This does **not** apply to machine-readable references like `<see cref="..."/>` or `nameof(...)` — those follow compiler syntax.

### Collection/Data Structure Layout

For classes implementing `IList<T>`, `ICollection<T>`, etc.:

```csharp
// 1. Public core API methods (no region) - prominently visible at top
public void Insert(int index, T item) { }
public bool Remove(T item) { }

// 2. Private helper methods (no region)
private void CopyToArray(...) { }

// 3. #region for explicit interface implementations only
#region Implementation of IList<T>
bool ICollection<T>.IsReadOnly => false;
void ICollection<T>.Add(T item) { }
public struct Enumerator { }
#endregion
```
> [!NOTE]
> Use `#region` **only** for **explicit** interface implementations (`void IFoo.Bar()`).
> Implicit implementations (`public void Bar()`) and base class overrides are core API — they belong at the top without a region.

### SQL Statements in Test Helpers

- SQL statements in test helpers should use raw string literals (`""" ... """`) or raw interpolated string literals (`$""" ... """`).
- **Multi-clause** SQL (SELECT/FROM/WHERE, CREATE TABLE with multiple columns, multi-row INSERT) should use multi-line raw strings with readable line breaks. See `ReadAllRowsAsync` in `SqliteImportWriterTests.Helpers.cs` as the reference style.
- **Single-clause** SQL that fits within the 120-character line limit should use a **single-line** raw string (e.g., `"""DELETE FROM "Table" WHERE "key" = 'value'"""`).

---

## Commit Messages

> [!IMPORTANT]
> Follow the **Conventional Commits** specification for all commit messages.

### Format

```text
<type>(<scope>): <subject>

<body>
```

- **Header line is mandatory** (max 72 characters)
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

```text
feat(auth)!: Change token response structure

BREAKING CHANGE: Response field "token" renamed to "accessToken".
Clients must update their JSON parsing logic.
```

### Example

```text
test(core): Add unit tests for ExceptionHelpers.PrepareForRethrow()

Introduces comprehensive test coverage for the PrepareForRethrow
method, including verification of stack trace preservation and proper
exception unwrapping from AggregateException.
```

---

## Member Ordering

- Member ordering in classes follows the coding standards in [docs/development/coding-standards.md](../docs/development/coding-standards.md):
  - Constants
  - Static fields
  - Instance fields
  - Constructors
  - Dispose/DisposeAsync
  - Properties
  - Public methods
  - Protected/Internal methods
  - Private methods

### Magic Strings

- Avoid magic strings in code. Use framework-provided constants (e.g., `HeaderNames.Authorization` instead of `"Authorization"`).
