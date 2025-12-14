# LumaCore Coding Standards

Coding conventions and best practices for working in the LumaCore codebase.

---

## Philosophy

This document focuses on the **practical side**: concrete rules for how we write C# every day.

**New to LumaCore?** Here's how to get started:

1. Read the [Engineering Guidelines](engineering-guidelines.md) once — understand the *why*
2. Skim the [TL;DR](#tldr--quick-rules) below — get the quick overview
3. Use this document and the [Quick Reference Checklist](#quick-reference-checklist) as your daily reference

If you just need to know *what to do*, this is the place.

---

## Table of Contents

- [TL;DR – Quick Rules](#tldr--quick-rules) *(for experienced developers)*

1. [File Structure](#file-structure)
2. [Naming Conventions](#naming-conventions)
3. [Type Conventions](#type-conventions)
4. [XML Documentation](#xml-documentation)
5. [Formatting](#formatting)
6. [Async/Await Patterns](#asyncawait-patterns)
7. [Null Handling](#null-handling)
8. [Error Handling](#error-handling)
9. [Testing Conventions](#testing-conventions)
10. [Quick Reference Checklist](#quick-reference-checklist)
11. [When to Break the Rules](#when-to-break-the-rules)
12. [Tools and Automation](#tools-and-automation)
13. [Learning Resources](#learning-resources)

---

## TL;DR – Quick Rules

For experienced developers who already know modern C# and just need the LumaCore-specific rules.  
Each heading links to the full explanation and rationale.

### File Header
*→ [Full details](#file-structure)*

```csharp
// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System;                    // 1. System
using Microsoft.Extensions;      // 2. Microsoft
using Serilog;                   // 3. Third-party
using LumaCore.Api;              // 4. LumaCore

namespace LumaCore.Api.Features.Auth;  // File-scoped, one type per file
```

### Member Order
*→ [Full details](#member-order)*

1. Constants
2. Static fields
3. Instance fields (readonly first)
4. Constructors
5. Properties
6. Public methods
7. Protected/Internal methods
8. Private methods

### Naming
*→ [Full details](#naming-conventions)*

| Element | Convention | Example |
|---------|------------|---------|
| Class/Record/Interface | PascalCase (`I` prefix for interfaces) | `JwtTokenFactory`, `IJwtTokenFactory` |
| Method/Property | PascalCase | `CreateToken`, `AccessToken` |
| Instance field | `m` prefix + camelCase | `mLogger`, `mOptions` |
| Static field | `s` prefix + camelCase | `sDefaultValue`, `sCache` |
| Const | PascalCase | `SectionName`, `DefaultPort` |
| Parameter/Local | camelCase | `userId`, `result` |
| Async methods | `Async` suffix | `GetUserAsync()` |
| Collections | Plural | `Users`, `Claims` |

### Types
*→ [Full details](#type-conventions)*

- **Classes** are reference types (passed by reference):
  - `class` for services, behavior, entities
  - `record` for DTOs, configs, value equality
  - `sealed` by default

- **Structs** are value types (copied when passed):
  - Prefer `readonly struct` or `readonly record struct`
  - Mutable structs only in localized performance scenarios

- **Static classes** only for extension methods, utility functions, and constants

### Formatting
*→ [Full details](#formatting)*

- **Tabs** for indentation (not spaces)
- **`var`** only when type is evident (`new`, cast) – explicit for built-in types and unclear cases
- **[`ConfigureAwait(false)`](#asyncawait-patterns)** in all library/service code
- **`Task`** by default, `ValueTask` only when profiled
- **Async all the way** – never `.Result` or `.Wait()`

### Null Handling
*→ [Full details](#null-handling)*

```csharp
public string Name { get; set; } = string.Empty;     // Never null
public string? OptionalName { get; set; }            // Explicitly nullable
public List<User> Users { get; set; } = [];          // Collections never null

ArgumentNullException.ThrowIfNull(user);             // Modern null check
```

### XML Documentation
*→ [Full details](#xml-documentation)*

```csharp
/// <summary>Brief description.</summary>
/// <param name="name">Parameter.</param>
/// <typeparam name="T">Type parameter.</typeparam>
/// <returns>Return value.</returns>
/// <exception cref="Exception">When thrown.</exception>
/// <remarks>Additional details.</remarks>
```

| Tag | Purpose |
|-----|---------|
| `<see cref="Type"/>` | Link to type or member |
| `<see langword="null"/>` | Keyword (`null`, `true`, `false`) |
| `<paramref name="name"/>` | Reference a parameter |
| `<typeparamref name="T"/>` | Reference a type parameter |
| `<c>code</c>` | Inline code |
| `<code>code</code>` | Multi-line code block |

### Testing
*→ [Full details](#testing-conventions)*

- **Framework:** xUnit
- **Test class:** `<ClassUnderTest>Tests` → `JwtTokenFactoryTests`
- **Test name:** `MethodName_Condition_ExpectedResult`
- **Structure:** Arrange → Act → Assert
- **Attributes:** `[Fact]` for single tests, `[Theory]` for parameterized
- **Categories:** `Unit` (isolated class) · `Integration` (combined systems)

```csharp
[Fact]
[Trait("Category", "Unit")]
public void CreateToken_WithValidPrincipal_ReturnsValidJwt()
{
    // Arrange
    var factory = new JwtTokenFactory(options);
    
    // Act
    var token = factory.CreateToken(principal);
    
    // Assert
    Assert.NotNull(token);
}
```

---

> &nbsp;
> 
> 📖 **For explanations and examples, continue reading the detailed sections below.**
> 
> &nbsp;

---

## File Structure

File structure is the first thing you see when you open a file.  
These rules make sure that every file feels familiar — no matter who wrote it.

### Copyright Header

Every `.cs` file must start with:

```csharp
// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore
```

This gives us legal clarity, consistent license information, and a clear link back to the project.

### Using Directives

Organize `using` directives with blank lines between the groups:

```csharp
// 1. System namespaces (alphabetically)
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

// 2. Microsoft namespaces (alphabetically)
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

// 3. Third-party namespaces (alphabetically)
using Serilog;

// 4. LumaCore namespaces (alphabetically)
using LumaCore.Api.Features.Auth;
using LumaCore.Api.Features.Auth.Contracts;
```

This ordering makes imports easy to scan and keeps diffs clean. When everyone uses the same structure, merge conflicts around `using` statements become rare.

### File-Scoped Namespaces

Use file-scoped namespaces (C# 10+) — they reduce indentation and keep the focus on the code that matters.

```csharp
// ✅ Good - File-scoped
namespace LumaCore.Api.Features.Auth;

public class JwtTokenFactory
{
    // ...
}
```

```csharp
// ❌ Bad - Block-scoped
namespace LumaCore.Api.Features.Auth
{
    public class JwtTokenFactory
    {
        // ...
    }
}
```

**Why file-scoped is preferred:**

1. **Less indentation, more readable:** Every line in your file saves one level of indentation.

   ```
   Block-scoped:    namespace { class { method { if { ... }}}}  = 4 levels
   File-scoped:     class { method { if { ... }}}               = 3 levels
   ```

   One level less may not look dramatic, but across a whole project it adds up to cleaner, flatter code.

2. **More vertical space for real code:** Without namespace braces, you save two lines per file that would otherwise be wasted on wrapping braces.

3. **One file, one namespace:** Modern C# code usually has a single primary type per file. File-scoped namespaces match that reality and avoid unnecessary structure.

4. **Less boilerplate:** No closing brace to maintain.

**Real-world comparison**

Block-scoped:

```csharp
namespace LumaCore.Api.Features.Auth
{
    public class JwtTokenFactory
    {
        public string CreateToken()
        {
            if (condition)
            {
                return "token";
            }
            return string.Empty;
        }
    }
}
```

File-scoped:

```csharp
namespace LumaCore.Api.Features.Auth;

public class JwtTokenFactory
{
    public string CreateToken()
    {
        if (condition)
        {
            return "token";
        }
        return string.Empty;
    }
}
```

### One Type Per File

Each file contains exactly one primary type:

```
JwtTokenFactory.cs      → class JwtTokenFactory
IJwtTokenFactory.cs     → interface IJwtTokenFactory
JwtOptions.cs           → class JwtOptions
LoginRequest.cs         → record LoginRequest
```

**Exception:** Private nested types stay in the parent class file.

**Why one type per file:**

1. **Easy to locate**

   If you want `JwtTokenFactory`, you open `JwtTokenFactory.cs`. No guessing, you find it where you expect it.

2. **Cleaner diffs**

   When each file contains one type, a change to that type results in a small, focused diff. Multi-type files tend to show unrelated changes together, which makes reviews harder.

3. **Natural size limit**

   Single-type files tend to stay at a manageable size. When a file starts to grow very large, it's a clear signal that the type may need refactoring.

4. **Encourages smaller, focused types**

   You are more likely to split responsibilities into separate types instead of piling more code into an already busy file.

**Example problem:**

```csharp
// BadFile.cs - Don't do this!
public class JwtTokenFactory { ... }
public class JwtTokenValidator { ... }
public class JwtOptions { ... }
public interface IJwtTokenFactory { ... }
```

When you scroll through a file like this, you lose track of which class you're in — the header scrolls off-screen, and the filename doesn't help.

Better:

```csharp
// JwtTokenFactory.cs
public class JwtTokenFactory { ... }

// JwtTokenValidator.cs
public class JwtTokenValidator { ... }

// JwtOptions.cs
public class JwtOptions { ... }

// IJwtTokenFactory.cs
public interface IJwtTokenFactory { ... }
```

**Nested types:**  
Private nested helper types that only exist to support their parent class may stay in the same file:

```csharp
// JwtTokenFactory.cs
public sealed class JwtTokenFactory
{
    private sealed class TokenCache  // ✅ OK - private helper
    {
        // Only used by JwtTokenFactory
    }
}
```

Everything else gets its own file.

### Member Order

Organize class members in this order:

```csharp
public sealed class MyService
{
    // 1. Constants
    private const int MaxRetries = 3;
    public const string DefaultValue = "default";

    // 2. Static fields (readonly first)
    private static readonly ILogger sStaticLogger = ...;
    private static int sInstanceCount = 0;

    // 3. Instance fields (readonly first)
    private readonly ILogger<MyService> mLogger;
    private readonly IConfiguration mConfig;
    private int mRetryCount;

    // 4. Constructors
    public MyService(ILogger<MyService> logger)
    {
        mLogger = logger;
    }

    // 5. Properties
    public string Name { get; set; }
    public bool IsEnabled => mConfig["Enabled"] == "true";

    // 6. Public methods
    public void DoWork() { }

    // 7. Protected/Internal methods
    protected virtual void ProcessData() { }

    // 8. Private methods
    private void HelperMethod() { }
}
```

**Why this specific order:**

It follows a **"general to specific"** and **"public to private"** flow:

1. **Constants** — Most general, least likely to change, compile-time values.
2. **Static fields** — Shared state and class-level concerns.
3. **Instance fields** — The actual state of this object (readonly first because they are stable once set).
4. **Constructors** — How the object is created and what it needs.
5. **Properties** — How the outside world reads or writes state.
6. **Public methods** — What the class does for its callers.
7. **Protected/Internal methods** — For inheritance or internal collaboration.
8. **Private methods** — Implementation details at the bottom.

When you open a class, you quickly see:
- what it depends on (fields),
- how to construct it (constructors),
- what it can do (public methods),
- and only then the internal mechanics (private methods).

Once you develop muscle memory for this order, navigating a new class becomes much faster: dependencies at the top, public surface in the middle, details at the bottom.

### When Classes Grow Large

If a class becomes large enough that scrolling and navigation start to hurt, treat that as a signal:

1. **Prefer refactoring** into smaller, focused classes. Large classes often hint at too many responsibilities.

2. **Use `partial class`** across multiple files if refactoring is not practical yet. This keeps the "one primary type per file" spirit while splitting along concerns:

   ```
   MyService.cs             # Core logic
   MyService.Validation.cs  # Validation methods
   MyService.Helpers.cs     # Helper methods
   ```

3. **Avoid `#region`**

   Regions create the illusion of structure while the underlying problem remains. If you feel the need for lots of regions, the class is probably doing too much and should be split instead.

### Namespace Organization

Match namespaces to folder structure:

```
src/LumaCore.Api/Features/Auth/JwtTokenFactory.cs
→ namespace LumaCore.Api.Features.Auth;

src/LumaCore.Core/Personas/PersonaEngine.cs
→ namespace LumaCore.Core.Personas;
```

That way, you can often guess the namespace just by looking at the path, and vice versa. It also keeps refactoring and moving files straightforward.

---

## Naming Conventions

Names are the first line of documentation. Good names make code readable even before you open the implementation.

### General Rules

| Element              | Convention                          | Example                              |
|----------------------|--------------------------------------|--------------------------------------|
| **Namespace**        | PascalCase, match folder structure  | `LumaCore.Api.Features.Auth`        |
| **Class**            | PascalCase                          | `JwtTokenFactory`                   |
| **Interface**        | PascalCase with `I` prefix          | `IJwtTokenFactory`                  |
| **Method**           | PascalCase                          | `CreateToken`                       |
| **Property**         | PascalCase                          | `AccessTokenLifetimeMinutes`        |
| **Field (instance)** | camelCase with `m` prefix           | `mLogger`, `mOptions`               |
| **Field (static)**   | camelCase with `s` prefix           | `sDefaultValue`, `sCache`           |
| **Const**            | PascalCase                          | `SectionName`, `DefaultPort`        |
| **Parameter**        | camelCase                           | `principal`, `userId`               |
| **Local Variable**   | camelCase                           | `utcNow`, `result`                  |
| **Type Parameter**   | PascalCase, single letter or `T...` | `T`, `TKey`, `TValue`               |

These conventions make it easy to see at a glance what kind of symbol you are looking at.

### Hungarian Notation for Fields

LumaCore uses a light form of Hungarian notation for **private fields**:

- Instance fields use the `m` prefix (for **m**ember)
- Static fields use the `s` prefix (for **s**tatic)

```csharp
public sealed class MyService
{
    // Static fields - 's' prefix
    private static readonly string sDefaultValue = "default";
    private static readonly ILogger sStaticLogger = ...;
    private static int sInstanceCount = 0;

    // Instance fields - 'm' prefix
    private readonly ILogger<MyService> mLogger;
    private readonly MyOptions mOptions;

    // Parameter - no prefix
    public MyService(ILogger<MyService> logger, MyOptions options)
    {
        mLogger = logger;   // Clear distinction
        mOptions = options; // between fields and parameters
    }

    public void DoWork()
    {
        string result = ProcessData(); // Local variable - no prefix
        int count = GetCount();
        ...
    }
}
```

**Why we do this:**

- Immediately distinguishes fields from parameters and locals
- Prevents name collisions in constructors (`mLogger` vs `logger`)
- Makes static vs instance state obvious (`sCache` vs `mCache`)
- Is consistent throughout the codebase, which reduces mental overhead

### Async Methods

Async methods must have the `Async` suffix:

```csharp
// ✅ Good
public Task<string> CreateTokenAsync(ClaimsPrincipal principal);
public Task<User?> GetUserAsync(Guid userId);
public async Task HandleLoginAsync(HttpContext context);

// ❌ Bad - missing Async suffix
public Task<string> CreateToken(ClaimsPrincipal principal);
public Task<User?> GetUser(Guid userId);
public async Task HandleLogin(HttpContext context);
```

**Rules:**

- Methods that return `Task` / `Task<T>` / `ValueTask<T>` get the `Async` suffix.
- `void`-returning async methods (typically event handlers) should also use `Async`, e.g. `OnMessageReceivedAsync`.
- Properties are never asynchronous.

**Why:**

- Makes the asynchronous nature visible at the call site.
- Prevents confusion between sync and async overloads.
- Matches .NET ecosystem conventions, so external APIs feel familiar.

### Boolean Names

Boolean members should read like clear questions and avoid double negatives.

```csharp
// ✅ Good
public bool IsEnabled { get; set; }
public bool HasAccess { get; }
public bool CanRetry { get; }

// ❌ Bad
public bool Enabled { get; set; }        // Reads like a noun, not a condition
public bool HasNoAccess { get; }         // Double negative in calling code
public bool IsNotDisabled { get; set; }  // Hard to reason about in conditions
```

**Guidelines:**

- Use prefixes like `Is`, `Has`, `Can`, `Should`, `Allow` to express intent.
- Avoid names that encode a negation (`Not`, `No`) unless it is truly the domain term.
- Prefer positive forms in code: `if (user.HasAccess)` is easier to read than `if (!user.HasNoAccess)`.

### Collections

Collections should have **plural** names and should not repeat the type in the name.

```csharp
// ✅ Good
public List<User> Users { get; set; }
public IEnumerable<Claim> Claims { get; }
public IReadOnlyDictionary<string, string> Settings { get; }

// ❌ Bad
public List<User> UserList { get; set; }            // Redundant - the type already says List
public List<User> User { get; set; }                // Singular - suggests a single item
public IEnumerable<Claim> ClaimCollection { get; }  // Type repeated
```

**Why:**

- The type already communicates that this is a collection.
- The name should describe **what** the collection contains, not that it *is* a collection.
- Plural names make calling code read naturally: `foreach (var user in Users)`.

---

## Type Conventions

Type choices should make intent obvious:  
Is this thing **stateful**, **behavioral**, or just **data**?  
The following rules help keep that consistent.

---

### Reference Types

**Class**

A `class` is a reference type with reference equality by default — two instances are only equal if they are the same object. Classes are the standard choice for services, entities with behavior, and mutable state that evolves over time.

```csharp
public sealed class JwtTokenFactory : IJwtTokenFactory
{
    private readonly JwtOptions mOptions;

    public JwtTokenFactory(IOptions<JwtOptions> options)
    {
        mOptions = options.Value;
    }

    public string CreateToken(ClaimsPrincipal principal)
    {
        // Token creation logic based on principal and options
    }
}
```

**Record**

A `record` is essentially a `class` with compiler-generated value equality (`Equals`, `GetHashCode`, `==`, `!=`), a descriptive `ToString()`, and support for `with`-expressions. Use records for DTOs, configuration models, and immutable value objects where value equality makes sense.

```csharp
public sealed record JwtOptions
{
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; init; }
}

var options = new JwtOptions { Issuer = "LumaCore", Audience = "Api" };
Console.WriteLine(options);
// Output: JwtOptions { Issuer = LumaCore, Audience = Api, AccessTokenLifetimeMinutes = 0 }
```

---

### Value Types

Value types are **copied** when passed around. This is powerful for performance, but can surprise developers who expect reference semantics.

> [!WARNING]
> Mutable structs can lead to subtle bugs if you're not careful. Changes to a copy don't affect the original.

```csharp
var p = new Point { X = 1, Y = 2 };
var copy = p;      // Copies the entire struct
copy.X = 99;       // Only changes the copy!
// p.X is still 1
```

**Struct**

A `struct` is a value type allocated on the stack. Prefer `readonly struct` for most use cases — immutability prevents surprises. Mutable structs are fine in performance-critical, localized scenarios, but avoid them in public APIs where the copy semantics might confuse consumers.

```csharp
public readonly struct Vector2
{
    public float X { get; }
    public float Y { get; }

    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float Length => MathF.Sqrt(X * X + Y * Y);
}
```

**Record Struct**

A `readonly record struct` combines stack allocation with compiler-generated value equality (`Equals`, `GetHashCode`, `==`, `!=`), a descriptive `ToString()`, and support for `with`-expressions. Best of both worlds for small value types where you want record features without heap allocation.

```csharp
public readonly record struct Point(int X, int Y);
public readonly record struct Color(byte R, byte G, byte B, byte A);

var point = new Point(10, 20);
Console.WriteLine(point);
// Output: Point { X = 10, Y = 20 }
```

---

### Sealed by Default

Most classes should be `sealed` — only unseal when you have a deliberate inheritance design (e.g., base classes, framework hooks).

```csharp
// ✅ Good
public sealed class JwtTokenFactory : IJwtTokenFactory
{
    // ...
}

// Only when you explicitly design for inheritance:
public abstract class PersonaStoreBase
{
    // ...
}
```

**Why `sealed` by default:**

1. **Clear intent:** A sealed class says: *"This type is not meant to be extended via inheritance."* If you need variation, you use composition or interfaces instead.

2. **Safer changes:** Non-sealed classes can have unknown inheritors outside your assembly. A seemingly harmless change (adding a virtual call, changing a protected behavior) can break consumers you don't see.

3. **Performance**

   The JIT compiler can optimize calls to sealed classes better:
   - Devirtualization (no virtual dispatch)
   - Inlining of methods
   - More predictable codegen

---

### Static Classes

Static classes should be the exception, not the default — they can't be injected, mocked, or tested in isolation. Use them only for stateless helpers.

**Use static classes only for:**

- Pure utility functions without state
- Extension methods
- Constants

```csharp
// ✅ Good - Feature registration
public static class AuthFeatureRegistration
{
    public static void AddAuthFeature(this WebApplicationBuilder builder)
    {
        // composition and wiring only
    }
}

// ✅ Good - Pure helper
public static class StringHelpers
{
    public static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
```

---

### Readonly Fields

Fields that are set once in the constructor and never change should be `readonly` — this makes invariants explicit and prevents accidental reassignment.

```csharp
public sealed class AuditLogger
{
    private readonly ILogger<AuditLogger> mLogger;
    private readonly string mCategory;

    public AuditLogger(ILogger<AuditLogger> logger, string category)
    {
        mLogger = logger;
        mCategory = category;
    }

    public void Log(string message)
    {
        mLogger.LogInformation("[{Category}] {Message}", mCategory, message);
    }
}
```

If you try to reassign them, the compiler will protect you:

```csharp
public void DoWork()
{
    mLogger.LogInformation("Starting work");

    mLogger = null;  // Compiler error: Cannot assign to readonly field

    mLogger.LogInformation("Done");
}
```

---

## XML Documentation

> [!TIP]
> **Why it matters:** XML documentation isn't just comments – it powers **IntelliSense** in your IDE and generates **Swagger/OpenAPI documentation** for your APIs. When you write `/// <summary>`, that text appears when other developers hover over your method. For API endpoints, it becomes the description in the OpenAPI spec that API consumers see.

Good XML docs mean:
- Developers understand your code without reading the implementation
- API consumers get accurate, up-to-date documentation automatically
- Swagger UI shows meaningful descriptions instead of empty boxes

### Full Documentation Required

Every API — public, internal, and private — should be fully documented. Describe all parameters, type parameters, return values, and exceptions. The only exception: trivial private methods where the name says it all.

Start with `<summary>` to explain *what* it does — this is what appears in IntelliSense and Swagger. For complex APIs, add `<remarks>` to explain *how* it works (implementation details, edge cases) and `<example>` to show *how to use it* with a code snippet.

```csharp
/// <summary>
/// Creates a JWT access token for the specified <paramref name="principal"/>.
/// </summary>
/// <param name="principal">
/// The claims principal containing the user's identity and claims.
/// This principal's claims will be included in the generated token.
/// </param>
/// <returns>
/// A signed JWT access token as a string.
/// The token includes all claims from the principal and has a lifetime
/// specified by <see cref="JwtOptions.AccessTokenLifetimeMinutes"/>.
/// </returns>
/// <exception cref="ArgumentNullException">
/// Thrown when <paramref name="principal"/> is <see langword="null"/>.
/// </exception>
/// <remarks>
/// The token is signed using HMAC-SHA256 with the key specified in
/// <see cref="JwtOptions.SigningKey"/>.
/// </remarks>
public string CreateToken(ClaimsPrincipal principal)
```

### XML Documentation Tags

#### Core Tags

```xml
<summary>
Brief description. First sentence should be self-contained.
</summary>

<param name="paramName">
Parameter description. What it is, what constraints apply.
</param>

<typeparam name="T">
Generic type parameter description.
</typeparam>

<returns>
What the method returns. Include type info and conditions.
</returns>

<exception cref="ExceptionType">
When and why this exception is thrown.
</exception>

<remarks>
Additional details, implementation notes, usage examples.
Appears after the main description in documentation.
</remarks>

<example>
A complete usage example with code:
<code>
var factory = new JwtTokenFactory(options);
var token = factory.CreateToken(principal);
</code>
</example>
```

#### Inline Tags

```xml
<see cref="Type"/>              <!-- Link to a type -->
<see cref="Method(Type)"/>      <!-- Link to a method -->
<see langword="null"/>          <!-- Keywords: null, true, false -->
<c>code</c>                     <!-- Inline code -->
<code>                          <!-- Multi-line code block -->
    var example = true;
</code>
<paramref name="param"/>        <!-- Reference a parameter -->
<typeparamref name="T"/>        <!-- Reference a type parameter -->
```

#### When to Use Which Tag

| Use Case | Tag | Example |
|----------|-----|---------|
| Types (own or framework) | `<see cref=""/>` | `<see cref="ProblemDetails"/>` |
| Interfaces | `<see cref=""/>` | `<see cref="IExceptionHandler"/>` |
| Methods as reference | `<see cref=""/>` | `<see cref="CreateToken"/>` |
| Methods as call example | `<c>` | `<c>AddProblemDetails()</c>` |
| Keywords | `<see langword=""/>` | `<see langword="null"/>` |
| Literals (strings, numbers) | `<c>` | `<c>"Bearer"</c>`, `<c>100</c>` |
| JSON field names | `<c>` | `<c>traceId</c>` |
| HTTP status codes | `<c>` | `<c>400 Bad Request</c>` |
| HTTP headers | `<c>` | `<c>Retry-After</c>` |
| HTTP methods + paths | `<c>` | `<c>POST /api/auth/login</c>` |
| URNs / URIs | `<c>` | `<c>urn:lumacore:error:validation</c>` |
| File names | `<c>` | `<c>Program.cs</c>` |

**Rule of thumb:**
- If the reader should be able to navigate to a definition → `<see cref=""/>`
- If it's a literal value or code snippet → `<c>`
- If it's a C# keyword → `<see langword=""/>`

---

## Formatting

This section covers formatting choices that affect everyday code readability: indentation and type declarations.

---

### Indentation

Use **tabs** for indentation (spaces for alignment if needed).

```csharp
public class MyClass
{
	private readonly ILogger mLogger;  // Tab
	
	public void MyMethod()
	{
		if (condition)
		{
			DoSomething();  // Tab
		}
	}
}
```

**Why tabs, not spaces:**

1. **Accessibility:** Developers with visual impairments can configure their editor to display tabs at their preferred width. Some people need 8 spaces of indentation to see code structure clearly; others prefer 2. With tabs, everyone can choose their own viewing preference while the file stays identical.

2. **Smaller file size:** One tab character = 1 byte. Four spaces = 4 bytes. For large codebases, this adds up.

3. **Semantic meaning:** A tab means "one level of indentation." Spaces mean "whitespace." Tabs are semantically correct for indentation.

4. **No arguments about width:** With spaces, teams argue: 2 spaces? 4 spaces? 8 spaces? With tabs, everyone can use their own preference without changing the file.

5. **Easier to navigate:** One keypress moves one indentation level. With spaces, you're hitting arrow keys 2–4–8 times per level.

**Real-world example**

Developer A has reduced vision and needs 8-space indentation to see structure:

```csharp
// With spaces (locked to 4):
public class MyClass
{
    public void Method()        // Hard to see
    {
        if (condition)
        {
            DoWork();
        }
    }
}

// With tabs (they can configure 8-space display):
public class MyClass
{
→       public void Method()    // Much clearer!
→       {
→       →       if (condition)
→       →       {
→       →       →       DoWork();
→       →       }
→       }
}
```

---

### Variable Declarations

We use `var` **where it helps** and avoid it where it hides intent.

Use explicit types for built-in types and unclear cases. Use `var` only when the type is evident from the right side:

```csharp
// ✅ Built-in types: always explicit
int count = GetCount();
string name = GetName();
bool isValid = CheckValidity();

// ✅ Type is evident (new, cast, literal): var is fine
var user = new User();
var logger = loggerFactory.CreateLogger<MyService>();
var items = new List<string>();

// ✅ Type is not evident: explicit
User user = GetUser();
HttpClient client = GetClient();
IEnumerable<Order> orders = LoadOrders();
```

**Why this approach:**

1. **Built-in types are short:** `int`, `string`, `bool` are easy to type and read. There is no real benefit from `var` here.

2. **Readability without IDE:** Code should be understandable in code reviews, on GitHub, or in printed snippets.  
   `var result = GetResult()` tells you nothing about what `result` actually is.

3. **Avoid redundancy:**  
   `User user = new User()` repeats the type;  
   `var user = new User()` is cleaner when the right side already tells the full story.

4. **Surprise-free reading:** When the type isn't obvious from the right side, an explicit type prevents you from mentally "guessing" what you're dealing with.

---

## Async/Await Patterns

LumaCore uses `async`/`await` throughout the codebase.  
These rules keep asynchronous code **correct**, **predictable**, and **efficient**.

---

### ConfigureAwait(false) in Library Code

In library code (services, repositories, utilities), **always** use `ConfigureAwait(false)` when awaiting tasks. In ASP.NET Core it's technically not needed, but it doesn't hurt and keeps the code portable.

```csharp
// ✅ Good — library code
public async Task<User> GetUserAsync(int id)
{
    var user = await mRepository
        .FindAsync(id)
        .ConfigureAwait(false);
    
    return user;
}
```

> [!TIP]
> Think of `ConfigureAwait(false)` as the default for all non-UI library code.
> It tells the runtime: "I don't care which thread I continue on — just resume as soon as possible."

**Why `ConfigureAwait(false)` matters:**

The main reason is **deadlock prevention** and avoiding unnecessary context capture.

```csharp
// Potential deadlock scenario (in UI or old ASP.NET)
public void SyncMethod()
{
    // Blocks thread waiting for task
    var result = GetDataAsync().Result;
}

async Task<Data> GetDataAsync()
{
    await mRepository.LoadAsync();  // Captures context by default
    // Tries to resume on original context...
    // But original thread is blocked above! 💀
    // With ConfigureAwait(false), this doesn't happen.
    return data;
}
```

In ASP.NET Core there is no `SynchronizationContext`, so deadlocks like this are much less likely and the performance impact of `ConfigureAwait(false)` is small.  
However, **library code can be reused** in UI apps or older ASP.NET applications — so we treat `ConfigureAwait(false)` as the safe default.

**When you DO need the context** (e.g., UI code):

Use explicit `ConfigureAwait(true)` to make the intent clear:

```csharp
// WPF/WinForms UI code — needs UI thread
public async Task LoadDataAsync()
{
    var data = await LoadFromDatabaseAsync().ConfigureAwait(true);  // Explicit: I NEED the UI thread
    this.TextBox.Text = data;  // Must run on UI thread!
}
```

---

### ValueTask vs Task

`Task` is the **default** for asynchronous operations.  
Use `ValueTask` only in **hot paths** where synchronous completion is common and performance has been measured.

```csharp
// ✅ Good — may complete synchronously (cache hit)
public ValueTask<User?> GetFromCacheAsync(string key)
{
    if (mCache.TryGetValue(key, out var user))
    {
        // Synchronous path — no allocation
        return new ValueTask<User?>(user);
    }
    
    // Asynchronous path — wrap the Task
    return new ValueTask<User?>(LoadFromDatabaseAsync(key));
}
```

For everything else, prefer `Task`:

```csharp
// ✅ Default choice
public Task<User?> GetUserAsync(int id)
{
    return mRepository.FindAsync(id);
}
```

**Limitations of `ValueTask`:**

1. **Can only be awaited once:** Use `Task` if you need to await more than once or retain for later reuse.

   ```csharp
   ValueTask<int> task = GetValueAsync();
   await task;  // ✅ OK
   await task;  // ❌ Undefined behavior!
   ```

   After the first `await`, the internal state may be recycled — unlike `Task`, which caches its result permanently.

2. **More complex to compose:** Use `.AsTask()` for APIs like `Task.WhenAll`.

   ```csharp
   Task<int> task1 = GetAsync();
   Task<int> task2 = GetAsync();
   await Task.WhenAll(task1, task2);  // ✅ Easy with Task
   ```

   With `ValueTask`, you need to convert via `.AsTask()` before using APIs like `Task.WhenAll`.

> [!TIP]
> Use `ValueTask<T>` only when you've **profiled** and confirmed that it helps, and document why you chose it.

```csharp
/// <summary>
/// Gets a user from cache by key.
/// </summary>
/// <remarks>
/// Uses <see cref="ValueTask"/> because this method is called 10k+ times/sec
/// and cache hits (synchronous path) are 90%+ of calls.
/// </remarks>
public ValueTask<User?> GetFromCacheAsync(string key)
```

---

### Async All the Way

Once you start using async, **stay async all the way up** the call chain.

In async methods, always use async variants of APIs:

```csharp
// ❌ Bad - Using sync API in async method
public async Task ProcessFileAsync(string path)
{
    var content = File.ReadAllText(path);  // Blocks thread!
    await ProcessAsync(content).ConfigureAwait(false);
}

// ✅ Good - Using async variant
public async Task ProcessFileAsync(string path)
{
    var content = await File.ReadAllTextAsync(path).ConfigureAwait(false);
    await ProcessAsync(content).ConfigureAwait(false);
}
```

Avoid `.Result` and `.Wait()`:

> [!WARNING]
> **Never use `.Result` or `.Wait()`** on a `Task` to wait for async code. This can cause deadlocks and thread pool starvation.

```csharp
// ❌ Bad — blocking wait on async method
public User GetUser(int id)
{
    return GetUserAsync(id).Result;  // Deadlock risk!
}

// ✅ Good — async all the way
public async Task<User> GetUserAsync(int id)
{
    return await mRepository
        .GetAsync(id)
        .ConfigureAwait(false);
}
```

**How deadlocks happen:**

In environments where async continuations try to resume on the original thread (WPF, WinForms, classic ASP.NET), blocking on `.Result` or `.Wait()` can deadlock that thread:

```csharp
// This can deadlock!
public void SyncMethod()
{
    // Thread 1: blocks here waiting for task
    var result = GetUserAsync(id).Result;
}

public async Task<User> GetUserAsync(int id)
{
    // Tries to resume on the same thread...
    await mRepository.LoadAsync();
    // ...but the thread is blocked in SyncMethod()
}
```

In ASP.NET Core this is less common, but blocking calls still waste threads and reduce throughput.  
That's why we avoid them as a general rule.

**When you truly can't go async:**

Sometimes you are forced to call async code from a synchronous entry point (legacy code, third-party APIs, framework constraints).  
These cases should be **rare** and clearly documented.

```csharp
// Option 1: Run on thread pool (safer, but adds overhead)
User user = Task.Run(() => GetUserAsync(id)).GetAwaiter().GetResult();

// Option 2: Design specifically for sync contexts
// Only if you are absolutely sure about the synchronization context
User user = GetUserAsync(id).ConfigureAwait(false).GetAwaiter().GetResult();
```

---

### IAsyncEnumerable for Streaming

Use `IAsyncEnumerable<T>` when returning sequences of data that are produced asynchronously — such as database query results or paginated API responses:

```csharp
// ✅ Good — streaming results from database
public async IAsyncEnumerable<User> GetAllUsersAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (var user in mDbContext.Users.AsAsyncEnumerable()
        .WithCancellation(cancellationToken)
        .ConfigureAwait(false))
    {
        yield return user;
    }
}

// Consuming the stream
await foreach (var user in userService.GetAllUsersAsync(cancellationToken))
{
    await ProcessUserAsync(user).ConfigureAwait(false);
}
```

**Why `IAsyncEnumerable<T>`:**

- **Memory efficient:** Items are processed one at a time, not loaded into a `List<T>` first
- **Responsive:** Callers can start processing before all data is available
- **Cancellation-friendly:** Supports `CancellationToken` via `[EnumeratorCancellation]`

**When to use:**

- Database queries returning many rows
- Paginated API calls
- Reading large files line by line
- Any scenario where you'd otherwise `await` a full collection before processing

**When NOT to use:**

- Small, bounded collections — just return `Task<List<T>>`
- When you need the count or all items before processing

---

## Null Handling

Null is one of the most common sources of bugs in any codebase.  
LumaCore uses **nullable reference types** to make null handling explicit and to move problems from runtime to compile time.

> [!TIP]
> Treat nullable warnings as design feedback, not as noise.  
> If the compiler complains about nullability, it is usually pointing at a design or contract that could be clearer.

---

### Nullable Reference Types

Nullable reference types are **enabled** in LumaCore. That means:

- `string` means "never null" — the compiler expects you to uphold that contract.
- `string?` means "may be null" — callers must handle the absence of a value.

```csharp
// ✅ Good — explicit nullability
public string Name { get; set; } = string.Empty;     // Never null
public string? OptionalName { get; set; }            // Can be null
public IEnumerable<User> Users { get; set; } = [];   // Never null

// ❌ Bad — non-nullable without initialization
public string Name { get; set; }  // Warning CS8618 — promises non-null but never set
```

---

### Collections Are Never Null

Collections should represent "zero or more items", not "maybe a collection, maybe nothing".  
We treat **empty collections** (`[]`) as the default, not `null`.

```csharp
// ✅ Good — initialized to empty
public List<User> Users { get; set; } = [];
public IEnumerable<Claim> Claims { get; set; } = Enumerable.Empty<Claim>();

// ❌ Bad — nullable collection
public List<User>? Users { get; set; }  // Don't do this
```

**Why collections should never be null:**

- `null` and "no items" are **different concepts**:
  - `null` → "we don't know / not loaded / doesn't exist"
  - empty → "we know, and there are zero items"
- Most calling code only cares about "for each user" — having to treat `null` as a special case adds noise everywhere.
- Many LINQ operations (`.Where`, `.Select`, `.Any`) work naturally on empty collections but throw on `null`.

**The cost of nullable collections:**

1. **Null checks everywhere:**
   ```csharp
   public void ProcessUsers(List<User>? users)
   {
       if (users == null) return;  // Defensive programming
       foreach (var user in users) { ... }
   }
   ```

2. **Cognitive load:**
   ```csharp
   public List<Order>? GetRecentOrders()
   {
       // Returns null if: error? no orders? not loaded? 🤔
   }
   ```

3. **More bugs:**
   ```csharp
   var users = GetUsers();  // Returns null
   var count = users.Count; // NullReferenceException 💥
   ```

**With initialized collections:**

```csharp
public List<User> Users { get; set; } = [];

public void ProcessUsers(List<User> users)
{
    // No null check needed
    foreach (var user in users) { ... }
}
```

**Performance consideration:**

Empty collections may seem like unnecessary allocations, but the overhead is minimal:

```csharp
// Allocates a new (small) List<T> per instance
public List<User> Users { get; set; } = [];

// Uses Array.Empty<T>() singleton — zero allocation
public IEnumerable<User> Users { get; set; } = [];
public IReadOnlyList<User> Users { get; set; } = [];
```

For mutable collections like `List<T>`, each instance needs its own list. For read-only interfaces, the compiler can use a shared singleton. For typical usage, this overhead is negligible.

---

### Boundaries: Converting Null to Empty

Not all systems follow the same rules. APIs, databases or external DTOs may legitimately use `null` to mean "no data".  
At the **boundary**, we normalize null to empty.

```csharp
// Incoming DTO (e.g., from an API)
public sealed record UserDto
{
    public List<string>? Tags { get; init; }  // May be null from the outside
}

// Internal model used in our codebase
public sealed class UserInternal
{
    public List<string> Tags { get; set; } = [];  // Never null inside LumaCore
}

// Conversion at the boundary
var model = new UserInternal
{
    Tags = dto.Tags ?? []   // Convert null to empty exactly once
};
```

After the boundary, **our code never has to care** whether the external system used `null` or `[]` — it's always an initialized collection.

---

### Null-Conditional and Coalescing Operators

Use the C# operators that make null handling explicit and readable:

```csharp
// Null-conditional: ?. and ?[]
// "If anything in this chain is null, stop and return null"
int? length = user?.Name?.Length;
string? first = users?[0];

// Null-coalescing: ??
// "If null, use this default instead"
string displayName = user.Name ?? "Unknown";

// Combined
int count = users?.Count ?? 0;
```

Avoid deeply nested `if (x != null)` ladders when the same logic can be expressed more clearly with `?.` and `??`.

---

### Null-Forgiving Operator

The null-forgiving operator (`!`) tells the compiler: "I know this value is not null at runtime." Only use it when you have **external knowledge** the compiler lacks — not to silence warnings.

```csharp
// ✅ OK — After validation the compiler doesn't understand
Debug.Assert(user != null);
Console.WriteLine(user!.Name);

// ✅ OK — Framework guarantees (e.g., required JSON property)
[Required]
public string Name { get; set; } = null!;

// ✅ OK — In tests where you control the data
User user = CreateTestUser()!;

// ✅ OK — Testing null argument handling
Assert.Throws<ArgumentNullException>(() => service.Process(null!));

// ❌ Bad — Hiding a potential bug
User? user = GetUser();
string name = user!.Name;   // What if GetUser() returned null?

// ❌ Bad — Instead of proper null handling
string name = possiblyNullString!;  // Just hoping for the best
```

---

### Argument Checking

For public APIs and important internal methods, check arguments explicitly and fail fast:

```csharp
public void SetUser(User user)
{
    ArgumentNullException.ThrowIfNull(user);
    // ...
}
```

This makes contracts clear:

- Callers see immediately which values may not be null.
- Bugs are caught **early** with a clear exception rather than a later `NullReferenceException` somewhere deep in the call stack.

> [!TIP]
> Argument checking is part of the method's contract.  
> Make it visible and consistent — it also makes your XML documentation more honest.

---

## Error Handling

LumaCore uses **exceptions** to signal exceptional situations and relies on **clear messages** and **structured logging** to make failures diagnosable in production.  
This section defines how we throw, structure, and log exceptions so that errors are:

- easy to understand during development,
- actionable in production,
- and consistent across the codebase.

---

### Exception Messages

Exception messages must be:

1. **Clear about what went wrong:** State the condition that failed.
2. **Include relevant values:** Echo inputs or state that help debugging (but **never** secrets or tokens).
3. **Suggest how to fix it:** When possible, hint at the required configuration or precondition.

```csharp
// ✅ Good — Helpful message with value and fix
throw new ArgumentException(
    $"UserId must be positive. Received: {userId}.",
    nameof(userId));

// ✅ Good — Configuration error with fix suggestion
throw new InvalidOperationException(
    "Jwt:SigningKey must be configured. " +
    "Set configuration key 'Jwt:SigningKey' or " +
    "environment variable 'Jwt__SigningKey'.");
```

```csharp
// ❌ Bad — Vague and unactionable
throw new Exception("Invalid");
```

**Guidelines:**

- Avoid generic `Exception` — prefer specific framework exceptions like `ArgumentException`, `ArgumentNullException`, `InvalidOperationException`, or custom domain exceptions.
- Include enough context to reproduce the issue without opening a debugger.
- Do **not** log in the exception message itself — logging is handled separately.

---

### Custom Exceptions

Use **custom exceptions** for domain- or feature-specific error cases that callers may want to distinguish and handle explicitly.

```csharp
public sealed class PersonaNotFoundException : Exception
{
    public string PersonaId { get; }

    public PersonaNotFoundException(string personaId)
        : base($"Persona with ID '{personaId}' was not found.")
    {
        PersonaId = personaId;
    }
}
```

**Why custom exceptions:**

- **Typed handling:** Callers can catch `PersonaNotFoundException` specifically instead of parsing message strings.
- **Structured context:** Properties like `PersonaId` provide programmatic access to important error details.
- **Readable failures:** Meaningful messages reduce the time needed to understand what went wrong.

**Guidelines:**

- Create custom exceptions when a **domain concept** appears in multiple places and needs dedicated error handling.
- Keep them **small and focused:** Message, key properties, and minimal constructors.
- Prefer `sealed` exceptions unless you explicitly expect inheritance.

---

### Logging Exceptions

Exceptions should be logged with **structured context** close to the boundary where they are handled.  
Inside the catch block:

1. Log the exception with contextual data.
2. Re-throw to preserve the error flow, unless this is a known, fully handled case.

```csharp
try
{
    await ProcessAsync(data).ConfigureAwait(false);
}
catch (Exception ex)
{
    mLogger.LogError(
        ex,
        "Failed to process data. DataId: {DataId}, UserId: {UserId}",
        data.Id,
        data.UserId);

    // Always re-throw unless this exception is fully handled.
    throw;
}
```

**Why this pattern:**

- Passing `ex` as the first parameter logs the full stack trace.
- Named placeholders (`{DataId}`, `{UserId}`) produce structured logs that can be filtered and searched.
- Using `throw;` (not `throw ex;`) preserves the original stack trace, which is crucial for debugging.

**Guidelines:**

- **Do not swallow exceptions** silently. If you catch and do not re-throw, document clearly **why** the error is considered handled.
- At boundaries (e.g., background workers, controllers), convert internal exceptions into appropriate HTTP responses or error contracts — *after* logging.
- Avoid logging the same exception multiple times on the same path; log at the boundary where the error becomes visible to the outside.

---

## Testing Conventions

Good tests make change safe. Tests are first-class citizens — they document behavior, guard against regressions, and show how the code is meant to be used.

### Framework and Project Layout

LumaCore uses **xUnit** for testing.

Test projects mirror the structure of the production code:

- Production: `src/LumaCore.Api/Features/Auth/JwtTokenFactory.cs`
- Tests: `tests/LumaCore.Api.Tests/Features/Auth/JwtTokenFactoryTests.cs`

This one-to-one mapping makes it easy to find tests for a given type.

### Test Class Naming

Test classes mirror the class they test with a `Tests` suffix:

```text
JwtTokenFactory      → JwtTokenFactoryTests
PersonaEngine        → PersonaEngineTests
AuthEndpointMapping  → AuthEndpointMappingTests
```

**Rule:** One primary class under test per test class. If a test class starts covering many unrelated types, it is usually a sign that behavior should be split.

### Test Naming

Use descriptive test method names in the form:

```csharp
MethodName_Condition_ExpectedResult
```

Examples:

```csharp
// ✅ Good — Descriptive test names
[Fact]
public void CreateToken_WithValidPrincipal_ReturnsValidJwt() { /* ... */ }

[Fact]
public void CreateToken_WithNullPrincipal_ThrowsArgumentNullException() { /* ... */ }

// ❌ Bad — Unclear, no behavior encoded in the name
[Fact]
public void Test1() { /* ... */ }
```

A reader should be able to understand **what** is being tested and **what the expected outcome is** without reading the test body.

### Facts, Theories, and Test Data

Use:

- `[Fact]` for fixed, self-contained tests with a single set of inputs.
- `[Theory]` with data attributes (`[InlineData]`, custom data sources, etc.) when the same behavior should be verified across multiple input combinations.

```csharp
[Theory]
[InlineData("short", false)]
[InlineData("longEnoughPassword", true)]
public void IsValidPassword_WithDifferentLengths_ReturnsExpectedResult(string password, bool expected)
{
    // Act
    bool result = PasswordValidator.IsValid(password);

    // Assert
    Assert.Equal(expected, result);
}
```

**Rule:** Prefer `[Theory]` when you would otherwise copy-paste the same test body with only the input changed.

### Arrange–Act–Assert Structure

> [!TIP]
> **Why AAA?** The Arrange-Act-Assert pattern makes tests **self-documenting**. Anyone reading the test immediately sees: what's the setup, what action is being tested, and what should happen. When a test fails, you know exactly which part broke. It also prevents the common mistake of mixing setup and assertions throughout the test.

Structure tests clearly using the **Arrange–Act–Assert (AAA)** pattern:

```csharp
[Fact]
public void CreateToken_WithValidPrincipal_ReturnsValidJwt()
{
    // Arrange
    var options = new JwtOptions
    {
        Issuer = "test",
        Audience = "test",
        SigningKey = "this-is-a-32-character-secret-key",
        AccessTokenLifetimeMinutes = 60
    };
    var factory = new JwtTokenFactory(Options.Create(options));
    var principal = new ClaimsPrincipal(/* ... */);

    // Act
    string token = factory.CreateToken(principal);

    // Assert
    Assert.NotNull(token);
    Assert.NotEmpty(token);
}
```

Why AAA:

- **Arrange:** Set up the world for this test only (no hidden shared state).
- **Act:** Perform exactly one action under test.
- **Assert:** Verify the outcome, as precisely as possible.

If you feel tempted to mix assertions into the Arrange or Act parts, the test is likely doing too much.

### Test Categories

Use `[Trait]` to categorize tests:

```csharp
[Fact]
[Trait("Category", "Unit")]
public void CreateToken_WithValidPrincipal_ReturnsValidJwt()
{
    // ...
}

[Fact]
[Trait("Category", "Integration")]
public void Login_WithValidCredentials_ReturnsToken()
{
    // ...
}
```

**Categories:**

- **Unit** — Isolated class or small group of classes with mocked or fake dependencies.  
  No database, no network, no file system. Fast and deterministic.
- **Integration** — Tests that involve multiple components or external resources  
  (database, file system, network, external services, etc.).

Run specific categories from the command line:

```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
```

### Test Quality Guidelines

When writing tests:

- Prefer **exact checks** over vague ones (for example, assert on full strings instead of substrings when feasible).
- Keep each test focused on **one behavior**; if you are checking many unrelated things, split the test.
- Avoid hidden dependencies (static state, shared mutable fixtures) unless there is a strong reason.
- Favor clarity over cleverness — tests are documentation as much as they are safety nets.

---

## Quick Reference Checklist

Use this checklist during code reviews to verify that new code aligns with the LumaCore standards.  
It is not a replacement for thinking — but it helps ensure we don't miss the basics.

### File Structure

- [ ] Copyright header present
- [ ] Using directives organized (System → Microsoft → Third-party → LumaCore)
- [ ] File-scoped namespace
- [ ] One primary type per file

### Naming

- [ ] PascalCase for types, methods, properties
- [ ] camelCase with `m` prefix for instance fields
- [ ] camelCase with `s` prefix for static fields
- [ ] `Async` suffix for async methods
- [ ] Plural names for collections

### Types

- [ ] Classes for behavior and services
- [ ] Records for data/DTOs and value-like types
- [ ] Structs are rare and usually `readonly`
- [ ] Classes are `sealed` by default unless designed for inheritance
- [ ] Static classes only for pure helpers or composition roots

### XML Documentation

- [ ] Public members have complete XML docs
- [ ] `<summary>` explains intent, not implementation details
- [ ] All `<param>` and `<returns>` tags are present where applicable
- [ ] Exceptions are documented when thrown as part of the contract
- [ ] `<see cref="..."/>` for types/members, `<c>` for literals (see [When to Use Which Tag](#when-to-use-which-tag))
- [ ] `<see langword="..."/>` for keywords (`null`, `true`, `false`)

### Code Organization and Formatting

- [ ] Tabs used for indentation, spaces only for alignment
- [ ] Member order respected (constants → static fields → instance fields → constructors → properties → public methods → protected/internal methods → private methods)
- [ ] No unnecessary `#region` blocks; large classes are split or refactored
- [ ] Namespaces match the folder structure

### Async/Await

- [ ] Async methods are truly asynchronous (no blocking `.Result`/`.Wait()`)
- [ ] `ConfigureAwait(false)` is used in library code where appropriate
- [ ] `Task` is used by default; `ValueTask` only in measured hot paths
- [ ] Async methods have an `Async` suffix

### Null Handling

- [ ] Nullable reference types used intentionally (`string` vs `string?`)
- [ ] Collections are non-nullable and initialized to empty
- [ ] Null from external systems is normalized at boundaries
- [ ] Null-forgiving operator (`!`) is rare and justified
- [ ] Public methods validate arguments (e.g., `ArgumentNullException.ThrowIfNull`)

### Error Handling

- [ ] Specific exception types are used instead of generic `Exception`
- [ ] Messages explain what failed and include relevant values (no secrets)
- [ ] Configuration or usage errors suggest how to fix the problem
- [ ] Exceptions are logged with structured context at appropriate boundaries
- [ ] `throw;` is used when re-throwing to preserve stack traces

### Testing

- [ ] Tests follow xUnit conventions (Facts/Theories)
- [ ] Test names follow `Method_Condition_ExpectedResult`
- [ ] Tests use Arrange–Act–Assert structure
- [ ] Traits categorize tests (e.g., `Category = Unit/Integration`)
- [ ] Tests are deterministic and do not depend on shared mutable state

---

## When to Break the Rules

These standards are **strong defaults**, not absolute laws. Breaking them is allowed when there's a good reason:

- **External APIs** that impose their own patterns
- **Performance-critical hot paths** where deviation is measurable and documented
- **Generated code** that follows different conventions
- **Transitional code** during migrations (clearly marked)

When you deviate:

1. **Comment** why the deviation exists
2. **Keep it local** — don't let exceptions spread
3. **Mention it in the PR** so reviewers understand

For the full rationale on balancing rules with pragmatism, see [When to Break the Rules](engineering-guidelines.md#when-to-break-the-rules) in the Engineering Guidelines.

---

## Tools and Automation

LumaCore uses tooling to make following these standards easier than ignoring them.

### EditorConfig

An `.editorconfig` file at the repository root configures basic formatting:

- **Indentation:** tabs (`indent_style = tabs`)
- **Indent size:** displayed as 4 spaces by default (`indent_size = 4`)
- **Encoding:** UTF-8 without BOM (`charset = utf-8`)
- **Line endings:** LF for all source files (`end_of_line = lf`)
  - Exception: Windows scripts (`.bat`, `.cmd`, `.ps1`, `.psm1`, `.psd1`) may use CRLF where required
- **Final newline:** enforced (`insert_final_newline = true`)
- **Trailing whitespace:** trimmed (`trim_trailing_whitespace = true`)
  - Exception: `.md` files may keep trailing spaces for intentional line breaks

**Why LF everywhere (except Windows scripts):**

Modern development is cross-platform. Using LF (Line Feed, `\n`) consistently:
- Works natively on Linux, macOS, and modern Windows (Git, VS Code, Visual Studio all handle LF)
- Prevents "line ending changed" noise in diffs when developers work on different OS
- Avoids Git autocrlf configuration issues
- Matches Unix/Linux conventions (where most servers run)
- Simpler: one line ending rule, not OS-dependent

Windows PowerShell and batch scripts get CRLF because Windows Command Prompt and older PowerShell versions require it for proper execution.

> [!TIP]
> If your editor doesn't seem to respect the settings, check that it is opened **at or below** the repository root so that `.editorconfig` is discovered.

### Code Analysis Tools

LumaCore relies on several layers of analyzers:

**Built-in Analyzers (included):**

- **Microsoft.CodeAnalysis.NetAnalyzers** — Built-in .NET analyzers enabled via project configuration
- **IDE diagnostics (IDE0xxx)** — Style analyzers provided by the IDE (VS/Rider)
- **Nullable reference type warnings** — C# nullable analysis enabled in the project

**Optional, but recommended:**

- **ReSharper** — Many team members use ReSharper for advanced code inspection and refactoring.

  The repository includes a ReSharper settings file (`.DotSettings`) that configures:

  - Recognition of `m` and `s` prefixes for fields
  - Tab-based indentation and formatting rules
  - Warnings for missing XML documentation
  - Preferences aligned with these coding standards

> [!NOTE]
> ReSharper is **not required** to contribute to LumaCore.  
> All critical rules are enforced via analyzers and CI. ReSharper settings are an additional convenience for those who use it.

---

## Learning Resources

### Internal Resources

- [Architecture Principles](../architecture/principles.md) — Why LumaCore is structured the way it is
- [Feature Pattern](../architecture/feature-pattern.md) — How features are organized and wired

### External Resources

These resources complement LumaCore's standards and give more background:

- [Microsoft C# Coding Conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/)
- [Async/Await Best Practices](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/)
- [Nullable Reference Types](https://learn.microsoft.com/dotnet/csharp/nullable-references)

---

## Questions or Suggestions?

These standards evolve with the project. If you have:

- Questions about how to apply a standard
- Suggestions for improvements or clarifications
- Cases where the current rules don't fit well

…please open a discussion on GitHub or propose changes via a pull request.

**Remember:** Consistency is more important than perfection.  
When in doubt, follow existing code in the same area — and if that code looks wrong, start a conversation.

---

© 2025 LumaCoreTech • MIT License
