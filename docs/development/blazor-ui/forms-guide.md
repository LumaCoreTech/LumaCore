# Forms and Validation Guide

Forms are where Blazor's data binding meets user input. Unlike traditional HTML forms that post data to a server, Blazor forms work entirely in C# — validating input, handling submission, and managing state without page reloads.

**What makes Blazor forms special:**
- **Two-way data binding:** Changes in the UI update C# properties (and vice versa)
- **Client-side validation:** Validation runs in the browser before any server calls
- **Strongly typed:** Form fields bind to C# model properties, catching errors at compile time
- **Localization-ready:** Validation messages can be pulled from the translation system

### The EditForm Component

At the heart of Blazor forms is `EditForm` — a component that wraps your inputs and manages validation state. When you submit the form, `EditForm` validates all fields and only fires your submit handler if everything passes.

**How it works:**

1. `EditForm` creates an `EditContext` from your model object
2. Input components (`InputText`, `InputNumber`, etc.) register with the `EditContext`
3. When the user changes a field and it loses focus (blur), Blazor updates the bound C# property
4. When the user submits, `EditContext` validates all properties
5. If valid → `OnValidSubmit` fires. If invalid → `OnInvalidSubmit` fires (or nothing if you only use `OnValidSubmit`)

The `EditContext` is the state manager — it tracks which fields have been modified, which have validation errors, and whether the form as a whole is valid.

### Basic Form Structure

Here's a minimal login form to see the pieces in action:

```csharp
@using System.ComponentModel.DataAnnotations
@inject LocalizationService L

<EditForm Model="@mUser" OnValidSubmit="@HandleSubmit">
    <DataAnnotationsValidator />
    <ValidationSummary />

    <div>
        <label>@L.Get("components.login.fields.username")</label>
        <InputText @bind-Value="mUser.Username" />
        <ValidationMessage For="@(() => mUser.Username)" />
    </div>

    <div>
        <label>@L.Get("components.login.fields.password")</label>
        <InputText type="password" @bind-Value="mUser.Password" />
        <ValidationMessage For="@(() => mUser.Password)" />
    </div>

    <button type="submit">@L.Get("components.login.submit")</button>
</EditForm>

@code {
    private UserModel mUser = new();

    private async Task HandleSubmit()
    {
        // This only runs if validation passes
        await LoginAsync(mUser).ConfigureAwait(true);
    }

    public class UserModel
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }
}
```

