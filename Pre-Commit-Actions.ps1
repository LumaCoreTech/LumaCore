# Copyright (c) 2025 LumaCoreTech
# SPDX-License-Identifier: MIT
# Project: https://github.com/LumaCoreTech/LumaCore

<#
.SYNOPSIS
    Pre-commit checklist script for LumaCore development.

.DESCRIPTION
    This script runs common pre-commit actions to ensure code quality and
    documentation is up-to-date before committing changes.
    
    Actions performed:
      1. Builds API project (triggers MSBuild OpenAPI generation)
      2. Generates Markdown documentation from OpenAPI spec
      3. Checks for unstaged changes in docs/
      4. Optionally runs tests
      5. Optionally checks code style and analyzers (whitespace is ReSharper's domain)
    
    OpenAPI generation is handled by Microsoft.Extensions.ApiDescription.Server
    during the build process - no server startup required.
    
    Use this script before committing API changes to ensure documentation
    stays in sync with the codebase.

.PARAMETER SkipTests
    Skip running tests (faster for quick commits)

.PARAMETER SkipFormat
    Skip code style and analyzer checks (whitespace/alignment is always skipped - use ReSharper)

.PARAMETER AutoStage
    Automatically stage changed documentation files

.EXAMPLE
    .\Pre-Commit-Actions.ps1
    
    Runs all checks and regenerates documentation.

.EXAMPLE
    .\Pre-Commit-Actions.ps1 -SkipTests
    
    Skips test execution (faster for documentation-only changes).

.EXAMPLE
    .\Pre-Commit-Actions.ps1 -AutoStage
    
    Automatically stages changed documentation files.

.NOTES
    This script is designed to be run manually before committing.
    For automatic execution, consider setting up a Git pre-commit hook.
#>

#Requires -Version 7.0

[CmdletBinding()]
param(
    [switch]$SkipTests,
    [switch]$SkipFormat,
    [switch]$AutoStage
)

$ErrorActionPreference = 'Stop'

# ============================================================================
# Configuration
# ============================================================================

$DocsDir = "docs/api"
$ApiProject = "src/LumaCore.Api/LumaCore.Api.csproj"

# ============================================================================
# Helper Functions
# ============================================================================

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host " $Title" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
}

# ============================================================================
# Pre-flight Checks
# ============================================================================

Write-Section "Pre-Commit Actions for LumaCore"

# Check if we're in a git repository
if (!(Test-Path ".git")) {
    Write-Error "Not in a git repository. Please run this from the repository root."
    exit 1
}

# Check if .NET is installed
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error ".NET SDK not found. Please install .NET 10 SDK."
    exit 1
}

# ============================================================================
# 1. Regenerate API Documentation
# ============================================================================

Write-Section "1. Regenerating API Documentation"

Write-Info "Generating OpenAPI JSON specifications..."

try {
    # Generate versioned OpenAPI JSON files (v1.json, v2.json, etc.)
    # Uses ./build.net/OpenApi/generate-openapi-json.ps1 which handles
    # Microsoft's quirky naming conventions via explicit --document-name
    # and --file-name parameters.
    & "$PSScriptRoot/build.net/OpenApi/generate-openapi-json.ps1"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "OpenAPI JSON generation failed"
        exit 1
    }
    
    Write-Info "✅ OpenAPI specifications generated"
}
catch {
    Write-Error "Failed to generate OpenAPI JSON: $_"
    exit 1
}

Write-Info "Generating Markdown documentation..."

try {
    & "$PSScriptRoot/build.net/OpenApi/generate-api-docs.ps1"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Markdown generation failed"
        exit 1
    }
    
    Write-Info "✅ API documentation generated successfully"
}
catch {
    Write-Error "Failed to generate documentation: $_"
    exit 1
}

# ============================================================================
# 2. Check for Documentation Changes
# ============================================================================

Write-Section "2. Checking for Documentation Changes"

$docChanges = git status --porcelain $DocsDir

if ($docChanges) {
    Write-Warn "⚠️  API documentation has changes:"
    Write-Host ""
    git status --short $DocsDir
    Write-Host ""
    
    if ($AutoStage) {
        Write-Info "Auto-staging documentation changes..."
        git add $DocsDir
        Write-Info "✅ Documentation changes staged"
    }
    else {
        Write-Info "To stage these changes, run:"
        Write-Host "  git add $DocsDir" -ForegroundColor Yellow
        Write-Host ""
    }
}
else {
    Write-Info "✅ No documentation changes detected"
}

