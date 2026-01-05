# Blazor Development Cheatsheet

Quick reference for common Blazor patterns in LumaCore.

## Lifecycle Methods

```csharp
protected override void OnInitialized()
{
    // First time component initializes (sync)
}

protected override async Task OnInitializedAsync()
{
    // First time component initializes (async)
    await LoadDataAsync().ConfigureAwait(true);
}

protected override void OnParametersSet()
{
    // After parameters are set (every render)
}

protected override async Task OnParametersSetAsync()
{
    // After parameters are set (async, every render)
}

protected override void OnAfterRender(bool firstRender)
{
    // After component renders to DOM
    if (firstRender)
    {
        // Only on first render
    }
}

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    // After component renders to DOM (async)
    if (firstRender)
    {
        await JsRuntime.InvokeVoidAsync("initComponent").ConfigureAwait(true);
    }
}
```

## Event Handling

```csharp
// Button click
<button @onclick="HandleClick">Click</button>

@code {
    private void HandleClick()
    {
        // Sync handler
    }
    
    private async Task HandleClickAsync()
    {
        // Async handler
        await SaveAsync().ConfigureAwait(true);
    }
}

// Input change (on blur)
<input @bind="mValue" />

// Input change (on every keystroke)
<input @bind="mValue" @bind:event="oninput" />

// Prevent default
<form @onsubmit:preventDefault>
    <button type="submit" @onclick="HandleSubmit">Submit</button>
</form>

// Stop propagation
<div @onclick="HandleOuter">
    <button @onclick:stopPropagation="true" @onclick="HandleInner">
        Click
    </button>
</div>
```

## Component Parameters

```csharp
[Parameter]
public string Title { get; set; } = string.Empty;

[Parameter]
public EventCallback<string> OnValueChanged { get; set; }

[Parameter]
public RenderFragment? ChildContent { get; set; }

// Usage
<MyComponent Title="Hello" OnValueChanged="HandleChange">
    <p>Child content here</p>
</MyComponent>
```

## Forms & Validation

```csharp
<EditForm Model="@mUser" OnValidSubmit="@HandleSubmit">
    <DataAnnotationsValidator />
    <ValidationSummary />
    
    <InputText @bind-Value="mUser.Username" />
    <ValidationMessage For="@(() => mUser.Username)" />
    
    <button type="submit">Save</button>
</EditForm>

@code {
    private UserModel mUser = new();
    
    private async Task HandleSubmit()
    {
        await SaveAsync(mUser).ConfigureAwait(true);
    }
}
```

## JavaScript Interop

```csharp
@inject IJSRuntime JsRuntime
@implements IAsyncDisposable

@code {
    private IJSObjectReference? mModule;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            mModule = await JsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./Components/MyComponent.razor.js")
                .ConfigureAwait(true);
                
            await mModule.InvokeVoidAsync("init").ConfigureAwait(true);
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        if (mModule is not null)
        {
            await mModule.DisposeAsync().ConfigureAwait(false);
        }
    }
}
```

## State Management

```csharp
// Service-based state
public class AppState
{
    private string mCurrentTheme = "light";
    private readonly object mLock = new();
    
    public event EventHandler? ThemeChanged;
    
    public string CurrentTheme
    {
        get
        {
            lock (mLock) { return mCurrentTheme; }
        }
        set
        {
            lock (mLock) { mCurrentTheme = value; }
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

// Component subscription
@inject AppState State
@implements IDisposable

@code {
    protected override void OnInitialized()
    {
        State.ThemeChanged += OnThemeChanged;
    }
    
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _ = InvokeAsync(StateHasChanged);
    }
    
    public void Dispose()
    {
        State.ThemeChanged -= OnThemeChanged;
    }
}
```

## Localization

```csharp
@inject LocalizationService L

<h1>@L.Get("components.login.title")</h1>
<button>@L.Get("common.buttons.save")</button>
```

## Error Handling

```csharp
@implements IDisposable

@code {
    private readonly CancellationTokenSource mCts = new();
    
    protected override async Task OnInitializedAsync()
    {
        try
        {
            await LoadDataAsync(mCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Component disposed, this is expected
        }
        catch (Exception ex)
        {
            // Handle error
        }
    }
    
    public void Dispose()
    {
        mCts.Cancel();
        mCts.Dispose();
    }
}
```

## Common Patterns

```csharp
// Conditional rendering
@if (mIsLoading)
{
    <p>Loading...</p>
}
else if (mError is not null)
{
    <p>Error: @mError</p>
}
else
{
    <DataDisplay Data="@mData" />
}

// List rendering
@foreach (var item in mItems)
{
    <div @key="item.Id">
        @item.Name
    </div>
}

// Conditional CSS class
<div class="card @(mIsActive ? "active" : "")">
    Content
</div>

// Inline style
<div style="color: @mColor; font-size: @(mSize)px;">
    Styled content
</div>
```

## ConfigureAwait

```csharp
// In event handlers (UI context required)
await SaveAsync().ConfigureAwait(true);
StateHasChanged();

// In Dispose (no UI context needed)
await CleanupAsync().ConfigureAwait(false);
```

For detailed explanations, see the [Blazor Guide](blazor-guide.md).