> [!NOTE]
> This example uses default validation messages for simplicity. For localized validation messages, see [Localized Validation Messages](#localized-validation-messages) below.

**Let's break this down:**

**`<EditForm Model="@mUser" OnValidSubmit="@HandleSubmit">`**
- `Model=` tells EditForm which object to validate
- `OnValidSubmit=` is the method to call when validation passes
- EditForm creates an `EditContext` internally from the model

**`<DataAnnotationsValidator />`**
- Enables validation via attributes like `[Required]`, `[StringLength]`, etc.
- Without this component, attributes are ignored

**`<ValidationSummary />`**
- Shows all validation errors in one place (usually at the top of the form)
- Useful for giving users an overview of what's wrong

**`<InputText @bind-Value="mUser.Username" />`**
- Blazor's built-in text input component (renders an `<input type="text">`)
- `@bind-Value=` creates two-way binding to the model property
- Updates the C# property when the field loses focus (on blur)

**`<ValidationMessage For="@(() => mUser.Username)" />`**
- Shows validation errors for a specific field
- The lambda `@(() => mUser.Username)` tells Blazor which property to watch
- Only displays errors after the field loses focus or form submission

**Why use `InputText` instead of `<input>`?**

Blazor's input components (`InputText`, `InputNumber`, `InputDate`, etc.) integrate with `EditContext`:
- They trigger validation when the field loses focus
- They apply CSS classes (`valid`, `invalid`, `modified`) for styling
- They handle type conversion (e.g., `InputNumber` converts strings to `int`)

You *can* use regular `<input @bind="mUser.Username">` but you lose validation integration.

**Note on binding timing:** By default, `@bind-Value` updates on blur (when field loses focus). If you need real-time updates while typing, use `@bind-Value:event="oninput"`:

```html
<InputText @bind-Value="mUser.Username" @bind-Value:event="oninput" />
```

This updates the property on every keystroke, but can impact performance with complex validation.

### Localized Validation Messages

Hardcoded error messages like `"Username is required"` don't work in multilingual apps. LumaCore supports two approaches for localized validation: **custom attributes** (recommended for most cases) and **manual validation** (for complex scenarios).

#### Approach 1: Custom Validation Attributes (Recommended)

Custom validation attributes live in `LumaCore.Ui.Core/Validation/` and pull messages from `LocalizationService`. This approach keeps validation logic reusable and works with standard `DataAnnotationsValidator`.

**The attribute:**

```csharp
using System.ComponentModel.DataAnnotations;
using LumaCore.Ui.Core.Services;

namespace LumaCore.Ui.Core.Validation;

/// <summary>
/// Validation attribute that uses <see cref="LocalizationService"/> for error messages.
/// </summary>
public class LocalizedRequiredAttribute : RequiredAttribute
{
    private readonly string mTranslationKey;

    /// <summary>
    /// Initializes a new instance with the translation key for the error message.
    /// </summary>
    /// <param name="translationKey">Translation key (e.g., "components.login.validation.usernameRequired")</param>
    public LocalizedRequiredAttribute(string translationKey)
    {
        mTranslationKey = translationKey;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || (value is string str && string.IsNullOrWhiteSpace(str)))
        {
            // Get LocalizationService from DI
            var localization = (LocalizationService?)validationContext.GetService(typeof(LocalizationService));
            string message = localization?.Get(mTranslationKey) ?? $"?? {mTranslationKey} ??";
            
            return new ValidationResult(message);
        }

        return ValidationResult.Success;
    }
}
```

**Login form with attributes:**

```csharp
@using LumaCore.Ui.Core.Validation
@inject LocalizationService L

<EditForm Model="@mUser" OnValidSubmit="@HandleSubmit">
    <DataAnnotationsValidator />
    
    <div>
        <label>@L.Get("components.login.fields.username")</label>
        <InputText @bind-Value="mUser.Username" />
        <ValidationMessage For="@(() => mUser.Username)" />
    </div>

    <div>
        <label>@L.Get("components.login.fields.password")</label>
        <InputText type="password" @bind-Value="mUser.Password" />
        <ValidationMessage For="@(() => mUser.Password)" />
    </div>

    <button type="submit">@L.Get("components.login.submit")</button>
</EditForm>

@code {
    private UserModel mUser = new();

    private async Task HandleSubmit()
    {
        await LoginAsync(mUser).ConfigureAwait(true);
    }

    public class UserModel
    {
        [LocalizedRequired("components.login.validation.usernameRequired")]
        public string Username { get; set; } = string.Empty;

        [LocalizedRequired("components.login.validation.passwordRequired")]
        public string Password { get; set; } = string.Empty;
    }
}
```

**Pros:**
- Validation logic in one place (the attribute)
- Reusable across multiple forms
- Works with standard `DataAnnotationsValidator`
- Less code in each component

**Cons:**
- Need to create custom attributes for each validation type
- Slightly more complex setup (ValidationContext.GetService)

#### Approach 2: Manual Validation

Manual validation gives you full control and is useful for complex, form-specific validation rules that don't fit into attributes.

**Same login form with manual validation:**

```csharp
@inject LocalizationService L

<EditForm EditContext="@mEditContext" OnValidSubmit="@HandleSubmit">
    <div>
        <label>@L.Get("components.login.fields.username")</label>
        <InputText @bind-Value="mUser.Username" />
        <ValidationMessage For="@(() => mUser.Username)" />
    </div>

    <div>
        <label>@L.Get("components.login.fields.password")</label>
        <InputText type="password" @bind-Value="mUser.Password" />
        <ValidationMessage For="@(() => mUser.Password)" />
    </div>

    <button type="submit">@L.Get("components.login.submit")</button>
</EditForm>

@code {
    private UserModel mUser = new();
    private EditContext? mEditContext;
    private ValidationMessageStore? mMessageStore;

    protected override void OnInitialized()
    {
        mEditContext = new EditContext(mUser);
        mMessageStore = new ValidationMessageStore(mEditContext);
        
        // Hook validation
        mEditContext.OnValidationRequested += (s, e) => ValidateForm();
    }

    private void ValidateForm()
    {
        mMessageStore?.Clear();

        // Username validation
        if (string.IsNullOrWhiteSpace(mUser.Username))
        {
            mMessageStore?.Add(() => mUser.Username, 
                L.Get("components.login.validation.usernameRequired"));
        }

        // Password validation
        if (string.IsNullOrWhiteSpace(mUser.Password))
        {
            mMessageStore?.Add(() => mUser.Password, 
                L.Get("components.login.validation.passwordRequired"));
        }

        mEditContext?.NotifyValidationStateChanged();
    }

    private async Task HandleSubmit()
    {
        await LoginAsync(mUser).ConfigureAwait(true);
    }

    public class UserModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
```

**Pros:**
- Full control over validation logic
- Easy to implement complex, form-specific rules
- Direct access to `L.Get()` - no workarounds needed

**Cons:**
- More boilerplate in each component
- Validation logic duplicated across forms
- Manual EditContext setup required

#### When to Use Which Approach

**Use custom attributes when:**
- Validation is standard (required, length, format, etc.)
- You'll use the same validation across multiple forms
- You want less code in components

**Use manual validation when:**
- Validation has complex business rules
- Rules are specific to one form
- You need conditional validation based on other fields

**Example of complex validation requiring manual approach:**

```csharp
private void ValidateBooking()
{
    mMessageStore?.Clear();

    // Simple validations could still use attributes, but complex ones need manual handling
    if (mBooking.StartDate > mBooking.EndDate)
    {
        mMessageStore?.Add(() => mBooking.EndDate, 
            L.Get("components.booking.validation.endDateBeforeStart"));
    }

    if (mBooking.Guests > mBooking.MaxCapacity)
    {
        mMessageStore?.Add(() => mBooking.Guests,
            L.Get("components.booking.validation.exceedsCapacity")
                .Replace("{max}", mBooking.MaxCapacity.ToString()));
    }

    mEditContext?.NotifyValidationStateChanged();
}
```

#### Creating Additional Attributes

For other validations like `StringLength` or `EmailAddress`, create similar attributes in `LumaCore.Ui.Core/Validation/`:

```csharp
public class LocalizedStringLengthAttribute : StringLengthAttribute
{
    private readonly string mTranslationKey;

    public LocalizedStringLengthAttribute(int maximumLength, string translationKey) 
        : base(maximumLength)
    {
        mTranslationKey = translationKey;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var result = base.IsValid(value, validationContext);
        
        if (result != ValidationResult.Success)
        {
            var localization = (LocalizationService?)validationContext.GetService(typeof(LocalizationService));
            string message = localization?.Get(mTranslationKey) ?? $"?? {mTranslationKey} ??";
            return new ValidationResult(message);
        }

        return ValidationResult.Success;
    }
}
```

#### Translation Keys

Both approaches use the same translation keys in `translations.json`:

```json
{
  "components": {
    "login": {
      "validation": {
        "usernameRequired": "Username is required.",
        "passwordRequired": "Password is required."
      }
    }
  }
}
```

### Form Submission Events

`EditForm` provides three events for handling submission:

**`OnValidSubmit`** — Fires only if validation passes:

```html
<EditForm Model="@mUser" OnValidSubmit="@HandleValidSubmit">
```

This is the most common choice. Blazor validates all fields, and if everything is valid, your method runs. If validation fails, nothing happens (except validation messages appear).

**`OnInvalidSubmit`** — Fires only if validation fails:

```html
<EditForm Model="@mUser" 
          OnValidSubmit="@HandleValid" 
          OnInvalidSubmit="@HandleInvalid">
```

Useful for logging, analytics, or showing custom error UI when the form is invalid.

**`OnSubmit`** — Fires on every submission (you must validate manually):

```html
<EditForm Model="@mUser" OnSubmit="@HandleSubmit">
```

With `OnSubmit`, you're responsible for calling `EditContext.Validate()` yourself. Use this when you need custom validation logic that goes beyond data annotations — for example, checking if a username is already taken via API call.

**Example with custom validation:**

```csharp
@inject LocalizationService L

private async Task HandleSubmit(EditContext editContext)
{
    // First, run data annotation validation
    if (!editContext.Validate())
    {
        // Built-in validation failed, stop here
        return;
    }

    // Custom async validation
    bool usernameExists = await CheckUsernameExistsAsync(mUser.Username);
    if (usernameExists)
    {
        // Add custom error to the form
        var messageStore = new ValidationMessageStore(editContext);
        messageStore.Add(() => mUser.Username, 
            L.Get("components.login.validation.usernameTaken"));
        editContext.NotifyValidationStateChanged();
        return;
    }

    // Everything valid, proceed
    await SaveUserAsync(mUser);
}
```

### Common Pitfall: Replacing the Model

**The problem:**

```csharp
// User fills in the form
mUser.Username = "alice";
mUser.Password = "secret123";

// Later, you want to clear the form:
mUser = new UserModel();  // ❌ This breaks validation!
```

When you replace the model object, `EditForm` detects the change and creates a **new** `EditContext`. The new context doesn't know about any previous validation state — which fields were touched, which had errors, etc. Everything resets.

**Even worse:** If you have child components that depend on the model, they may be destroyed and recreated, losing their internal state.

**The solution:**

Mutate the existing object instead of replacing it:

```csharp
// ✅ Reset properties individually
mUser.Username = string.Empty;
mUser.Password = string.Empty;

// If you have many properties, create a Reset method:
public class UserModel
{
    public void Reset()
    {
        Username = string.Empty;
        Password = string.Empty;
    }
}

// Then call it:
mUser.Reset();
```

**When you *must* replace the model:**

Sometimes you need to replace the entire model — for example, when loading user data from an API:

```csharp
mUser = await LoadUserAsync(userId);  // Different object
```

In this case:
- Validation state will reset (that's usually fine for a fresh data load)
- Consider showing a loading state while fetching data
- If you have complex child components, consider using `@key` to control their lifecycle

### Server-Side Validation Errors

Client-side validation (data annotations) catches obvious errors, but some validation can only happen on the server — checking if an email is already registered, verifying a promo code, etc.

When the API returns validation errors, you need to display them in the form. This requires using `EditContext` directly:

```csharp
@inject LocalizationService L

<EditForm EditContext="@mEditContext" OnValidSubmit="@HandleSubmit">
    <DataAnnotationsValidator />
    <ValidationSummary />
    
    <div>
        <label>@L.Get("components.register.fields.email")</label>
        <InputText @bind-Value="mUser.Email" />
        <ValidationMessage For="@(() => mUser.Email)" />
    </div>
    
    <button type="submit">@L.Get("components.register.submit")</button>
</EditForm>

@code {
    private EditContext? mEditContext;
    private ValidationMessageStore? mMessageStore;
    private UserModel mUser = new();

    protected override void OnInitialized()
    {
        mEditContext = new EditContext(mUser);
        mMessageStore = new ValidationMessageStore(mEditContext);
    }

    private async Task HandleSubmit()
    {
        // Clear previous server-side errors
        mMessageStore?.Clear();

        var result = await RegisterUserAsync(mUser).ConfigureAwait(true);

        if (!result.Success)
        {
            // API returned validation errors
            foreach (var error in result.Errors)
            {
                // error.FieldName = "Email", error.Message = "This email is already registered"
                var fieldIdentifier = new FieldIdentifier(mUser, error.FieldName);
                mMessageStore?.Add(fieldIdentifier, error.Message);
            }
            
            // Notify Blazor that validation state changed
            mEditContext?.NotifyValidationStateChanged();
        }
    }
}
```

**Key differences from basic form:**

**`EditContext="@mEditContext"` instead of `Model="@mUser"`**
- When you need to manipulate the `EditContext` (adding server errors), create it yourself
- `EditContext` still needs a model — you pass it in `OnInitialized`

**`ValidationMessageStore`**
- This is where you add custom validation errors
- `Add(fieldIdentifier, message)` associates an error with a specific property
- `Clear()` removes all previously added errors (important before re-submitting!)

**`NotifyValidationStateChanged()`**
- After adding errors, call this to trigger UI update
- Without it, validation messages won't appear

**Why clear errors before re-submit?**

If the user fixes the error and re-submits, you don't want old server errors lingering. Always `Clear()` before making the API call.

**FieldIdentifier:**

This tells Blazor which property has the error:

```csharp
var fieldIdentifier = new FieldIdentifier(mUser, "Email");
```

The string must match the property name exactly (case-sensitive). If your API returns a different field name format (e.g., `email` instead of `Email`), you'll need to map it.

### Validation Styling

Blazor automatically applies CSS classes to form inputs based on validation state:

- `valid` — Field has been validated and passed
- `invalid` — Field has validation errors
- `modified` — Field has been changed from its initial value

You can use these classes for styling:

```css
.invalid {
    border-color: #dc3545;
}

.valid {
    border-color: #28a745;
}

.validation-message {
    color: #dc3545;
    font-size: 0.875rem;
    margin-top: 0.25rem;
}
```

The classes are applied automatically by `InputText`, `InputNumber`, etc. — you just need to provide the CSS.

---

