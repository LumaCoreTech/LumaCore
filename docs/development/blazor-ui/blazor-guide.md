# Blazor Development Guide

This guide covers everything you need to write effective Blazor code in LumaCore — the rules, patterns, and the *how* and *why* of day-to-day development. For general C# conventions (naming, formatting, async patterns, etc.), see the [Coding Standards](coding-standards.md).

If you're new to Blazor or coming from a different frontend framework, this guide will help you understand the mental model. If you're experienced but hitting strange bugs, jump to [Troubleshooting Guide](#troubleshooting-guide) — you'll likely find the issue there.

> [!NOTE]
> LumaCore uses Blazor WebAssembly without prerendering. The app runs entirely in the browser. This guide does not cover prerendering scenarios.

---

## Table of Contents

- [The Blazor Mental Model](#the-blazor-mental-model)
- [Understanding the Component Lifecycle](#understanding-the-component-lifecycle)
  - [The Lifecycle at a Glance](#the-lifecycle-at-a-glance)
  - [Rendering During Async Initialization (Important!)](#rendering-during-async-initialization-important)
  - [When to Do What](#when-to-do-what)
  - [The Tricky Part: Conditional Rendering](#the-tricky-part-conditional-rendering)
- [Working with JavaScript Interop](#working-with-javascript-interop)
  - [Calling JavaScript from Blazor](#calling-javascript-from-blazor)
  - [Calling Blazor from JavaScript](#calling-blazor-from-javascript)
  - [ES Modules and Cleanup](#es-modules-and-cleanup)
  - [Parameter Marshalling](#parameter-marshalling)
- [State Management Patterns](#state-management-patterns)
  - [Local Component State](#local-component-state)
  - [Cascading Values](#cascading-values)
  - [Service-Based State](#service-based-state)
- [Forms and Validation](#forms-and-validation)
  - *See [Forms Guide](forms-guide.md) for complete coverage*
- [Error Handling and Cancellation](#error-handling-and-cancellation)
  - [The Core Pattern: CancellationTokenSource](#the-core-pattern-cancellationtokensource)
  - [Defensive Pattern: State Change Guards](#defensive-pattern-state-change-guards)
  - [Error Boundaries: Catching the Uncatchable](#error-boundaries-catching-the-uncatchable)
  - [Complete Example: Loading States with Error Recovery](#complete-example-loading-states-with-error-recovery)
- [Performance Considerations](#performance-considerations)
  - [Avoiding Unnecessary Renders](#avoiding-unnecessary-renders)
  - [Large Lists: Use Virtualization](#large-lists-use-virtualization)
  - [Batch JSInterop Calls](#batch-jsinterop-calls)
- [ConfigureAwait in Blazor](#configureawait-in-blazor)
  - [Why This Matters: The SynchronizationContext](#why-this-matters-the-synchronizationcontext)
  - [The Decision: Where Does the Code Run?](#the-decision-where-does-the-code-run)
  - [Service Classification: UI vs. Backend](#service-classification-ui-vs-backend)
  - [Razor Components: Always Stay on Context](#razor-components-always-stay-on-context)
  - [Common Mistakes](#common-mistakes)
  - [Summary: ConfigureAwait at a Glance](#summary-configureawait-at-a-glance)
- [Troubleshooting Guide](#troubleshooting-guide)
  - ["My JavaScript Does Nothing"](#my-javascript-does-nothing)
  - ["My UI Doesn't Update"](#my-ui-doesnt-update)
  - ["Cannot Read Property of Null" in JavaScript](#cannot-read-property-of-null-in-javascript)
  - ["Events Fire Multiple Times"](#events-fire-multiple-times)
  - [CSS Isolation Not Working](#css-isolation-not-working)
- [Summary](#summary)

---

## The Blazor Mental Model

Before diving into patterns and pitfalls, it helps to understand how Blazor thinks about components.

In traditional web development, you write HTML and JavaScript separately. The HTML exists, and JavaScript manipulates it. In React or Vue, you write components that *describe* what the UI should look like, and the framework figures out how to update the DOM.

**Blazor takes a similar approach.** C# code runs in the browser (via WebAssembly), and Blazor maintains a *RenderTree* — a lightweight representation of the DOM. When a re-render is triggered, Blazor rebuilds the RenderTree, compares it to the previous version, and applies only the necessary changes to the actual DOM.

This has an important implication: **the DOM elements you reference might not exist yet.** An element inside an `@if` block doesn't exist until that condition becomes `true`. An element in a `@foreach` doesn't exist until the collection has items. This is the source of most Blazor bugs — and once you internalize it, the framework becomes much more predictable.

---

## Understanding the Component Lifecycle

Every Blazor component goes through a lifecycle when it's created, updated, and destroyed. Understanding this lifecycle helps you put code in the right place.

### The Lifecycle at a Glance

The full lifecycle is more complex than this summary, but for day-to-day development, these are the key methods:

| Method | When it runs | Use it for |
|--------|--------------|------------|
| `OnInitialized` / `OnInitializedAsync` | Once, when the component is first created | Loading initial data, one-time setup |
| `OnParametersSet` / `OnParametersSetAsync` | After initialization, and whenever parameters change | Reacting to parameter changes |
| `OnAfterRender` / `OnAfterRenderAsync` | After the component has rendered to the DOM | JavaScript interop, DOM manipulation |

The critical insight: **`OnAfterRender` is the only method where the DOM definitely exists.** If you try to call JavaScript to manipulate an element during `OnInitializedAsync`, that element isn't in the browser yet.

### Rendering During Async Initialization (Important!)

This behavior surprises many developers: **Blazor renders the component before `OnInitializedAsync` completes.**

Specifically, Blazor triggers a render at the first `await` in async lifecycle methods. This is by design — it enables progressive UI (showing a loading state while data loads). But without awareness of this, `NullReferenceException` errors are common.

**The actual execution order:**

```
1. OnInitialized()              — sync, runs to completion
2. OnInitializedAsync() starts
3. First await in OnInitializedAsync()
   ↓
   ══════════════════════════════════════════════════
   Blazor renders immediately (data is still `null`!)
   ══════════════════════════════════════════════════
   ↓
4. OnAfterRender(firstRender: true)
5. OnAfterRenderAsync(firstRender: true)
6. await completes, OnInitializedAsync() continues
7. OnInitializedAsync() finishes
   ↓
   ══════════════════════════════════════════════════
   Blazor renders again (data is now available)
   ══════════════════════════════════════════════════
   ↓
8. OnAfterRender(firstRender: false)
9. OnAfterRenderAsync(firstRender: false)
```

**This is why you need null-checks and loading states:**

```razor
@if (mUsers is null)
{
    <p>Loading...</p>
}
else
{
    @foreach (var user in mUsers)
    {
        <p>@user.Name</p>
    }
}
```

Without the `@if` check, Blazor would try to iterate over `null` during step 3 — before the data has loaded.

**What about multiple awaits in OnInitializedAsync?**

Only the *first* `await` triggers a render. Subsequent awaits do not cause additional renders:

```csharp
protected override async Task OnInitializedAsync()
{
    mUsers = await LoadUsersAsync().ConfigureAwait(true);       // ← Render here
    mRoles = await LoadRolesAsync().ConfigureAwait(true);       // ← NO automatic render
    mSettings = await LoadSettingsAsync().ConfigureAwait(true); // ← NO automatic render
}
// ← Render when method completes
```

This results in 2 renders total, not 4. If you want progressive UI (showing each piece of data as it loads), call `StateHasChanged()` explicitly:

```csharp
protected override async Task OnInitializedAsync()
{
    mUsers = await LoadUsersAsync().ConfigureAwait(true);
    StateHasChanged();  // Show users immediately
    
    mRoles = await LoadRolesAsync().ConfigureAwait(true);
    StateHasChanged();  // Show roles immediately
    
    mSettings = await LoadSettingsAsync().ConfigureAwait(true);
    // Final render happens automatically
}
```

> [!NOTE]
> Two other lifecycle methods exist but are rarely needed:
> - `SetParametersAsync` — runs *before* `OnInitialized`. Useful for intercepting parameters before the component initializes.
> - `ShouldRender` — controls whether a render happens. Return `false` to skip rendering (performance optimization).
>
> For details, see [Microsoft's lifecycle documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle).

### When to Do What

| Method | DOM exists? | Use for |
|--------|-------------|---------|
| `OnInitializedAsync` | ❌ No | Data loading, service setup, state preparation |
| `OnAfterRenderAsync` | ✅ Yes | JavaScript interop, DOM manipulation, JS library initialization |

The DOM doesn't exist during `OnInitializedAsync` — the browser hasn't rendered anything yet. Any JSInterop call that tries to find DOM elements will fail. Use `OnAfterRenderAsync` for JavaScript, and guard one-time setup with `firstRender`:

```csharp
protected override async Task OnInitializedAsync()
{
    // ✅ Data loading — no DOM needed
    mUsers = await mUserService.GetAllAsync().ConfigureAwait(true);
    
    // ❌ DON'T do this here — the DOM doesn't exist yet!
    // await mJsRuntime.InvokeVoidAsync("initChart", mChartElement).ConfigureAwait(true);
}

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        // ✅ JavaScript setup — DOM elements exist now
        await mJsRuntime.InvokeVoidAsync("initChart", mChartElement).ConfigureAwait(true);
    }
}
```

**But what does "DOM exists" actually mean?** It means the DOM is consistent with the *current state* — not that all data has loaded. Consider the typical flow:

1. First `await` in `OnInitializedAsync` → Render #1 (`firstRender = true`)
   - `mUsers` is still `null`, so `@if (mUsers is null)` shows "Loading..."
   - The DOM exists and is correct for *this state* — but the data isn't there yet

2. `OnInitializedAsync` completes → Render #2 (`firstRender = false`)
   - Now `mUsers` is populated, so the `@foreach` block renders

This is why `firstRender` doesn't mean "data is ready". For elements that depend on loaded data, use the flag pattern instead — see [Conditional Rendering](#the-tricky-part-conditional-rendering) below.

### The Tricky Part: Conditional Rendering

Here's where most bugs come from. Consider this component:

```razor
@if (mIsLoaded)
{
    <div id="content">@mContent</div>
}
```

The `content` div doesn't exist until `mIsLoaded` becomes `true`. If you try to call JavaScript targeting that element before the condition is met, it will fail.

Even `OnAfterRenderAsync(firstRender: true)` won't help if `mIsLoaded` is still `false` at that point. The element simply isn't there yet.

The solution is to trigger JavaScript *after* the state change that makes the element appear. We call this the "flag pattern" — set a flag when JavaScript should run, and check that flag in `OnAfterRenderAsync`:

```csharp
private bool mIsLoaded;
private bool mNeedsJsSetup;

protected override async Task OnInitializedAsync()
{
    mContent = await LoadContentAsync().ConfigureAwait(true);
    mIsLoaded = true;
    mNeedsJsSetup = true;  // Flag for after render
}

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (mNeedsJsSetup)
    {
        mNeedsJsSetup = false;  // Clear the flag
        await mJsRuntime.InvokeVoidAsync("highlightElement", "content").ConfigureAwait(true);
    }
}
```

This pattern ensures JavaScript runs only after Blazor has rendered the elements it needs.

---

## Working with JavaScript Interop

Blazor can do a lot on its own, but sometimes JavaScript is needed — for DOM manipulation, browser APIs, or third-party libraries. JSInterop is the bridge between C# code and the browser's JavaScript runtime.

### Calling JavaScript from Blazor

Calling JavaScript from C# is straightforward. You specify the function name and any arguments:

```csharp
// Call a function with no return value
await mJsRuntime.InvokeVoidAsync("alert", "Hello from Blazor!").ConfigureAwait(true);

// Call a function and get a result
var width = await mJsRuntime.InvokeAsync<int>("getWindowWidth").ConfigureAwait(true);
```

The first parameter is the JavaScript function identifier, relative to the global `window` object. For nested functions, use dot notation (e.g., `localStorage.setItem` instead of `window.localStorage.setItem`). Any additional parameters are passed to the function.

**Passing DOM elements:** Sometimes you need to pass a DOM element to JavaScript without relying on IDs. This is especially useful when multiple instances of the same component exist, where ID collisions would be a problem. Blazor provides `ElementReference` for this:

```razor
<div @ref="mMyDiv">Content here</div>

@code {
    private ElementReference mMyDiv;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Pass the actual DOM element to JavaScript
            await mJsRuntime.InvokeVoidAsync("initializeElement", mMyDiv).ConfigureAwait(true);
        }
    }
}
```

On the JavaScript side, you receive the actual DOM element — no need to look it up by ID.

For details on how parameters are marshalled between C# and JavaScript, see [Parameter Marshalling](#parameter-marshalling) below.

### Calling Blazor from JavaScript

So far, we've only called JavaScript *from* Blazor. But what if JavaScript needs to call *back* into Blazor — for example, when a browser event occurs that Blazor doesn't natively handle?

**The challenge:** Blazor components are C# objects living in the .NET runtime. JavaScript can't just "call a C# method" directly — it needs a bridge.

**The solution:** `DotNetObjectReference` creates a handle that JavaScript can use to invoke methods on a specific component instance. Think of it as giving JavaScript a "phone number" to reach the component.

**How it works:**

1. The component creates a `DotNetObjectReference` pointing to itself
2. This reference is passed to JavaScript during initialization
3. JavaScript stores the reference and uses it to call `[JSInvokable]` methods
4. When the component is destroyed, the reference must be disposed (or memory leaks)

```csharp
@implements IDisposable

@code {
    private readonly string mComponentId = Guid.NewGuid().ToString();
    private DotNetObjectReference<MyComponent>? mDotNetRef;
    private string? mData;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Create the reference and pass it to JavaScript
            mDotNetRef = DotNetObjectReference.Create(this);
            await mJsRuntime.InvokeVoidAsync("registerDotNetHelper", mComponentId, mDotNetRef).ConfigureAwait(true);
        }
    }

    [JSInvokable]  // This attribute makes the method callable from JavaScript
    public Task OnStorageChanged(string key)
    {
        // InvokeAsync ensures everything runs on Blazor's synchronization context —
        // both the state update and the re-render
        return InvokeAsync(() =>
        {
            mData = key;
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        // Clean up: tell JavaScript to forget us, then dispose the reference
        _ = mJsRuntime.InvokeVoidAsync("unregisterDotNetHelper", mComponentId);
        mDotNetRef?.Dispose();
    }
}
```

```javascript
// In JavaScript (e.g., in index.html or a module):
const dotNetHelpers = new Map();

window.registerDotNetHelper = function(componentId, helper) {
    dotNetHelpers.set(componentId, helper);
};

window.unregisterDotNetHelper = function(componentId) {
    dotNetHelpers.delete(componentId);
};

// Example: When localStorage changes, notify all registered components
window.addEventListener('storage', (e) => {
    dotNetHelpers.forEach(helper => {
        helper.invokeMethodAsync('OnStorageChanged', e.key);
    });
});
```

**Why use a `Map` with component IDs?** If the same component exists multiple times in the application — even on different views — each instance needs its own registration. A single global variable would be overwritten by the second instance, breaking callbacks for the first. The `Map` ensures each instance is tracked separately.

**Why `InvokeAsync` in the callback?** The `[JSInvokable]` method may be called from a different thread (especially in Blazor Server). `InvokeAsync` ensures the state update and `StateHasChanged()` run on Blazor's synchronization context, avoiding race conditions.

> [!WARNING]
> Always dispose `DotNetObjectReference` in the component's `Dispose` method and unregister from JavaScript. Otherwise, the .NET object remains pinned in memory and cannot be garbage collected — even after the component is destroyed.

For details on how parameters are marshalled between JavaScript and C#, see [Parameter Marshalling](#parameter-marshalling) below.

### ES Modules and Cleanup

The examples above use global JavaScript functions (`window.registerDotNetHelper`). For larger applications, this pollutes the global namespace and makes it hard to track what JavaScript belongs to which component.

**ES Modules** solve this by keeping JavaScript isolated in separate files. Each module has its own scope — no global variables needed.

**The pattern:**

1. Create a JavaScript file with `export` functions
2. Import it in Blazor using `IJSObjectReference`
3. Call functions on that reference instead of global `window`
4. Dispose the reference when the component is destroyed

```javascript
// wwwroot/js/chart-component.js
let chart = null;

export function initialize(canvasElement, data) {
    chart = new Chart(canvasElement, {
        type: 'bar',
        data: data
    });
}

export function updateData(newData) {
    chart.data = newData;
    chart.update();
}

export function cleanup() {
    if (chart) {
        chart.destroy();
        chart = null;
    }
}
```

```csharp
@implements IAsyncDisposable
@inject IJSRuntime JsRuntime

@code {
    private ElementReference mCanvas;
    private IJSObjectReference? mModule;
    private object? mChartData = new { labels = new[] { "Q1", "Q2" }, values = new[] { 100, 200 } };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Import the module — returns a reference to call its exports
            mModule = await JsRuntime
                .InvokeAsync<IJSObjectReference>("import", "./js/chart-component.js")
                .ConfigureAwait(true);
            
            // Call the initialize function from the module
            await mModule.InvokeVoidAsync("initialize", mCanvas, mChartData).ConfigureAwait(true);
        }
    }

    private async Task UpdateChartAsync(object newData)
    {
        if (mModule is not null)
        {
            await mModule.InvokeVoidAsync("updateData", newData).ConfigureAwait(true);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (mModule is not null)
        {
            try
            {
                // Dispose the module reference itself
                await mModule.DisposeAsync().ConfigureAwait(true);
            }
            catch (JSDisconnectedException)
            {
                // JS runtime already gone (page navigation, browser refresh, etc.)
                // This is expected during cleanup — ignore silently.
            }
        }
    }
}
```

**Why `IAsyncDisposable` instead of `IDisposable`?** The cleanup calls `mModule.DisposeAsync()`, which returns a `ValueTask`. `IAsyncDisposable` with `DisposeAsync()` allows properly awaiting this call. With synchronous `Dispose()`, we'd have to either fire-and-forget (risking incomplete cleanup) or use `.Wait()` (which can deadlock).

**Why catch `JSDisconnectedException`?** When the user navigates away or refreshes the page, the JavaScript runtime may already be gone by the time `DisposeAsync` runs. This exception is expected and can be safely ignored. Note: In Blazor WASM, `JSDisconnectedException` is rare since there's no SignalR connection that can disconnect — this is primarily a Blazor Server concern.

**Important: DOM Cleanup Limitations**

If you need to clean up **DOM-related resources** (event listeners, MutationObservers, etc.), calling JavaScript from `DisposeAsync` is problematic:

- **The DOM might already be gone** — when the component is removed, the browser may have already destroyed the elements
- **The renderer might be disposed** — especially in Blazor Server, the circuit could be disconnected

**The recommended pattern for DOM cleanup:** Use JavaScript's `MutationObserver` or the `disconnectedCallback` (if using Web Components) to detect when elements are removed from the DOM:

```javascript
// chart.js
export function initialize(element) {
    const chart = createChart(element);
    
    // Cleanup when element is removed from DOM
    const observer = new MutationObserver(() => {
        if (!document.body.contains(element)) {
            chart.destroy();
            observer.disconnect();
        }
    });
    
    observer.observe(document.body, { childList: true, subtree: true });
    
    return chart;
}
```

This way, cleanup happens automatically when the DOM element disappears — no JSInterop from `DisposeAsync` needed.

**When module cleanup IS appropriate:**

Disposing the module reference itself (via `mModule.DisposeAsync()`) is fine — this releases the .NET-side reference. Just avoid calling JavaScript functions that manipulate or query the DOM from `DisposeAsync`.

**Benefits of this pattern:**
- **No global namespace pollution** — each module is isolated
- **Clear ownership** — the JavaScript file "belongs" to the component
- **Proper cleanup** — resources are released when the component is destroyed
- **Reusable** — multiple instances of the component each get their own module state

> [!NOTE]
> This pattern is for JavaScript setup and cleanup. To also receive callbacks from JavaScript (e.g., chart click events), combine this with `DotNetObjectReference` — pass the reference to the module's `initialize` function.

### Parameter Marshalling

When calling between Blazor and JavaScript in either direction, parameters are serialized to JSON using `System.Text.Json` with camelCase naming convention (C# property names like `UserName` become `userName` in JSON).

**How it works:** Blazor serializes parameters using `System.Text.Json` with camelCase naming. All types that `System.Text.Json` supports work automatically. Additionally, Blazor provides special handling for `ElementReference` (converted to DOM element) and `DotNetObjectReference` (handle for JS→C# callbacks). For custom types that need non-standard serialization, use `[JsonConverter]` attributes on properties. The global `JsonSerializerOptions` for JSInterop cannot be customized — for completely custom scenarios, serialize manually to a JSON string.

**What works out of the box:**
- **Primitives:**
  - Numeric types: `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal` ↔ JS `number`
  - Text: `string`, `char` ↔ JS `string`
  - Boolean: `bool` ↔ JS `boolean`
  - Dates: `DateTime`, `DateTimeOffset` ↔ JS `string` (ISO 8601 format, e.g., "2024-03-15T14:30:00Z")
  - TimeSpan: ↔ JS `string` (duration format, e.g., "01:30:45" for 1 hour, 30 minutes, 45 seconds)
  - Guid: ↔ JS `string` (standard format, e.g., "550e8400-e29b-41d4-a716-446655440000")
- **Classes and records** — serialized as JSON objects (C# `Title` ↔ JS `title`)
- **Dictionaries** — `Dictionary<string, T>` ↔ JS object `{ key: value }`
- **Arrays and lists** — of any of the above types (including nested objects) ↔ JS arrays `[...]`
- **`null`** — preserved in both directions
- **`ElementReference`** — converted to the actual DOM element (Blazor → JS only)

**Requirements for custom types:**

For classes/records to serialize correctly in JSInterop, follow these guidelines:

- **Prefer public properties with get/set** — the most reliable pattern for JSInterop
- **Records and init-only properties work** — System.Text.Json supports modern C# patterns: `public record UserData(string Name, int Age)` or `public string Name { get; init; }`
- **Parameterized constructors supported** — System.Text.Json can deserialize using constructor parameters that match property names (case-insensitive)
- **Fields require `[JsonInclude]`** — but this annotation is unnecessarily fragile in JSInterop due to non-configurable serializer options. Stick to properties.

**Recommended pattern (simple and reliable):**

```csharp
public class UserData
{
    public string Name { get; set; } = "";  // ✅ Property with get/set
    public int Age { get; set; }            // ✅ Property with get/set
}
```

**Also works (modern C# records):**

```csharp
// Primary constructor - works fine!
public record UserData(string Name, int Age);

// Init-only properties - works fine!
public class UserData
{
    public string Name { get; init; } = "";
    public int Age { get; init; }
}
```

**Doesn't work reliably:**

```csharp
public class BadExample
{
    public string Name;  // ❌ Field - requires [JsonInclude], unnecessarily fragile for JSInterop
    
    // ⚠️ Works if you manually JSON.stringify() in JS, but not via direct invokeMethodAsync()
    public string City { get; }  // ❌ Getter-only property, no way to set from JS
}
```

> [!TIP]
> **When to manually serialize:** If you need custom converters, non-standard constructors, or complex initialization logic, serialize to JSON string manually in JavaScript (`JSON.stringify(data)`) and deserialize in C# (`JsonSerializer.Deserialize<T>(jsonString)`). This gives you full control over serializer options.

**What doesn't work (without special handling):**

- **Delegates/Functions** — cannot be serialized to JSON
- **Circular references** — infinite loops during serialization
- **Streams (directly)** — but see below for stream support

**Stream support:**

While raw `Stream` objects can't be serialized to JSON, Blazor provides special types for streaming data:

**Blazor → JavaScript:** Use `DotNetStreamReference` to pass a stream reference

C# side:
```csharp
await using var stream = File.OpenRead("data.bin");
var streamRef = new DotNetStreamReference(stream);
await mJsRuntime.InvokeVoidAsync("processStream", streamRef).ConfigureAwait(true);
```

JavaScript side:
```javascript
export async function processStream(streamRef) {
    // Convert to JavaScript ReadableStream
    const arrayBuffer = await streamRef.arrayBuffer();
    // Or read incrementally:
    const stream = await streamRef.stream();
    const reader = stream.getReader();
    
    while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        // Process chunk (value is Uint8Array)
        console.log(`Received ${value.length} bytes`);
    }
}
```

**JavaScript → Blazor:** Pass a browser File or Blob as a stream

JavaScript side:
```javascript
// From file input
const fileInput = document.getElementById('upload');
const file = fileInput.files[0];

// Create stream reference (NOT passing file directly!)
const streamRef = await DotNet.createJSStreamReference(file);

// Pass stream reference to Blazor
await dotNetRef.invokeMethodAsync('ReceiveFile', streamRef);
```

C# side:
```csharp
[JSInvokable]
public async Task ReceiveFile(IJSStreamReference streamRef)
{
    // Set maxAllowedSize based on expected file size (default: ~512 KB if omitted)
    var maxSize = 10 * 1024 * 1024; // 10MB - adjust based on your requirements
    await using var stream = await streamRef.OpenReadStreamAsync(maxSize);
    
    // Server-side example: save to disk
    using var fileStream = File.Create("uploaded.bin");
    await stream.CopyToAsync(fileStream);
    
    // WASM alternative: upload to backend API or use browser storage
}
```

> [!IMPORTANT]
> **Why `createJSStreamReference`?** Blazor cannot directly deserialize browser `File` or `Blob` objects. The `createJSStreamReference` method wraps them into a format Blazor understands (`IJSStreamReference`), enabling efficient streaming without loading the entire file into memory.

**Use cases:**
- **File uploads:** User selects file in browser → stream to Blazor → save to disk/cloud
- **File downloads:** Blazor generates file → stream to JavaScript → trigger browser download
- **Large data processing:** Process binary data in chunks without loading entire file into memory

This enables efficient transfer of large binary data (files, images, etc.) without loading everything into memory at once.

**Example — passing a complex object:**

```csharp
public record ChartConfig(string Title, int Width, List<DataPoint> Data);
public record DataPoint(string Label, double Value);

var config = new ChartConfig(
    "Sales 2024",
    800,
    new List<DataPoint> { new("Q1", 1500), new("Q2", 1800) }
);

await mJsRuntime.InvokeVoidAsync("createChart", config).ConfigureAwait(true);
```

```javascript
function createChart(config) {
    console.log(config);
    // { title: "Sales 2024", width: 800, data: [{ label: "Q1", value: 1500 }, { label: "Q2", value: 1800 }] }
}
```

**The same applies in reverse** — when JavaScript calls Blazor via `DotNetObjectReference.invokeMethodAsync()`, parameters are serialized to JSON (with camelCase) and deserialized back to C# types (with PascalCase). The method signature determines how the JSON is parsed.

---

## State Management Patterns

As an application grows, state needs to be shared between components. Blazor offers several patterns, each suited to different scenarios.

### Local Component State

The simplest approach is private fields in the component. This works well for state that only this component cares about — form inputs, loading flags, locally fetched data.

```csharp
@code {
    private bool mIsLoading = true;
    private List<Item> mItems = [];
}
```

Blazor does **not** automatically track changes to fields. Instead, it re-renders after every event handler (button clicks, form submissions, etc.) completes. At that point, it sees whatever values the fields currently have.

If you change state *outside* an event handler — for example, in a timer callback or a service event — you must manually trigger a re-render:

```csharp
// From a timer or service callback (external source):
private void OnExternalEvent(string data)
{
    mData = data;
    _ = InvokeAsync(StateHasChanged);  // Safe from any context
}

// From a lifecycle method or UI event handler (already on UI thread):
private async Task OnButtonClick()
{
    mData = await LoadDataAsync().ConfigureAwait(true);
    StateHasChanged();  // Direct call is fine here
}
```

**When to use `InvokeAsync(StateHasChanged)`:** When called from outside Blazor's synchronization context — timer callbacks, service events, background threads. `InvokeAsync` ensures the call executes on the correct thread.

**When direct `StateHasChanged()` is sufficient:** Inside lifecycle methods (`OnInitializedAsync`, `OnParametersSetAsync`) or UI event handlers (`@onclick`, `@onchange`). You're already on Blazor's synchronization context, so wrapping is unnecessary.

### Cascading Values

When a parent component needs to share state with its children (and their children, and so on), cascading values avoid the need to pass parameters through every level of the component tree. This is how Blazor's built-in authentication state works — it cascades down from a provider at the top of the tree.

> [!NOTE]
> Cascading values are a niche feature for specific scenarios. For most application state, service-based state (explained below) is more appropriate — it gives finer control over which components re-render and makes data flow explicit.

**When to use Cascading Values:**
- Shared context that many descendants need (theme, user info, settings)
- Deep component hierarchies where passing parameters becomes tedious
- State that changes infrequently (frequent updates trigger re-renders in all descendants)

**When NOT to use Cascading Values:**
- State specific to one or two components (use parameters instead)
- Frequently changing state (every change re-renders all descendants — use service-based state with events)
- Unrelated components in different parts of the app (use service injection)

**Basic usage:**

In the parent or layout:

```csharp
@code {
    private string mTheme = "dark";

    private void ToggleTheme()
    {
        mTheme = mTheme == "dark" ? "light" : "dark";
        // All descendants will re-render automatically
    }
}

<CascadingValue Value="mTheme">
    <button @onclick="ToggleTheme">Toggle Theme</button>
    @ChildContent
</CascadingValue>
```

In any descendant, no matter how deeply nested:

```csharp
[CascadingParameter]
private string? Theme { get; set; }

<div class="@($"theme-{Theme}")">
    Content styled based on cascaded theme
</div>
```

**Multiple cascading values of the same type:**

If you need to cascade multiple values of the same type, use the `Name` parameter to distinguish them:

```razor
<CascadingValue Value="mPrimaryColor" Name="PrimaryColor">
    <CascadingValue Value="mSecondaryColor" Name="SecondaryColor">
        @ChildContent
    </CascadingValue>
</CascadingValue>
```

```csharp
[CascadingParameter(Name = "PrimaryColor")]
private string? PrimaryColor { get; set; }

[CascadingParameter(Name = "SecondaryColor")]
private string? SecondaryColor { get; set; }
```

**Performance consideration:** When the parent updates a cascading value, **all descendants** that declare a `[CascadingParameter]` for it will re-render — even if they don't use the value. For frequently changing state shared across unrelated components, consider service-based state with events instead.

### Service-Based State

For state shared across unrelated components — like user preferences or application-wide settings — inject a shared service. This is the most flexible pattern and the recommended approach for most shared state scenarios.

**The service holds the state and exposes an event for changes:**

```csharp
public class AppStateService
{
    private readonly object mLock = new();
    private Action? mOnChange;
    
    // Thread-safe event accessor (C# events are not thread-safe by default)
    public event Action? OnChange
    {
        add { lock (mLock) mOnChange += value; }
        remove { lock (mLock) mOnChange -= value; }
    }
    
    private string mCurrentLocale = "en";
    
    public string CurrentLocale
    {
        get { lock (mLock) return mCurrentLocale; }
        set
        {
            bool changed;
            lock (mLock)
            {
                changed = mCurrentLocale != value;
                if (changed) mCurrentLocale = value;
            }
            
            // Fire event outside lock to avoid potential deadlocks
            if (changed) mOnChange?.Invoke();
        }
    }
}
```

> [!IMPORTANT]
> **Thread-Safety by Service Lifetime:**
> 
> **Singleton services** must be thread-safe because in Blazor Server, multiple circuits (user sessions) access them concurrently across different threads. In WASM, Singletons are naturally safe (single-threaded), but we follow Server patterns for portability. Use `lock`, `Volatile.Read/Write`, or `Interlocked` operations.
> 
> **Scoped services** are automatically safe: In Blazor Server, each circuit has its own scoped instance, serialized by the Synchronization Context. In WASM, there's only one user anyway. Explicit synchronization isn't required unless you spawn background threads or timers.
> 
> **LumaCore standard:** We implement thread-safe patterns for all stateful services — even though LumaCore currently uses WASM (single-threaded). This ensures Blazor Server compatibility if we migrate later. The example above demonstrates proper thread-safe implementation:
> - Event accessors (`add`/`remove`) use `lock` because C# events aren't thread-safe by default
> - State property uses `lock` for get/set operations  
> - Event is fired **outside** the lock to avoid potential deadlocks
> 
> **Note:** Components handle events from any thread safely via `InvokeAsync(StateHasChanged)` — the concern is protecting the service's own state from concurrent access.

**Service registration:**

Register the service in `Program.cs` with the appropriate lifetime:

```csharp
// Singleton: One instance for the entire application lifetime
builder.Services.AddSingleton<AppStateService>();

// Scoped: One instance per user session (Blazor Server) or per app instance (Blazor WASM)
builder.Services.AddScoped<UserPreferencesService>();
```

**Choosing the lifetime:**
- **Singleton:** Use for truly global state (theme, feature flags, app-wide settings). Shared across all users in Blazor Server.
- **Scoped:** Use for user-specific state (user preferences, shopping cart). In Blazor Server, scoped to the SignalR circuit (one user). In Blazor WASM, effectively the same as Singleton since there's only one user.

**Components subscribe to the event and trigger re-renders when it fires:**

```csharp
@implements IDisposable
@inject AppStateService AppState

@code {
    protected override void OnInitialized()
    {
        AppState.OnChange += OnAppStateChanged;
    }

    private void OnAppStateChanged()
    {
        _ = InvokeAsync(StateHasChanged);  // Discard Task — we're not awaiting
    }

    public void Dispose()
    {
        AppState.OnChange -= OnAppStateChanged;  // Always unsubscribe to prevent memory leaks
    }
}
```

**Why `InvokeAsync`?** Service events can fire from any thread. `InvokeAsync(StateHasChanged)` ensures the UI update runs on Blazor's synchronization context, preventing threading issues in Blazor Server.

**Why `_ =` (discard)?** We're intentionally not awaiting the `Task` returned by `InvokeAsync`. The underscore explicitly documents this fire-and-forget pattern. If the handler needed to wait for the re-render to complete, use `await InvokeAsync(StateHasChanged)` instead.

---

## Localization

LumaCore uses a custom JSON-based localization system for multi-language support. Translations are stored in `wwwroot/locales/` with a simple nested JSON structure.

**Quick example:**

```csharp
@inject LocalizationService L

<h1>@L.Get("components.login.title")</h1>
<button>@L.Get("common.buttons.save")</button>
```

The system is thread-safe, synchronous (no async lookups needed), and integrates seamlessly with Blazor's rendering.

**For complete documentation,** see the [Localization Guide](localization-guide.md) which covers:
- File structure and service setup
- Using translations in components
- Language switching and persistence
- Localized validation messages
- Thread-safety design

---

## Forms and Validation

Forms in Blazor use the `EditForm` component for validation and submission handling. Unlike traditional HTML forms, validation runs client-side in C# before any server calls.

**Core concept:** `EditForm` manages an `EditContext` that tracks field state and validation errors. Input components bind to model properties and trigger validation on blur. Forms integrate with the localization system for both UI labels and validation messages.

**For complete documentation,** see the [Forms and Validation Guide](forms-guide.md) which covers:
- EditForm and EditContext fundamentals
- Localized validation (custom attributes vs. manual validation)
- Form submission events and server-side validation
- Common pitfalls and best practices

---

## Error Handling and Cancellation

Blazor components have a lifecycle problem: they can be disposed while async operations are still running. This happens when users navigate away from a page before data finishes loading, or when a component is removed from the DOM mid-operation.

**What goes wrong without proper handling:**
- Exceptions when trying to update state on a disposed component
- Memory leaks from operations that never complete
- Network requests that keep running even though no one cares about the result

The solution is **cancellation tokens** and **defensive coding**.

### The Core Pattern: CancellationTokenSource

Every component with async operations should follow this pattern:

```csharp
@implements IDisposable

@code {
    private readonly CancellationTokenSource mCts = new();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            mItems = await LoadItemsAsync(mCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Component was disposed during load — this is expected, not an error
        }
        catch (Exception ex)
        {
            mError = ex.Message;
        }
    }

    public void Dispose()
    {
        mCts.Cancel();  // Signal all operations to stop
        mCts.Dispose();
    }
}
```

**How it works:**

1. Create a `CancellationTokenSource` when the component initializes
2. Pass `mCts.Token` to every async operation (`HttpClient.GetAsync`, EF Core queries, etc.)
3. When the component disposes, call `mCts.Cancel()` — this signals all operations to stop
4. Operations throw `OperationCanceledException` when canceled — catch it separately from real errors

**Why catch `OperationCanceledException` separately?**

Cancellation is not an error — it's expected behavior when a user navigates away. If you catch it as a generic `Exception`, you might log it as an error or show error UI to the user, which is wrong.

### Defensive Pattern: State Change Guards

Even with cancellation tokens, there's a subtle race condition. An async operation can complete successfully, but before its continuation runs, the component gets disposed.

**The problem in detail:**

```csharp
private async Task RefreshDataAsync()
{
    var data = await FetchDataAsync(mCts.Token).ConfigureAwait(true);
    // ↑ await completes successfully, returns data
    // ↓ continuation (next line) is queued
    // ← Dispose() can run HERE
    mData = data;  // ❌ Runs after disposal!
    StateHasChanged();
}
```

**In Blazor WASM** (single-threaded), everything runs in the same event loop:

1. `FetchDataAsync()` completes → returns data
2. Continuation is queued in the event loop
3. User navigates away → Blazor queues `Dispose()`
4. Event loop may process `Dispose()` first → component disposed
5. Continuation runs → tries to update disposed component → exception

**In Blazor Server** (multi-threaded thread pool):

- `Dispose()` runs on Thread A (from the thread pool)
- Thread A finishes and returns to the pool
- Later, continuation runs on Thread B (different thread, same circuit)
- Multiple threads work on the circuit, but **never simultaneously** — Blazor guarantees "at any given point in time, work is performed on exactly one thread"
- The difference from WASM isn't parallel execution — it's that **the circuit's work moves between threads**. However, Blazor's Synchronization Context guarantees memory visibility, so a simple bool flag is sufficient.

**In both cases,** the cancellation token doesn't help because the async operation **already succeeded**.

**The solution:**

Add a flag that tracks disposal state:

```csharp
private bool mIsDisposed;

private async Task RefreshDataAsync()
{
    var data = await FetchDataAsync(mCts.Token).ConfigureAwait(true);
    
    if (mIsDisposed) return;  // Exit before touching state
    
    mData = data;
    await InvokeAsync(StateHasChanged).ConfigureAwait(true);
}

public void Dispose()
{
    mIsDisposed = true;  // Set flag BEFORE canceling
    mCts.Cancel();
    mCts.Dispose();
}
```

**Why a simple flag works:** In Blazor Server, the Synchronization Context emulates a single-threaded environment within each circuit, guaranteeing memory visibility and sequential consistency. In WASM, everything is actually single-threaded. The `await` points provide implicit memory barriers that ensure the disposed flag is visible across async operations. No explicit `volatile` needed for component-scoped state.

**LumaCore standard:** Use simple `bool` flags for disposal in components. We design for Blazor Server compatibility even though LumaCore currently uses WASM — if we migrate to Server later, we won't need to rewrite disposal patterns.

**When to use this pattern:**

Use state guards when you have async operations that might complete after disposal **and** you're updating component state afterward. If you're only reading data and returning it to a caller (no state updates), cancellation tokens alone are sufficient.

### Error Boundaries: Catching the Uncatchable

`ErrorBoundary` is Blazor's safety net for unhandled exceptions. Without it, an unhandled exception in any component crashes the **entire app** — the UI stops responding, and users see a generic error message (or worse, nothing at all).

**Why error boundaries matter:**

In traditional server-side rendering, an exception on one page doesn't affect other pages. But in Blazor (both WASM and Server), the entire app runs as a single application instance. One unhandled exception can take down the whole thing.

`ErrorBoundary` **isolates** failures. If a component inside an error boundary throws an exception, only that boundary's content is replaced with fallback UI — the rest of the app keeps working.

**Basic usage:**

```razor
<ErrorBoundary>
    <ChildContent>
        <RiskyComponent />
    </ChildContent>
    <ErrorContent Context="ex">
        <div class="error-panel">
            <p>Something went wrong: @ex.Message</p>
        </div>
    </ErrorContent>
</ErrorBoundary>
```

**How it works:**

1. Blazor renders `<RiskyComponent />` inside the boundary
2. If `RiskyComponent` throws an unhandled exception during rendering or lifecycle methods:
   - Blazor **stops** rendering the component
   - The exception is passed to the `ErrorContent` as `Context="ex"`
   - Blazor renders the fallback UI instead
3. The rest of the app continues running normally

The `Context="ex"` parameter gives you access to the exception object, so you can display details like `ex.Message` or log the error.

**What ErrorBoundary catches:**

- **Exceptions in lifecycle methods:** `OnInitializedAsync`, `OnParametersSetAsync`, `OnAfterRenderAsync`
- **Exceptions during rendering:** Code that runs while Blazor is building the DOM (in `@code` blocks or razor markup)
- **Unhandled exceptions in event handlers:** (`@onclick`, `@onchange`) — ErrorBoundary catches these too (in interactive render modes)

> [!NOTE]
> **Blazor Web App limitation:** In apps with mixed render modes (e.g., static SSR MainLayout with interactive pages), a non-interactive ErrorBoundary cannot catch exceptions from interactive components. This does not affect pure Blazor WASM or Blazor Server apps where everything uses the same render mode.
> 
> **Fix/Workaround:** Place the ErrorBoundary **inside** the interactive subtree, or make the boundary itself interactive (set matching RenderMode), so it can catch exceptions from interactive components.

**What it DOESN'T catch:**

- **Exceptions in `Dispose()`** — When Blazor calls `Dispose()`, the component is already removed from the component tree. No parent ErrorBoundary is active anymore to catch exceptions. Additionally, `Dispose()` should never throw exceptions per .NET guidelines — catch and log errors internally instead of letting them propagate.

- **Fire-and-forget async operations** (`_ = SomeMethodAsync()`) — These run outside the component's lifecycle. Handle errors internally:

  ```csharp
  _ = Task.Run(async () =>
  {
      try
      {
          await BackgroundWorkAsync();
      }
      catch (Exception ex)
      {
          // Option 1: Just log (no UI update)
          Logger.LogError(ex, "Background work failed");
          
          // Option 2: Update UI via InvokeAsync
          await InvokeAsync(() =>
          {
              mError = ex.Message;
              StateHasChanged();
          });
      }
  });
  ```

  Use `InvokeAsync()` to safely update component state from background threads — it marshals execution back to the component's `SynchronizationContext`.

**When to use try-catch despite ErrorBoundary:**

Even though ErrorBoundary catches unhandled exceptions from event handlers, you should still use try-catch for **expected errors** (validation failures, network timeouts, API errors):

```csharp
private string? mError;

private async Task HandleSubmit()
{
    try
    {
        await SaveDataAsync().ConfigureAwait(true);
    }
    catch (ValidationException ex)
    {
        // Expected error - show user-friendly message
        mError = L.Get("validation.failed");
        StateHasChanged();
    }
    catch (HttpRequestException ex)
    {
        // Expected error - show retry option
        mError = L.Get("network.error");
        StateHasChanged();
    }
    // Unexpected errors (bugs) bubble up to ErrorBoundary
}
```

**Why handle expected errors explicitly:**

- **User experience:** Expected errors need specific, actionable messages ("Invalid email format", "Server unavailable - try again")
- **Recovery options:** You can offer retry buttons, alternative actions, or validation hints
- **ErrorBoundary is for bugs:** It shows a generic "something went wrong" message, which is appropriate for unexpected failures but not for validation or network issues

**Summary:**
- **ErrorBoundary:** Safety net for unexpected crashes (bugs you didn't anticipate)
- **try-catch:** Graceful handling of expected failures (validation, network, business rules)

**Recoverable error boundaries:**

Sometimes you want to give users a way to retry after an error. Use a reference to the `ErrorBoundary` and call `Recover()`:

```razor
<ErrorBoundary @ref="mErrorBoundary">
    <ChildContent>
        <DataGrid />
    </ChildContent>
    <ErrorContent>
        <div class="error-panel">
            <p>Failed to load data.</p>
            <button @onclick="Recover">Try Again</button>
        </div>
    </ErrorContent>
</ErrorBoundary>

@code {
    private ErrorBoundary? mErrorBoundary;
    
    private void Recover() => mErrorBoundary?.Recover();
}
```

**What `Recover()` does:**

1. Clears the error state
2. Tells Blazor to re-render the child content
3. The component starts fresh (runs `OnInitializedAsync` again)

This is useful for transient errors like network timeouts — the user clicks "Try Again" and the component reloads.

**When to use error boundaries:**

- **Around risky components:** API calls, complex calculations, user-uploaded content rendering
- **At strategic levels:** Don't wrap every component — wrap sections of the UI that can fail independently (e.g., wrap a data grid, not every grid cell)
- **Not for validation:** Use form validation (covered earlier) for user input errors, not error boundaries

**Example hierarchy:**

```razor
<MainLayout>
    <ErrorBoundary>  <!-- Catches errors in entire layout -->
        <NavBar />
        <ErrorBoundary>  <!-- Catches errors in main content only -->
            <PageContent />
        </ErrorBoundary>
        <Footer />
    </ErrorBoundary>
</MainLayout>
```

If `PageContent` crashes, only that section shows an error. The nav bar and footer keep working.

### Complete Example: Loading States with Error Recovery

Putting it all together — a component that combines **ErrorBoundary** (for unexpected crashes) with **manual error handling** (for expected failures like network errors):

```razor
@implements IDisposable

<ErrorBoundary>
    <ChildContent>
        @if (mIsLoading)
        {
            <p>Loading...</p>
        }
        else if (mError is not null)
        {
            <div class="error">
                <p>@mError</p>
                <button @onclick="RetryAsync">Retry</button>
            </div>
        }
        else
        {
            <ItemList Items="mItems" />
        }
    </ChildContent>
    <ErrorContent Context="ex">
        <div class="error-panel">
            <h3>Unexpected Error</h3>
            <p>Something went wrong: @ex.Message</p>
            <p>Please refresh the page. If the problem persists, check the browser console for details.</p>
        </div>
    </ErrorContent>
</ErrorBoundary>

@code {
    private readonly CancellationTokenSource mCts = new();
    private List<Item>? mItems;
    private bool mIsLoading = true;
    private string? mError;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync().ConfigureAwait(true);
    }

    private async Task LoadDataAsync()
    {
        mIsLoading = true;
        mError = null;
        StateHasChanged();  // Show loading message

        try
        {
            mItems = await FetchItemsAsync(mCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // User navigated away — ignore
        }
        catch (HttpRequestException ex)
        {
            // Expected network error — show retry option
            mError = $"Failed to load: {ex.Message}";
        }
        catch (Exception)
        {
            // Unexpected error (bug) — re-throw to let ErrorBoundary handle it
            // ErrorBoundary will show generic error UI
            throw;
        }
        finally
        {
            mIsLoading = false;
        }
    }

    private async Task RetryAsync()
    {
        await LoadDataAsync().ConfigureAwait(true);
    }

    public void Dispose()
    {
        mCts.Cancel();
        mCts.Dispose();
    }
}
```

**Pattern breakdown:**

1. **ErrorBoundary wraps everything** — catches unexpected exceptions (bugs, null references, etc.)
2. **Manual try-catch in LoadDataAsync** — catches expected errors (network timeouts, 404s) and stores them in `mError`
3. **Three UI states:** Loading spinner, error message with retry button, or actual content
4. **Cancellation:** Token passed to `FetchItemsAsync`, canceled on disposal
5. **Error differentiation:**
   - `HttpRequestException` → Expected, show retry button
   - Other exceptions → Unexpected, re-throw to ErrorBoundary
6. **State synchronization:** `StateHasChanged()` after setting `mIsLoading` ensures the loading message appears immediately

**Why both ErrorBoundary AND try-catch?**

- **try-catch** handles **expected** failures you want to recover from (network errors, API timeouts). These show a user-friendly message with a retry button.
- **ErrorBoundary** handles **unexpected** failures you didn't anticipate (bugs, null references). These show a generic error message without retry, since retrying a bug won't help.

This pattern gives users the best experience: graceful recovery from network issues, and clear feedback when something truly goes wrong.

---

## Performance Considerations

**Start with measurement, not optimization.** Most Blazor apps are fast enough without any performance tuning. The browser is highly optimized for rendering HTML, and Blazor's diffing algorithm is efficient.

**When performance actually matters:**
- Rendering lists with hundreds or thousands of items
- Frequent updates (real-time dashboards, live data feeds)
- Complex components that re-render on every parent update
- Heavy JSInterop usage (many calls per second)

If your app feels sluggish, **measure first**. Use browser DevTools to identify the actual bottleneck before applying optimizations. Premature optimization makes code harder to maintain without solving real problems.

### Understanding Blazor's Rendering

Before optimizing, understand what triggers renders:

**Blazor re-renders a component when:**
1. `StateHasChanged()` is called (explicitly or implicitly after event handlers)
2. A parent component re-renders (Blazor checks all child components by default)
3. A `CascadingValue` changes
4. Parameters change

The key insight: **Blazor doesn't know which children actually need to update**, so it checks them all. For most apps (small component trees, simple UIs), this overhead is negligible. For complex apps with deep hierarchies, it adds up.

### The `@key` Directive: Help Blazor Track Items

When rendering lists, Blazor needs to know which items changed. Without help, it assumes the list might have been completely reordered and re-renders everything.

**The problem without `@key`:**

```razor
@foreach (var item in mItems)
{
    <ItemComponent Item="item" />
}
```

If you remove item #3 from a 10-item list, Blazor sees:
- Old list: 10 components
- New list: 9 components
- Blazor's assumption: "Components 1-9 might have changed, component 10 disappeared"
- Result: Re-renders components 1-9, destroys component 10

**The solution with `@key`:**

```razor
@foreach (var item in mItems)
{
    <ItemComponent @key="item.Id" Item="item" />
}
```

Now Blazor tracks each component by its unique key (typically an ID). When you remove item #3:
- Blazor sees: "Item with key=3 disappeared, all others unchanged"
- Result: Only destroys component #3, leaves the rest alone

**When to use `@key`:**
- Lists where items can be added, removed, or reordered
- Any `@foreach` loop rendering components

**When NOT to use `@key`:**
- Static lists that never change
- Simple HTML elements (not worth the overhead)

The key must be **stable** (doesn't change for the same item) and **unique** (no two items share the same key). An entity ID from the database is ideal.

### CascadingValue Optimization: `IsFixed="true"`

`CascadingValue` provides data to child components without explicit parameters. By default, Blazor checks if the cascading value changed on every render cycle.

**The problem with default behavior:**

```razor
<CascadingValue Value="mTheme">
    <!-- Deep component tree -->
</CascadingValue>
```

Every time anything re-renders, Blazor checks if `mTheme` changed, even if it's a constant that never changes (like application configuration or user settings loaded at startup).

**The optimization:**

```razor
<CascadingValue Value="mTheme" IsFixed="true">
    <!-- Deep component tree -->
</CascadingValue>
```

`IsFixed="true"` tells Blazor: "This value will never change after initialization. Don't bother checking it."

**When to use `IsFixed="true"`:**
- Theme configuration
- App settings loaded once at startup
- User profile (loaded once, doesn't change during session)
- Any value that's truly constant after the component initializes

**When NOT to use it:**
- Values that actually change (current user selection, real-time data)
- When you're not sure — measure first, optimize later

> [!WARNING]
> **Reference type trap:** `IsFixed="true"` checks if the **reference** changes, not the object's contents. If you mutate the object internally (e.g., `mTheme.ColorScheme = "dark"`), child components won't re-render because Blazor "promised" the value is fixed. Only use `IsFixed="true"` when the object itself is immutable or never changes semantically after initialization.

### Large Lists: Virtualization

Rendering 10,000 list items means creating 10,000 DOM elements. This is slow in any framework — the browser has to layout and paint every element, even those scrolled out of view.

**The problem:**

```razor
@foreach (var item in mHugeList)  <!-- 10,000 items -->
{
    <div>@item.Name</div>
}
```

Result: 10,000 DOM elements, slow initial render, high memory usage, laggy scrolling.

**The solution: `Virtualize` component**

```razor
<Virtualize Items="mHugeList" Context="item">
    <div>@item.Name</div>
</Virtualize>
```

**How virtualization works:**

1. Blazor calculates how many items fit in the viewport (maybe 20 items)
2. Renders only those 20 items, plus a small buffer (maybe 5 above and below)
3. As you scroll, items leaving the viewport are destroyed
4. Items entering the viewport are created
5. Total DOM elements: ~30 instead of 10,000

**Important limitation:** The **data** (`mHugeList`) is still fully in memory. `Virtualize` only optimizes rendering, not data loading. If loading 10,000 items from the API is slow, use `ItemsProvider` instead of `Items`:

```csharp
<Virtualize ItemsProvider="LoadItemsAsync" Context="item">
    <div>@item.Name</div>
</Virtualize>

@code {
    private async ValueTask<ItemsProviderResult<Item>> LoadItemsAsync(
        ItemsProviderRequest request)
    {
        // request.StartIndex and request.Count tell you what's visible
        // Pass CancellationToken to allow canceling pending requests
        var items = await FetchItemsAsync(
            request.StartIndex, 
            request.Count, 
            request.CancellationToken
        ).ConfigureAwait(true);
        
        return new ItemsProviderResult<Item>(items, totalItemCount);
    }
}
```

Now data is loaded on-demand as the user scrolls. This works well with paginated APIs. The `CancellationToken` ensures that if the user scrolls quickly, abandoned requests are canceled rather than continuing to load data that's no longer visible.

**When to use `Virtualize`:**
- Lists with 100+ items
- Infinite scrolling scenarios
- Performance issues with list rendering (measure first!)

### Batching JSInterop Calls

Every JSInterop call has overhead — Blazor serializes the arguments, sends them to JavaScript, deserializes, executes, serializes the result, sends it back. For a single call, this is negligible. For many calls in rapid succession, it adds up.

**The problem:**

```csharp
// Setting 10 CSS properties: 10 separate JSInterop calls
foreach (var property in cssProperties)
{
    await mJsRuntime.InvokeVoidAsync("setStyle", elementId, property.Key, property.Value)
        .ConfigureAwait(true);
}
```

Each call serializes arguments, crosses the boundary, executes, and returns. 10 calls = 10× the overhead.

**The solution:**

```csharp
// Batch into one call
await mJsRuntime.InvokeVoidAsync("setStyles", elementId, cssProperties)
    .ConfigureAwait(true);
```

JavaScript side:

```javascript
window.setStyles = (elementId, properties) => {
    const element = document.getElementById(elementId);
    Object.keys(properties).forEach(key => {
        element.style[key] = properties[key];
    });
};
```

One call, one serialization, one boundary crossing.

**When to batch:**
- Multiple related operations (setting multiple properties, creating multiple elements)
- Frequent updates (animation loops, real-time data)
- Operations that naturally group together

**When NOT to batch:**
- Single calls (no overhead to avoid)
- Unrelated operations (harder to maintain, no benefit)

### ShouldRender: The Nuclear Option

⚠️ **WARNING:** This is the most dangerous performance optimization in Blazor. Use it only as a last resort after measuring and understanding exactly what's slow.

`ShouldRender()` lets you tell Blazor: "Skip rendering this component." It's dangerous because Blazor no longer automatically updates the UI — you're responsible for managing when renders happen.

**The problem it solves:**

A component deep in the tree re-renders on every parent update, even though its data never changes. After profiling, you've confirmed this component is the bottleneck.

**The pattern:**

```csharp
private bool mShouldRender = true;

protected override bool ShouldRender()
{
    if (!mShouldRender) return false;
    mShouldRender = false;  // Render once, then stop
    return true;
}

// When you actually need to render:
private void TriggerRender()
{
    mShouldRender = true;
    StateHasChanged();
}
```

**Why it's dangerous:**

If you forget to call `TriggerRender()` when data changes, the UI freezes — the component stops updating. This creates bugs that are hard to debug: "Why doesn't this button work anymore?"

**Before using `ShouldRender()`, try:**
1. Using `@key` on list items
2. Using `IsFixed="true"` on cascading values
3. Restructuring components to avoid deep hierarchies
4. Moving state closer to where it's used

**Only use `ShouldRender()` if:**
- You've measured a real performance problem
- You've tried other solutions
- You understand exactly when the component needs to render
- You're willing to manually manage render triggers

Most apps never need this. If you're reaching for it, step back and reconsider the architecture.

---

## ConfigureAwait in Blazor

> [!NOTE]
> **LumaCore context:** LumaCore UI is built as **standalone Blazor WebAssembly**, where `ConfigureAwait` technically has minimal impact (single-threaded browser environment). However, we enforce explicit `ConfigureAwait` usage as a **team standard** for two reasons: **(1) Portability** — the codebase could migrate to Blazor Server or be reused in libraries, and **(2) Best practice** — explicit intent is clearer than implicit defaults. This section explains the why behind the standard.

Understanding when to use `ConfigureAwait(true)` vs `ConfigureAwait(false)` is important for Blazor Server applications. In Blazor WASM (single-threaded), the choice has minimal practical impact — but using it correctly ensures code portability and clear intent.

> [!IMPORTANT]
> **LumaCore standard:** Never omit `ConfigureAwait()`. Always be explicit — `true` in UI code, `false` in backend code. This makes our codebase Blazor Server-ready without requiring a full rewrite if we migrate later.

### Why This Matters: The SynchronizationContext

In traditional .NET, `ConfigureAwait(false)` tells the runtime: *"I don't need to resume on the original synchronization context."* This avoids the overhead of capturing the context and posting the continuation back to it.

**The Blazor reality:**

- **Blazor WebAssembly** runs single-threaded in the browser. `ConfigureAwait(false)` makes no practical difference — there's only one thread.

- **Blazor Server** is multi-threaded with a `SynchronizationContext` per circuit. Here, `ConfigureAwait(false)` breaks `JSInterop` calls and UI updates — the continuation runs on a different thread outside the Blazor context.

**Why we require `ConfigureAwait(true)` in UI code:**

1. **Portability:** If LumaCore migrates to Blazor Server later, we won't need to rewrite async patterns.
2. **Consistency:** One rule is easier to remember than "it depends on the hosting model."
3. **Intent documentation:** Explicit `ConfigureAwait(true)` signals "this code needs the UI context."

It costs nothing in WASM performance and prevents bugs in Server.

### The Decision: Where Does the Code Run?

**The rule is simple:** It's not about *what* is called, but *where* the code runs and *what follows the await*.

| Execution Context | ConfigureAwait | Reason |
|-------------------|----------------|--------|
| **Backend services** (no `IJSRuntime`, no UI) | `ConfigureAwait(false)` | No UI context needed, continuation can run anywhere |
| **Library code** (reusable, context-agnostic) | `ConfigureAwait(false)` | Caller decides context, library shouldn't capture |
| **.razor files** (components) | `ConfigureAwait(true)` | UI code, may call `StateHasChanged` or `JSInterop` after |
| **UI services** (inject `IJSRuntime`) | `ConfigureAwait(true)` | Need browser context for `JSInterop` |
| **Any code before `StateHasChanged()`** | `ConfigureAwait(true)` | UI update requires Blazor context |

**The simple test:** Ask yourself, *"Does this code need the browser to work?"* If yes, use `ConfigureAwait(true)`.

### Service Classification: UI vs. Backend

Services in Blazor projects fall into two categories:

**Backend services** don't interact with the browser. They make HTTP calls, access databases, perform calculations, or call external APIs:

```csharp
// AuthService.cs — backend service
public async Task<LoginResult> LoginAsync(string username, string password)
{
    // HTTP call — no browser interaction
    var response = await mHttpClient
        .PostAsJsonAsync("api/v1/auth/login", new LoginRequest(username, password))
        .ConfigureAwait(false);  // ✅ Correct for backend services

    // ... process response
}
```

**UI services** use `JSInterop` to communicate with the browser. They manipulate the DOM, show notifications, or interact with JavaScript libraries:

```csharp
// ToastService.cs — UI service
public async Task ShowSuccessAsync(string message)
{
    // JSInterop — needs browser context
    await mJsRuntime
        .InvokeVoidAsync("showToast", message, "success")
        .ConfigureAwait(true);  // ✅ Explicit: UI service stays on context
}
```

**Quick check:** If the constructor injects `IJSRuntime`, it's a UI service.

### Razor Components: Always Stay on Context

Code inside `.razor` files is inherently UI code. Always use `ConfigureAwait(true)`:

```razor
@code {
    private async Task HandleButtonClickAsync()
    {
        // Even though this is "just" an HTTP call, we're in a component
        var data = await mDataService.LoadAsync().ConfigureAwait(true);
        
        // Update component state
        mItems = data;
        
        // Blazor automatically calls StateHasChanged after event handlers
    }
}
```

**Important:** An HTTP call in a Razor component still needs `ConfigureAwait(true)` — because the *continuation* runs in UI context. What matters is not the operation, but what code runs after the `await`.

Even if `mDataService.LoadAsync()` internally uses `ConfigureAwait(false)` (which it should, as a backend service), the component's call site uses `ConfigureAwait(true)` to stay on the Blazor context.

### Common Mistakes

**❌ Using `ConfigureAwait(false)` with JSInterop:**
```csharp
// Works in WASM, breaks in Blazor Server!
await mJsRuntime.InvokeVoidAsync("alert", message).ConfigureAwait(false);
```

**❌ Omitting ConfigureAwait entirely:**
```csharp
// Implicit ConfigureAwait(true), but unclear intent
await mJsRuntime.InvokeVoidAsync("alert", message);
```

**✅ Explicit intent:**
```csharp
// Clear: this code needs the UI context
await mJsRuntime.InvokeVoidAsync("alert", message).ConfigureAwait(true);
```

### Summary: ConfigureAwait at a Glance

| ✅ DO | ❌ DON'T |
|-------|----------|
| Use `ConfigureAwait(false)` in backend services | Use `ConfigureAwait(false)` with `JSInterop` |
| Use `ConfigureAwait(true)` in UI services and .razor files | Omit `ConfigureAwait()` — always be explicit |
| Check for `IJSRuntime` injection to identify UI services | Assume WASM behavior applies to Server |

---

## Troubleshooting Guide

This section provides solutions to the most common Blazor issues, with explanations of **why** they happen and **why** the fixes work. Understanding the root cause helps you solve similar problems on your own.

### "My JavaScript Does Nothing"

**What you see:** You call a JavaScript function, but nothing happens. No error in the console, no visible effect.

**Why it happens:** `OnInitializedAsync` runs **before** the first render. When you call JavaScript from `OnInitializedAsync`, the component hasn't rendered yet — the DOM elements don't exist.

**The timing sequence:**

```
1. OnInitializedAsync() starts
2. You call JavaScript → element doesn't exist yet
3. First await in OnInitializedAsync()
   ↓
   ══════════════════════════════════════
   Blazor renders (DOM is updated now)
   ══════════════════════════════════════
   ↓
4. OnAfterRenderAsync(firstRender: true) runs
   → Too late, JavaScript already ran in step 2
```

For the complete lifecycle details, see [Understanding the Component Lifecycle](#understanding-the-component-lifecycle).

**The fix:** Move the JSInterop call to `OnAfterRenderAsync`, which runs **after** the DOM is updated:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        // DOM is guaranteed to exist now
        await mJsRuntime.InvokeVoidAsync("initializeWidget", "my-element")
            .ConfigureAwait(true);
    }
}
```

**For conditional elements** (`@if (mShowWidget)`), you need the flag pattern because the element only exists when the condition is true. See [Conditional Rendering](#the-tricky-part-conditional-rendering) for the complete pattern.

### "My UI Doesn't Update"

**What you see:** You change a property's value, but the UI stays the same.

**Why it happens:** Blazor only re-renders automatically after **its own** event handlers (`@onclick`, `@onchange`, etc.). When state changes come from **external sources** — JavaScript callbacks, timer events, service notifications — Blazor doesn't know anything changed.

**The synchronization issue:**

External events happen outside Blazor's render cycle:

**In Blazor WASM** (single-threaded):
- Everything runs on the browser's main thread
- But external events (JS callbacks, timers) run outside Blazor's control
- `StateHasChanged()` must run in Blazor's `SynchronizationContext` to trigger a render

**In Blazor Server** (multi-threaded):
- External events often run on different threads:
  - Timer events: ThreadPool threads
  - SignalR messages: Network threads
  - JavaScript callbacks: Blazor's dispatcher thread (but not necessarily the render context)
- Calling `StateHasChanged()` from the wrong thread causes exceptions

**The fix: `InvokeAsync`**

```csharp
// JavaScript callback via DotNetObjectReference
[JSInvokable]
public async Task OnJsCallback(string data)
{
    // This might run on JS thread!
    await InvokeAsync(() =>
    {
        // Now we're safely on Blazor's UI thread
        mData = data;
        StateHasChanged();  // Safe to call here
    }).ConfigureAwait(true);
}
```

**What `InvokeAsync` does:**

Ensures the code runs on Blazor's `SynchronizationContext`, which allows `StateHasChanged()` to safely trigger a render. It marshals execution to the correct context if needed.

**For JavaScript callbacks specifically,** see [Calling Blazor from JavaScript](#calling-blazor-from-javascript) for the complete pattern with `DotNetObjectReference` and cleanup.

### "Cannot Read Property of Null" in JavaScript

**What you see:** JavaScript throws `Cannot read property 'X' of null` or `undefined is not an object`.

**Why it happens:** JavaScript tried to access a DOM element that doesn't exist. This is almost always a **lifecycle issue** — the code assumes an element exists when it doesn't.

**Common causes:**

1. **Calling JSInterop before the element renders** — Use `OnAfterRenderAsync`, not `OnInitializedAsync`
2. **Element is inside `@if (false)`** — The condition is `false`, so the element doesn't exist. Use the flag pattern to wait until after it renders.
3. **Multiple component instances with the same ID** — JavaScript finds the wrong element (or one that's already been destroyed). Use `ElementReference` instead of IDs.

**Why defensive null checks in JavaScript are wrong:**

```javascript
// BAD: Hiding the problem
function initWidget(elementId) {
    const element = document.getElementById(elementId);
    if (!element) return;  // ❌ Silently fails, hard to debug
    // ...
}
```

If JavaScript regularly receives `null` elements, the **Blazor code has a structural problem**. Fix the lifecycle issue in C#, don't work around it in JavaScript.

**The right approach:** Ensure the element exists **before** calling JavaScript. See [Conditional Rendering](#the-tricky-part-conditional-rendering) for the flag pattern.

### "Events Fire Multiple Times"

**What you see:** A click handler runs twice, or effects accumulate (tooltips stack up, animations repeat).

**Why it happens:** JavaScript event listeners are **added** when the component mounts but **never removed** when it's destroyed. Each time you navigate to the page, another listener is added. Visit the page 5 times, the handler fires 5 times.

**The lifecycle problem:**

```javascript
// WRONG: Adds listener, never removes it
export function initialize(element, callback) {
    element.addEventListener('click', () => callback.invokeMethodAsync('OnClick'));
}
```

When the Blazor component is destroyed (user navigates away), the JavaScript listener stays attached. The old listener still has a reference to the old component via `callback`. When you navigate back, a **new** component is created with a **new** callback, but the old listener is still there.

**The fix: Use MutationObserver for automatic cleanup**

Instead of calling cleanup from `DisposeAsync` (which is unreliable because the DOM might already be gone), use JavaScript's `MutationObserver` to detect when the element is removed:

```javascript
export function initialize(element, callback) {
    const handler = () => callback.invokeMethodAsync('OnClick');
    element.addEventListener('click', handler);
    
    // Automatically cleanup when element is removed from DOM
    const observer = new MutationObserver(() => {
        if (!document.body.contains(element)) {
            element.removeEventListener('click', handler);
            observer.disconnect();
        }
    });
    
    observer.observe(document.body, { childList: true, subtree: true });
}
```

**How it works:**

1. `MutationObserver` watches for DOM changes (elements added/removed)
2. On each change, it checks if our element still exists in the document
3. When the element is removed (component destroyed), it automatically removes the listener and stops observing
4. No JSInterop from `DisposeAsync` needed — cleanup happens purely in JavaScript

**Why this is better than cleanup in DisposeAsync:**

- **Reliable:** Works even if `DisposeAsync` can't call JavaScript (renderer disposed, DOM already gone)
- **Automatic:** No chance of forgetting to call cleanup
- **No exceptions:** Doesn't depend on JSInterop working during disposal

C# side remains simple:

```csharp
public async ValueTask DisposeAsync()
{
    // Just dispose the module reference — JavaScript handles cleanup automatically
    if (mModule is not null)
    {
        await mModule.DisposeAsync().ConfigureAwait(true);
    }
}
```

**Why this works:** When the component is destroyed, `DisposeAsync` removes the event listener. The next time you visit the page, only **one** listener exists.

See [ES Modules and Cleanup](#es-modules-and-cleanup) for the complete pattern with module imports and safe disposal.

### CSS Isolation Not Working

**What you see:** You create a `MyComponent.razor.css` file next to the component, but the styles aren't applied.

**Why it happens:** CSS Isolation relies on build-time tooling that generates a `*.styles.css` bundle. If the bundle isn't created or referenced, styles don't load. This can fail for several reasons:
- File naming doesn't match exactly (`MyComponent.razor.css` must match `MyComponent.razor`)
- The bundle isn't referenced in `index.html`
- Build cache is stale (happens after moving files or changing structure)

**Troubleshooting checklist:**

1. **File name must match exactly:** `Login.razor` → `Login.razor.css` (case-sensitive)
2. **Bundle must be referenced in `index.html`:**
   ```html
   <link href="ProjectName.styles.css" rel="stylesheet" />
   ```
3. **Clean rebuild:** `dotnet clean && dotnet build` (clears build cache)

**If it still doesn't work:**

CSS Isolation has known issues with certain folder structures and project templates. See [Issue #53248](https://github.com/dotnet/aspnetcore/issues/53248) for documented cases where it fails.

**The reliable alternative: HeadContent**

Put CSS in `wwwroot/css/` and use `<HeadContent>` to include it per-page:

```razor
@page "/login"

<HeadContent>
    <link rel="stylesheet" href="css/login.css" />
</HeadContent>

<div class="login-container">
    ...
</div>
```

**Why this works:**

`HeadContent` injects content into the `<head>` tag at runtime (requires `<HeadOutlet>` registered in `Program.cs`). This bypasses the build-time CSS Isolation tooling entirely. The CSS is loaded only on pages that need it, similar to CSS Isolation, but without the fragile build dependencies.

> [!WARNING]
> **Trade-off: Global CSS scope**
> 
> `HeadContent` loads CSS globally — styles aren't scoped to the component. This means selectors like `.container` will affect **all** elements on the page with that class, not just your component.
> 
> **Recommended:** Use a naming convention to avoid conflicts:
> - Prefix with component name: `.login-container`, `.login-button`
> - Or use a project prefix: `.lc-login-container`, `.lc-login-button`
> 
> This explicit naming makes the global scope manageable and prevents accidental style collisions across components.

---

## Summary

Blazor development becomes predictable once you internalize a few key concepts:

1. **Blazor may render as soon as an async lifecycle method yields** (often at its first `await`). The component renders *before* `OnInitializedAsync` completes. Always use null-checks and loading states.

2. **Unconditional DOM elements exist after render.** Use `OnAfterRenderAsync` for JSInterop, not `OnInitializedAsync`.

3. **Conditional elements need the flag pattern.** Set a flag when you want JavaScript to run, and check it after the next render.

4. **State changes from external sources need `StateHasChanged()`.** Blazor doesn't know about callbacks from JavaScript or service events. Wrap in `InvokeAsync(StateHasChanged)` for thread safety.

5. **Don't replace form models — mutate them.** Replacing `EditForm.Model` resets validation and can destroy child components.

6. **Cancel async operations on disposal.** Use `CancellationTokenSource` and cancel in `Dispose()` to prevent exceptions from operations completing after the component is gone.

7. **Use ErrorBoundary for unexpected failures, try-catch for expected errors.** ErrorBoundary catches unhandled exceptions (bugs). Use try-catch for validation, network errors, and business rules that need user-friendly messages.

8. **Clean up after yourself.** Implement `IAsyncDisposable` when you add event listeners or other resources. Handle `JSDisconnectedException` in cleanup code.

9. **If CSS Isolation doesn't work, check bundling first.** If it's still broken, use `wwwroot/css/` with `<HeadContent>` as a reliable fallback.

10. **Always be explicit with `ConfigureAwait()`.** Use `true` in UI code (.razor files, UI services), `false` in backend services. Never omit it.

---

## Related Documentation

For LumaCore-specific coding conventions (field naming, XML documentation, etc.), see [Coding Standards](../coding-standards.md).

---

© 2025 LumaCoreTech • MIT License