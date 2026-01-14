# LumaCore Localization Guide

Building applications that speak users' language isn't just about translating text — it's about creating an experience that feels native. The LumaCore localization framework prioritizes architectural integrity and long-term maintainability, providing a secure, scalable solution optimized for self-hosted environments.

> **Just want to use translations?** Jump straight to the [Developer Guide](#-developer-guide-implementing-localization).

## Table of Contents

- [Architecture & Design Principles](#-architecture--design-principles)
- [How Initialization Works](#-how-initialization-works)
- [Project Structure & System Elements](#-project-structure--system-elements)
- [Content Management & Best Practices](#-content-management--best-practices)
- [Developer Guide: Implementing Localization](#-developer-guide-implementing-localization)
- [Adding a New Language](#-adding-a-new-language)
- [Technical Deep Dive](#-technical-deep-dive)

## 🏗 Architecture & Design Principles

### Core Philosophies

The LumaCore localization engine follows several design principles that ensure system stability and developer productivity:

- **Self-Hosting & Decoupling:** LumaCore uses a dynamic JSON-based structure instead of compiled resource files (`.resx`). This allows real-time adjustments and community-driven translations without rebuilding the system or needing specialized developer environments.
- **Fail-Visible Fallbacks:** LumaCore avoids silent failures or empty strings for missing keys. Instead, it shows explicit fallbacks (`?? key ??`) so missing localization data is immediately visible during development and QA.
- **Interface Interoperability:** The system uses standard .NET interfaces (like `IStringLocalizer`). This ensures seamless integration with third-party Blazor libraries and standard ASP.NET Core features without custom adapters.
- **Immutability by Default:** Once a language file is parsed into the internal data structure, the information is treated as immutable. This enables high-performance, thread-safe access across multiple concurrent requests without complex locking mechanisms.

### System Overview

The localization system consists of three main components:

- **Repository** (`TranslationRepository`): Holds all translation data in memory.
- **Localizer** (`JsonStringLocalizer`): Tracks each user's language preference.
- **Factory** (`JsonStringLocalizerFactory`): Creates localizer instances for Blazor's validation system.

This separation enables efficient memory usage—translations are loaded once and shared, while each user maintains their own language preference.

### Memory Efficiency at Scale

The architecture separates expensive data (translations) from lightweight data (locale preferences).

In a **Blazor Server** scenario with 1,000 simultaneous users all using English, there is only **one** shared instance holding English translations in memory. Each user has a lightweight component that stores just their locale preference (e.g., `"en"`). If users switch languages, the new translation file loads **once**, and all users in that locale share the same data.

In **Blazor WASM**, each browser tab runs as an isolated application with its own instance. While they don't share memory between tabs, the pattern remains identical—translations are loaded once per locale per tab and reused throughout that tab's lifetime.

## 🚀 How Initialization Works

Initialization in `MainLayout.razor` follows a precise sequence for a smooth user experience without visual inconsistencies:

1. **Initial Render (Guard State)**: Before any data is fetched, `Localizer.IsInitialized()` returns `false`. During this brief period, a hardcoded "Loading..." message or a simple UI shell is displayed. This prevents the "Flash of Unlocalized Content" (FOUT), where users might otherwise see raw technical translation keys like `common.buttons.save` before the system is ready.
2. **Preference Lookup & Validation**: `InitializeAsync()` determines the user's language. It first retrieves the saved locale code from the browser's `localStorage`, then validates it against `manifest.json`. If the stored preference is missing or invalid (for instance, if a previously supported language was removed), it defaults to English.
3. **Targeted Data Fetching**: Once the locale is verified, the repository checks its cache. If the required translation set isn't already in memory, the `JsonStringLocalizer` delegates to the repository to fetch the specific JSON file from the server's `locales/` directory. This on-demand loading ensures the application only downloads the language currently in use, rather than all available translations at once.
4. **Ready State & UI Sync**: After the JSON data is loaded and parsed into the internal dictionary tree, `IsInitialized` becomes `true`. This triggers a re-render of `MainLayout`. Because the localizer is injected as a cascading dependency or used globally via `_Imports.razor`, this re-render propagates through the entire component tree, updating every UI element in a single synchronized step.

**Note:** This localization setup is separate from the initial WASM binary download. While the .NET runtime is loading (typically 3–5 seconds), the user sees the static branded splash screen defined in `index.html`. The localization sequence described here begins after the app code starts executing.

## 📂 Project Structure & System Elements

For clean separation of concerns, the localization system is partitioned into distinct layers: services, data models, and UI components. This modular approach keeps data retrieval and caching logic isolated from the presentation layer, so you can modify storage or lookup algorithms without impacting the UI or validation logic.

### Directory Layout

```
src/LumaCore.Ui.Web/
├── Components/ (UI Components & Validator)
├── Models/ (Data Structures)
├── Services/ (Logic & Infrastructure)
└── wwwroot/
    └── locales/
        ├── manifest.json           # Available languages definition
        ├── en/                     # English locale folder
        │   ├── translations.json   # English translation strings
        │   └── flag.svg            # English flag icon
        └── de/                     # German locale folder
            ├── translations.json   # German translation strings
            └── flag.svg            # German flag icon
```

### Core Services (DI Registration)

| **Service**                   | **Lifetime** | **Responsibility**                                         | **Implementation File**         |
| ----------------------------- | ------------ | ---------------------------------------------------------- | ------------------------------- |
| **`TranslationRepository`**   | Singleton    | Fetches and caches JSON files; ensures thread-safe access. | `TranslationRepository.cs`      |
| **`JsonStringLocalizer`**     | Scoped       | Primary UI interface; manages user locale and persistence. | `JsonStringLocalizer.cs`        |
| **`IStringLocalizerFactory`** | Singleton    | Interface bridge for .NET's built-in localization system.  | `JsonStringLocalizerFactory.cs` |

For details on why these lifetimes were chosen, see [Technical Deep Dive](#-technical-deep-dive).

### Data Models & Structures

| **Element**            | **Responsibility**                                           | **File**              |
| ---------------------- | ------------------------------------------------------------ | --------------------- |
| **`TranslationTable`** | Holds the internal dictionary tree for recursive lookups. | `TranslationTable.cs` |
| **`LocaleInfo`**       | Immutable record containing metadata for available languages (code, native name). | `LocaleInfo.cs`       |
| **`Translation`**      | High-performance, stack-allocated struct carrying the lookup result and location. | `Translation.cs`      |

### UI Components

| **Component**            | **Responsibility**                                    | **File**                               |
| ------------------------ | ----------------------------------------------------- | -------------------------------------- |
| **`LocalizedDataAnnotationsValidator`** | Specialized validator for localizing DataAnnotations. | `LocalizedDataAnnotationsValidator.cs` |
| **`LanguageSwitcher`**   | Compact navbar dropdown for quick selection.          | `LanguageSwitcher.razor`               |
| **`LanguageSelector`**   | Detailed settings interface for selection.            | `LanguageSelector.razor`               |

## 📂 Content Management & Best Practices

### The Manifest File

The `manifest.json` file defines which languages are available in the application. It's loaded during initialization to populate the language switcher and validate stored preferences.

```json
{
  "locales": [
    { "code": "en", "nativeName": "English" },
    { "code": "de", "nativeName": "Deutsch" }
  ]
}
```

**Why `nativeName`?** Always use the language's native script (e.g., "日本語"). This respects the user's identity and ensures they can identify their language easily.

### Example Translation File

Below is a comprehensive example of a `translations.json` following the LumaCore structure:

```
{
  "app": {
    "name": "LumaCore",
    "tagline": "Self-hosted. Persona-focused. Yours.",
    "subtitle": "..."
  },
  "common": {
    "buttons": {
      "save": "Save",
      "cancel": "Cancel",
      "delete": "Delete",
      "login": "Sign In"
    },
    "messages": {
      "loading": "Loading…",
      "error": "An error occurred",
      "success": "Changes saved successfully",
      "welcome": "Welcome back, {0}!"
    }
  },
  "components": {
    "login": {
      "_comment": "Translations for the login card and validation messages",
      "title": "Welcome to LumaCore",
      "subtitle": "Sign in to continue",
      "fields": {
        "username": "Username",
        "password": "Password"
      },
      "validation": {
        "usernameRequired": "Username is required.",
        "usernameTooLong": "Username cannot exceed 50 characters.",
        "passwordRequired": "Password is required.",
        "passwordTooShort": "Password must be at least 8 characters."
      }
    }
  }
}
```

**Why nested instead of flat?**

Many i18n systems use flat structures where all keys exist at the same level:

```json
{
  "components_login_title": "Welcome",
  "components_login_subtitle": "Sign in",
  "components_login_validation_usernameRequired": "Username required"
}
```

This structure is hard to scan because all keys sit at the same level, making it difficult to find related translations. You also risk namespace collisions if you're not careful with prefixes.

Nested structure groups related translations together. Need to update all login-related text? Navigate to `components.login` and you see everything in one place. Modern editors can also collapse nested sections, making it easy to navigate large translation files by folding sections you're not currently working on.

### Key Naming Conventions

Use dot notation to navigate the nested structure:

- **`common.*`**: Shared UI elements (e.g., `common.buttons.save`).
- **`components.{name}.*`**: Translations specific to a single component (e.g., `components.login.title`).
- **`validation.*` / `errors.*`**: Global validation and error messages.

**Guideline:** Always prefer descriptive keys. Instead of a generic `validation.required`, use `components.login.validation.usernameRequired`.

### Providing Context via Comments

Since JSON lacks native comments, use the `_comment` key. This is vital for translators to understand **UI constraints** and the meaning of **placeholders** like `{0}`.

## 🛠 Developer Guide: Implementing Localization

### Quick Start

To display a localized title in your `MyWidget.razor` component:

1. Add your key to `wwwroot/locales/en/translations.json` (and other locales):
   ```json
   {
     ...
     "components": {
       ...
       "myWidget": {
         "title": "My Widget Title"
       },
       ...
     },
     ...
   }
   ```
2. Use the localizer in your Razor component:
   ```razor
   <h1>@Localizer["components.myWidget.title"]</h1>
   ```

That's it! The localizer is globally available—no setup required.

### Global Availability (`_Imports.razor`)

In LumaCore, the localization infrastructure is already globally configured. You don't need to add these lines yourself, but it's important to know they exist in `_Imports.razor`. This ensures every component has access to the localizer and its related models without additional setup:

```razor
@using LumaCore.Ui.Web.Services
@using LumaCore.Ui.Web.Models
@using LumaCore.Ui.Web.Components
@using Microsoft.Extensions.Localization

@* Global localization service - use @Localizer["key"] in any component *@
@inject JsonStringLocalizer Localizer
```

### Using Translations in Razor Components

Since the localizer is injected globally via `_Imports.razor`, you can use it immediately in any `.razor` file:

```razor
<h1>@Localizer["components.login.title"]</h1>
```

The localizer returns a `LocalizedString` object, which implicitly converts to a string but also provides metadata like `ResourceNotFound` and `SearchedLocation`. LumaCore uses an **explicit fallback** mechanism: missing keys render as `?? key.name ??`, making them immediately obvious during development and testing.

### Using Placeholders

For dynamic content requiring variable injection, LumaCore uses standard C# string formatting. The `LocalizedString` returned by the localizer can be used directly as the format string:

```razor
<p>@string.Format(Localizer["common.messages.welcome"], mUserName)</p>
```

- **Logic & Translation Flexibility**: The localizer fetches the JSON template containing placeholders (e.g., `"Welcome back, {0}!"`). This approach is better than manual string concatenation because translators can move placeholders within the sentence to match the target language's grammar. It also supports multiple parameters and standard .NET format specifiers for complex data types like currency or dates.
- **Security & XSS Mitigation**: Blazor's rendering engine HTML-escapes the output of `@` expressions by default. This ensures user-provided variables injected via `string.Format` are neutralized before reaching the browser, protecting against Cross-Site Scripting (XSS). This is the recommended pattern for dynamic localized content.

### Localized Form Validation

LumaCore provides `LocalizedDataAnnotationsValidator` for seamless integration into standard Blazor forms. By dropping this component into your `EditForm`, you bridge traditional .NET validation attributes with the dynamic JSON-based architecture. This lets you use localization keys directly in your Data Models, transforming static validation strings into reactive identifiers that draw from the global thread-safe repository.

#### 1. Define the Model

Assign translation keys directly to the `ErrorMessage` property of your Data Annotation attributes. The LumaCore system intercepts these keys during validation. Using the registered `IStringLocalizerFactory`, it resolves these identifiers into localized strings from your JSON files. This works with all standard .NET validation attributes—`[Required]`, `[StringLength]`, `[Range]`, `[RegularExpression]`—letting you maintain a clean data model while keeping error messages consistent with the active language.

```csharp
public class LoginModel
{
    [Required(ErrorMessage = "components.login.validation.usernameRequired")]
    [StringLength(50, ErrorMessage = "components.login.validation.usernameTooLong")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "components.login.validation.passwordRequired")]
    [MinLength(8, ErrorMessage = "components.login.validation.passwordTooShort")]
    public string Password { get; set; } = string.Empty;
}
```

#### 2. Implement the Form

Integrate `LocalizedDataAnnotationsValidator` within your `EditForm`. It intercepts translation keys from your model during validation and resolves them against the active JSON dictionary:

```razor
<div class="login-card">
    <h1>@Localizer["components.login.title"]</h1>
    <p>@Localizer["components.login.subtitle"]</p>
    
    <EditForm Model="@mLoginModel" OnValidSubmit="@HandleLogin">
        <LocalizedDataAnnotationsValidator />
        
        <label>
            @Localizer["components.login.fields.username"]
            <InputText @bind-Value="mLoginModel.Username" />
            <ValidationMessage For="@(() => mLoginModel.Username)" />
        </label>
        
        <label>
            @Localizer["components.login.fields.password"]
            <InputText type="password" @bind-Value="mLoginModel.Password" />
            <ValidationMessage For="@(() => mLoginModel.Password)" />
        </label>
        
        <button type="submit">
            @Localizer["common.buttons.login"]
        </button>
    </EditForm>
</div>
```

### Language Switching Components

LumaCore provides two UI components for language transitions. Both use `SetLocaleAsync(code)`, which is the main method for updating the application's language. When triggered, this method persists the user's choice in `localStorage` to remember it across sessions, then triggers a `location.reload()`. This full-page refresh ensures the entire Blazor component tree is re-initialized, so every localized string—including those in static layouts and headers—updates correctly.

- **`LanguageSwitcher.razor`**: A compact dropdown menu for the navigation bar.
- **`LanguageSelector.razor`**: A detailed, card-based interface for the Settings panel.

**Why reload the page?**

When the language changes, every component needs to re-render with new translations. Reloading guarantees a clean slate — all components are recreated, all state is fresh, and there's no risk of stale translations lingering in component state.

The alternative (broadcasting a language-changed event to all components) is complex and error-prone. Components would need to subscribe to the event, update their local state, and call `StateHasChanged()`. You'd inevitably have components that forget to subscribe or handle the event incorrectly. Reloading is simpler and more reliable.

## 🌐 Adding a New Language

Adding a new language to LumaCore requires three steps: creating the translation file, adding a flag icon, and registering the locale in the manifest.

### Step 1: Create the Locale Folder

Create a new folder under `wwwroot/locales/` using the standard ISO 639-1 language code:

```
wwwroot/locales/
├── en/
├── de/
└── fr/    ← New folder for French
```

### Step 2: Create the Translation File

Copy an existing `translations.json` as your starting point. The English file serves as the canonical reference—it contains all keys the application expects:

```bash
cp wwwroot/locales/en/translations.json wwwroot/locales/fr/translations.json
```

Then translate the values while keeping all keys identical. The structure must match exactly:

```json
{
  "_comment": "LumaCore UI Translations - French",
  "_version": "1.0.0",
  
  "app": {
    "name": "LumaCore",
    "tagline": "Self-hosted. Persona-focused. Yours."
  },
  "common": {
    "buttons": {
      "close": "Fermer",
      "save": "Enregistrer"
    }
  }
}
```

**Important:** Never remove or rename keys. If a key exists in English, it must exist in every language file. Missing keys will display as `?? key.path ??` in the UI.

### Step 3: Add the Flag Icon

Place a `flag.svg` file in your locale folder. The flag appears in the `LanguageSwitcher` dropdown and `LanguageSelector` panel:

```
wwwroot/locales/fr/
├── translations.json
└── flag.svg
```

Use a clean, simple SVG. Recommended sources for public domain flag icons include [Flagpedia](https://flagpedia.net) or [Flag Icons](https://flagicons.lipis.dev/).

### Step 4: Register in the Manifest

Add your new locale to `manifest.json`. The `code` must match your folder name exactly, and `nativeName` should be the language's name in its own script:

```json
{
  "locales": [
    { "code": "en", "nativeName": "English" },
    { "code": "de", "nativeName": "Deutsch" },
    { "code": "fr", "nativeName": "Français" }
  ]
}
```

**Why `nativeName`?** Users identify languages by their native names. A Japanese speaker looks for "日本語", not "Japanese". This respects the user's identity and ensures they can find their language in a list.

### Verification Checklist

After adding a new language:

- [ ] Folder exists: `wwwroot/locales/{code}/`
- [ ] Translation file exists: `translations.json`
- [ ] Flag icon exists: `flag.svg`
- [ ] Manifest entry added with correct `code` and `nativeName`
- [ ] All keys from `en/translations.json` are present
- [ ] Application builds without errors
- [ ] Language appears in `LanguageSwitcher` dropdown
- [ ] Selecting the language loads correct translations

## 🔧 Technical Deep Dive

This section covers implementation details for developers who want to understand how the localization system works internally, or who need to extend or debug it.

### Service Registration (`Program.cs`)

The core infrastructure is configured in `Program.cs`. This centralized registration ensures that standard .NET localization interfaces—which normally rely on static resources—are swapped out for LumaCore's high-performance, JSON-based logic.

```csharp
builder.Services.AddLocalization();
builder.Services.AddSingleton<TranslationRepository>();
builder.Services.AddScoped<JsonStringLocalizer>();
builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
```

The `AddLocalization()` call registers .NET's built-in localization infrastructure, which our custom services build upon. The key element is `IStringLocalizerFactory`. When third-party components inject `IStringLocalizer<T>` (the generic variant that .NET uses for component-scoped translations), the DI container uses our `JsonStringLocalizerFactory` to provide them with our JSON-based localizer automatically. This ensures third-party Blazor libraries that support localization work seamlessly with our system—no additional configuration or changes to existing coding patterns required.

The `TranslationRepository` is registered as a Singleton because translations are the same for all users—only the chosen language differs. This means translations are loaded on-demand and then shared across all circuits in Blazor Server. When the first user requests German, it's loaded once and cached; subsequent German users reuse that cached data, dramatically reducing memory usage. In Blazor WASM, each browser tab gets its own Singleton instance, but the pattern remains the same.

The `JsonStringLocalizer` is Scoped because each user needs their own instance to track their language preference independently. In Blazor Server, each user's circuit gets its own Scoped instance. In WASM, there's only one user anyway, so the distinction doesn't matter.

The factory registration serves a specific purpose: third-party components that inject `IStringLocalizer` from DI need it. The factory doesn't create new localizer instances itself—it delegates to the DI container. This guarantees that everyone gets the same `JsonStringLocalizer` instance, whether they inject it directly or request `IStringLocalizer<T>` through the standard .NET interface.

### Global Initialization (`MainLayout.razor`)

Since localization data is loaded asynchronously (via HTTP in both Blazor WASM and Server for consistency), LumaCore initiates initialization in the root layout for a uniform user experience. By centralizing loading logic in `MainLayout.razor`, no child component begins rendering with unresolved translation keys. The root layout acts as a synchronization barrier that only releases the application body once all localized resources are cached and ready.

```razor
@if (!Localizer.IsInitialized())
{
    <!-- Show a simple loader while JSONs are being fetched -->
    <div class="app-loading-screen">
        <p>Loading Translations...</p>
    </div>
}
else
{
    @Body
}

@code {
    protected override async Task OnInitializedAsync()
    {
        // This checks localStorage, validates the manifest, 
        // and fetches the translation JSON if needed.
        await Localizer.InitializeAsync();
    }
}
```

**Why the conditional rendering?**

Without it, child components render immediately — before `InitializeAsync()` completes. They'd access translations and see `?? some.key ??` placeholders flash on screen, then disappear once translations load. The conditional prevents this by showing a loading screen until translations are ready.

**Why hardcoded "Loading…"?** Before initialization completes, the localizer isn't ready yet. Using a hardcoded string ensures the loading screen always displays correctly, regardless of locale or initialization state.

**Note:** This loading screen is typically only visible for a fraction of a second (0.1-0.5s) while translations load from JSON. For the main WASM loading experience (3-5 seconds), see the branded splash screen in `index.html`.

### Recursive Lookup Implementation

The system parses localization keys by splitting at the dot (`.`) delimiter and traversing the `TranslationTable`—a nested dictionary structure—recursively. This provides efficient hash-map lookups at each nesting level while preserving the logical hierarchy found in JSON source files. If the traversal reaches a leaf node that isn't a string, or if an intermediate key is missing, the system aborts the search and triggers the "fail-visible" fallback. This lets developers organize translation files logically by feature or component without a performance penalty during key resolution.

### Thread-Safe Implementation

As a shared Singleton, the `TranslationRepository` must handle high concurrency. It uses a synchronization pattern that prioritizes non-blocking read access:

- **Lock-free Reads**: Using `Volatile.Read` on immutable snapshots, UI threads never experience contention. Readers always operate on a consistent, static view of the data, letting hundreds of concurrent components resolve keys without waiting for background updates.
- **Per-Locale Semaphores (Concurrency Guarding)**: To prevent a "thundering herd" effect—where multiple users requesting a new language simultaneously trigger identical HTTP requests—the system uses a `SemaphoreSlim` per locale. Only the first request performs the I/O operation; subsequent requests for the same language wait and then consume the result from the shared cache.
- **Atomic Publication (Snapshot Swap)**: Updates to the global translation state are performed through an atomic reference swap using `Volatile.Write`. This guarantees instantaneous, indivisible transitions; at no point will a component access a partially loaded or corrupted dictionary.

### Security and XSS Protection

Since the localization system handles strings displayed throughout the entire application, security is a primary concern:

- **Automatic Escaping**: Blazor automatically HTML-escapes all strings rendered via `@`. Even if a translation file were compromised to include malicious `<script>` tags, the browser would render them as harmless plain text.
- **The Danger of MarkupString**: Avoid using `MarkupString` to render translations with placeholders. If a translation includes a variable (e.g., a username), injecting it via raw HTML could enable Cross-Site Scripting (XSS). For complex scenarios requiring formatted text (like bolding or links), split the translation into multiple keys or use nested Blazor components rather than raw HTML injection.

© 2025 LumaCoreTech • MIT License
