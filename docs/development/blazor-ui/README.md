# Blazor UI Development

Documentation for LumaCore's Blazor WebAssembly frontend.

## Guides

### [Blazor Guide](blazor-guide.md)
Core Blazor concepts and patterns used in LumaCore:
- Component lifecycle and rendering
- Event handling and state management
- JavaScript interop
- Error handling and performance

### [Forms and Validation Guide](forms-guide.md)
Building and validating forms in Blazor:
- EditForm and EditContext fundamentals
- Localized validation (custom attributes vs. manual validation)
- Form submission events and server-side validation
- Common pitfalls and validation styling

### [Localization Guide](localization-guide.md)
Multi-language support with LumaCore's JSON-based localization system:
- File structure and setup
- Using translations in components
- Language switching
- Localized validation messages
- Thread-safety considerations

### [Theming Guide](theming-guide.md)
Dynamic theming system with CSS Custom Properties:
- Theme structure and registration
- Creating custom themes
- Runtime theme switching
- Color system and design tokens

### [Auth Integration](auth-integration.md)
Cookie-based authentication flow between Blazor WASM and the LumaCore API:
- Client-side auth services (`AuthService`, `CookieCredentialHandler`, `CookieAuthenticationStateProvider`)
- Authentication flow and DI registration
- Design decisions (no token storage, server-first logout, cross-origin support)

### [Cheatsheet](cheatsheet.md)
Quick reference for common Blazor patterns and LumaCore conventions.

## Getting Started

New to Blazor development in LumaCore? Start with the [Blazor Guide](blazor-guide.md) to understand the fundamentals, then explore the other guides as needed.