# ============================================================================
# 3. Run Tests (Optional)
# ============================================================================

if (!$SkipTests) {
    Write-Section "3. Running Tests"
    
    Write-Info "Running test suite..."
    
    # Check if there are any test projects
    $testProjects = Get-ChildItem -Path "src" -Filter "*.Tests.csproj" -Recurse
    
    if ($testProjects.Count -eq 0) {
        Write-Warn "No test projects found - skipping tests"
    }
    else {
        try {
            dotnet test --configuration Release --no-build --verbosity minimal
            
            if ($LASTEXITCODE -ne 0) {
                Write-Error "Tests failed!"
                exit 1
            }
            
            Write-Info "✅ All tests passed"
        }
        catch {
            Write-Error "Failed to run tests: $_"
            exit 1
        }
    }
}
else {
    Write-Info "Skipping tests (use without -SkipTests to run)"
}

# ============================================================================
# 4. Code Formatting (Optional)
# ============================================================================

if (!$SkipFormat) {
    Write-Section "4. Checking Code Formatting"
    
    Write-Info "Checking code formatting..."
    
    # NOTE: We check 'style' and 'analyzers' but NOT 'whitespace'.
    # Whitespace formatting (including alignment) is handled by ReSharper,
    # and dotnet format has different opinions about column alignment.
    # This avoids false positives for ReSharper's aligned parameter lists.
    
    $hasIssues = $false
    
    try {
        # Check style rules (using directives, var preferences, etc.)
        Write-Info "Checking style rules..."
        dotnet format style --verify-no-changes --verbosity quiet 2>&1 | Out-Null
        
        if ($LASTEXITCODE -ne 0) {
            $hasIssues = $true
            Write-Warn "⚠️  Style issues detected"
        }
        
        # Check analyzer rules (code quality, nullability, etc.)
        Write-Info "Checking analyzer rules..."
        dotnet format analyzers --verify-no-changes --verbosity quiet 2>&1 | Out-Null
        
        if ($LASTEXITCODE -ne 0) {
            $hasIssues = $true
            Write-Warn "⚠️  Analyzer issues detected"
        }
        
        if ($hasIssues) {
            Write-Info "To auto-fix issues, run:"
            Write-Host "  dotnet format style" -ForegroundColor Yellow
            Write-Host "  dotnet format analyzers" -ForegroundColor Yellow
            Write-Host ""
            Write-Info "Note: Whitespace/alignment is managed by ReSharper, not dotnet format."
        }
        else {
            Write-Info "✅ Code formatting is correct"
        }
    }
    catch {
        Write-Warn "dotnet-format not available - skipping format check"
        Write-Info "To install: dotnet tool install -g dotnet-format"
    }
}
else {
    Write-Info "Skipping code formatting check (use without -SkipFormat to check)"
}

# ============================================================================
# Summary
# ============================================================================

Write-Section "Pre-Commit Summary"

Write-Host ""
Write-Info "✅ Pre-commit actions completed successfully!"
Write-Host ""

# Show what needs to be staged
$allChanges = git status --porcelain

if ($allChanges) {
    Write-Info "Modified files:"
    git status --short
    Write-Host ""
    
    if (!$AutoStage) {
        Write-Info "Next steps:"
        Write-Host "  1. Review changes:  git diff" -ForegroundColor Cyan
        Write-Host "  2. Stage changes:   git add <files>" -ForegroundColor Cyan
        Write-Host "  3. Commit:          git commit -m 'Your message'" -ForegroundColor Cyan
        Write-Host ""
    }
}
else {
    Write-Info "No changes detected - you're ready to commit!"
}

# ============================================================================
# Optional: Git Hook Setup Instructions
# ============================================================================

if (!(Test-Path ".git/hooks/pre-commit")) {
    Write-Host ""
    Write-Info "💡 Tip: You can automate this by setting up a Git pre-commit hook:"
    Write-Host ""
    Write-Host "  Create .git/hooks/pre-commit with:" -ForegroundColor Yellow
    Write-Host '  #!/usr/bin/env pwsh' -ForegroundColor Gray
    Write-Host '  .\Pre-Commit-Actions.ps1 -SkipTests' -ForegroundColor Gray
    Write-Host ""
}
