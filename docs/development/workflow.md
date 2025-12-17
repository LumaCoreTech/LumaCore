# Developer Workflow

This document describes the complete development workflow for LumaCore, from setting up your environment to contributing changes.

---

## Quickstart (5 Minutes)

Get up and running quickly:

```bash
# Clone with submodules
git clone --recursive https://github.com/LumaCoreTech/LumaCore.git
cd LumaCore

# Restore and build
dotnet restore
dotnet build

# Run locally
cd src/LumaCore.Api
dotnet run
```

**Verify everything works:**
- Health check: `http://localhost:5080/health` (should return 200 OK)
- *Swagger UI* (dev): `http://localhost:5080/swagger`

---

## Getting Started

### Initial Setup

Before you can start developing, you need to set up your local environment. Clone the repository and ensure you have the required tools installed:

**Prerequisites:**

- **.NET 10 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/10.0)
  
- ***PowerShell Core* 7+** - [Download here](https://github.com/PowerShell/PowerShell/releases)
  - Cross-platform version of PowerShell (works on Windows, macOS, Linux)
  
- ***Node.js* and npm** - [Download here](https://nodejs.org/)
  - Required for API documentation generation
  - The documentation tools (*widdershins*) are installed automatically when needed, but npm itself must be available
  - ***Visual Studio* users:** You can install *Node.js* as an optional component during *Visual Studio* installation (Workloads → "Node.js development"). This way *Visual Studio* handles updates automatically.
  
- **A code editor:**
  - [*Visual Studio* 2026](https://visualstudio.microsoft.com/) (Community, Professional, or Enterprise)
    - **Community Edition is free** for non-commercial use, open source projects, academic research, and small teams
  - [*JetBrains Rider*](https://www.jetbrains.com/rider/)
  - [*Visual Studio Code*](https://code.visualstudio.com/) with C# extension

Clone the repository and restore dependencies:

```bash
# Clone with submodules (recommended)
git clone --recursive https://github.com/LumaCoreTech/LumaCore.git
cd LumaCore
dotnet restore
```

**Note:** The `--recursive` flag is important because LumaCore uses the `build.net` submodule for build scripts. If you already cloned without `--recursive`, initialize the submodule manually:

```bash
git submodule update --init --recursive
```

Build the solution to verify everything works:

```bash
dotnet build
```

If the build succeeds, you're ready to start developing.

### Running Locally

To run LumaCore locally, start the API project:

```bash
cd src/LumaCore.Api
dotnet run
```

The API will start on `http://localhost:5080` (or the port configured in your launch settings). You can test it by navigating to `http://localhost:5080/health` which should return a 200 OK status.

**Interactive API Documentation:** In development mode, *Swagger UI* is available at `http://localhost:5080/swagger` for exploring and testing the API interactively.

For debugging in your IDE, open the solution file and use the built-in debugger. Set breakpoints as needed and use the standard debugging features.

---

## Development Cycle

### Creating a Feature Branch

LumaCore follows a feature branch workflow. Never commit directly to `main`. Instead, create a branch for your changes:

```bash
# Create and switch to a new branch
git checkout -b feature/add-persona-endpoints

# Or for bug fixes
git checkout -b fix/memory-leak-in-generation
```

Use descriptive branch names that indicate what you're working on. Common prefixes include `feature/`, `fix/`, `docs/`, and `refactor/`.

### Making Changes

Work in the feature-based structure under `src/LumaCore.Api/Features/` for API endpoints, or in `src/LumaCore.Core/` for shared library code. LumaCore uses a vertical slice architecture where each feature is self-contained.

As you work, run the application locally to test your changes. Make incremental commits with clear messages following conventional commit format:

```bash
git add .
git commit -m "feat: Add endpoint for listing personas"
```

**Commit message format:**
- `feat:` New features
- `fix:` Bug fixes
- `docs:` Documentation changes
- `refactor:` Code refactoring
- `test:` Adding or updating tests
- `chore:` Maintenance tasks

These messages become part of automatically generated release notes, so write them clearly.

### Writing Tests

Add tests for your changes using *xUnit*. For test structure, naming conventions, and best practices, see [Coding Standards - Testing Conventions](coding-standards.md#testing-conventions).

Test projects live in `src/` alongside the production code:
- `src/LumaCore.Core.Tests`
- `src/LumaCore.Api.Tests`

**Running tests:**

```bash
# All tests
dotnet test

# Specific project
dotnet test src/LumaCore.Core.Tests
```

**Test coverage:** Use your IDE's built-in tools:
- *Visual Studio:* Code Coverage (Test → Analyze Code Coverage)
- *Rider:* *dotCover* (Run → Cover Unit Tests)
- *VS Code:* *Coverage Gutters* extension with coverlet

Write tests that cover happy path scenarios, edge cases, and error conditions. Good test coverage gives confidence that your changes work correctly and don't break existing functionality.

### Local Testing

Before pushing changes, test them thoroughly to catch issues early.

**Compile check:**
```bash
dotnet build
```
Verifies that your code compiles without errors. This catches syntax errors, missing references, and type mismatches before you push. If the build fails, fix the errors before continuing.

**Run tests:**
```bash
# All tests
dotnet test

# Only unit tests (fast, no external dependencies)
dotnet test --filter "Category=Unit"

# Only integration tests (slower, may require database/services)
dotnet test --filter "Category=Integration"
```
Ensures your changes don't break existing functionality. Unit tests run quickly and test individual components in isolation. Integration tests verify that components work together correctly but take longer to run.

**Note:** Test filtering by category only works if your test projects use the `[Trait("Category", "Unit")]` or `[Trait("Category", "Integration")]` attributes. If these aren't configured, the filter won't match any tests.

**Format check:**
```bash
dotnet format --verify-no-changes
```
Checks if your code follows the project's formatting standards (defined in `.editorconfig`). This ensures consistent code style across the team. If this fails, run `dotnet format` to automatically fix formatting issues.

**If everything passes locally, you're ready to push.**

---

## Special Considerations

### API Documentation

If your changes modify the API surface (add endpoints, change request/response schemas, modify routes), you must regenerate the API documentation. This ensures that for every commit verified by CI, the documentation matches the code exactly.

Run the pre-commit script to regenerate documentation:

```bash
# Cross-platform invocation (recommended)
pwsh ./Pre-Commit-Actions.ps1 -SkipTests

# Windows PowerShell alternative
.\Pre-Commit-Actions.ps1 -SkipTests
```

This script rebuilds the solution with OpenAPI generation enabled, transforms the output to GitHub-friendly Markdown, and shows you what changed. Review the documentation in `docs/api/README.md` to verify it looks correct.

The documentation must be committed alongside your code changes. CI will verify that committed documentation matches the current API, and the build will fail if they're out of sync.

### Database Migrations

If you modify database schemas (which would be in `src/LumaCore.Core/` or feature-specific areas), document the migration process. LumaCore uses EF Core Migrations, so follow those conventions.

### Configuration Changes

If you add new configuration options, document them with inline comments in the configuration files (`appsettings.json`, `appsettings.Development.json`, etc.) in `src/LumaCore.Api/`. Configuration files should never contain secrets - use environment variables or user secrets for sensitive values.

For more details on configuration, see the [Configuration Guide](../deployment/configuration.md).

---

## Creating a Pull Request

Once your changes are complete and tested locally, push your branch and create a pull request on GitHub:

```bash
git push origin feature/add-persona-endpoints
```

In your pull request description:
- Explain what you changed and why
- Link to relevant issues (`Fixes #123`)
- Describe how to test the changes
- Note any breaking changes or special considerations
- Include screenshots if you changed UI or added visual features

**What happens next:**

*GitHub Actions* automatically runs CI workflows:
- **linux-build.yml** - Builds and tests on Linux
- **windows-build.yml** - Builds and tests on Windows

Both workflows verify:
- Code compiles
- All tests pass
- API documentation is up-to-date (if applicable)
- Code formatting is correct

A maintainer will review your pull request. They may ask questions, request changes, or approve and merge. If there's feedback, you can address it by making additional commits to the same branch - they'll automatically appear in the pull request.

Once approved and CI passes, the maintainer will merge your changes into `main`.

---

## Before Every Commit

Develop good habits to catch issues early:

**Compile:** Make sure your code compiles with `dotnet build`. This catches syntax errors and type issues.

**Test:** Run `dotnet test` to ensure you haven't broken existing functionality.

**Format:** Check code formatting with `dotnet format --verify-no-changes`. If it fails, run `dotnet format` to apply formatting.

**Documentation:** If you touched the API, regenerate documentation with `pwsh ./Pre-Commit-Actions.ps1 -SkipTests`.

**Review:** Use `git status` and `git diff` to review what you're committing. Make sure you're not including unintended files.

The `Pre-Commit-Actions.ps1` script automates most of these checks. Run it with `-SkipTests` during development for faster iteration, or without flags for the full check before pushing:

```bash
# Cross-platform (recommended)
pwsh ./Pre-Commit-Actions.ps1 -SkipTests

# Windows PowerShell
.\Pre-Commit-Actions.ps1 -SkipTests
```

---

## Common Development Tasks

### Updating Dependencies

Check for outdated packages periodically:

```bash
dotnet list package --outdated
```

To update a specific package:

```bash
dotnet add package PackageName
```

After updating, rebuild and run tests to ensure nothing broke. *Dependabot* automatically creates pull requests for dependency updates weekly - review and merge these to keep dependencies current, especially for security updates.

### Clean Builds

If you encounter strange build issues, do a clean rebuild:

```bash
dotnet clean
dotnet restore
dotnet build
```

For a really deep clean that removes all untracked files:

```bash
git clean -fdx -e node_modules/
```

This deletes everything not tracked by git except `node_modules/` (which is slow to reinstall). Use with caution.

### Code Formatting

LumaCore uses `dotnet format` to enforce consistent code formatting across the project. Formatting rules are defined in the `.editorconfig` file at the repository root, which specifies things like indentation, spacing, brace placement, and naming conventions.

**Check if your code is formatted correctly:**

```bash
dotnet format --verify-no-changes
```

This command checks formatting without changing files. If formatting issues are found, it exits with an error and shows which files need formatting. Use this before committing to catch issues early.

**Automatically format your code:**

```bash
dotnet format
```

This applies formatting rules to all files in the solution. Run this if the verify command found issues, or whenever you want to ensure your code follows the project's formatting standards.

**Pro tip:** Configure your IDE to format on save:
- ***Visual Studio:*** Tools → Options → Text Editor → C# → Code Style → Formatting → "Format document on save"
- ***Rider:*** Settings → Editor → Code Style → C# → Enable "Reformat code on save"
- ***VS Code:*** Install *C# Dev Kit*, then enable "Editor: Format On Save" in settings

This way, your code is always formatted correctly without manual commands.

### Debugging

Use your IDE's built-in debugger for the best experience. Set breakpoints, inspect variables, and step through code. *VS Code*, *Rider*, and *Visual Studio* all have excellent debugging support for .NET.

For command-line debugging, use `dotnet run` with the `DOTNET_ENVIRONMENT=Development` environment variable to enable detailed error pages and logging.

---

## Troubleshooting

### Build Failures

If `dotnet build` fails, read the error messages carefully. Common issues:
- Missing dependencies: Run `dotnet restore`
- Syntax errors: Fix the code
- Type errors: Check your using statements and references

### Test Failures

If tests fail:
- Read the test output to understand what broke
- Run the specific failing test in isolation
- Use the debugger to step through the test
- Check if your changes affected the code being tested

### API Documentation Out of Sync

If CI fails with "API documentation is outdated," you made API changes but didn't regenerate documentation. Run `pwsh ./Pre-Commit-Actions.ps1 -SkipTests`, commit the updated documentation, and push again.

### Pre-Commit Script Issues

If the pre-commit script fails:
- Ensure *Node.js* is installed (needed for *widdershins*)
- Update to *PowerShell Core* 7+ and use `pwsh` for cross-platform compatibility
- Check for port conflicts (the script usually handles these automatically)

### PowerShell Script Execution Blocked

If you get an error like "cannot be loaded because running scripts is disabled on this system," you need to adjust your PowerShell execution policy:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

This allows locally created scripts to run while still requiring downloaded scripts to be signed. This is a one-time setup per machine.

### Git Conflicts

If you have merge conflicts:
- Pull the latest changes: `git pull origin main`
- Resolve conflicts in your editor
- Test that everything still works
- Commit the resolution

---

## Additional Resources

**Scripts:**
- `Pre-Commit-Actions.ps1` - Pre-commit automation
- `build.net/OpenApi/generate-api-docs.ps1` - Generate OpenAPI documentation
- `build.net/OpenApi/verify-api-docs.ps1` - Verify documentation (CI)

**Documentation:**
- `.github/workflows/README.md` - CI/CD architecture
- `docs/api/README.md` - Generated API documentation
- `docs/architecture/` - Architecture decisions and patterns
- `docs/features/` - Feature-specific documentation

**External Resources:**
- [*GitHub Flow*](https://guides.github.com/introduction/flow/) - Branching workflow
- [*Semantic Versioning*](https://semver.org/) - Version numbering
- [*Conventional Commits*](https://www.conventionalcommits.org/) - Commit message format

---

**For maintainers:** See [Release Process](release-process.md) for information on creating and publishing releases.

---

© 2025 LumaCoreTech • MIT License
