# Copyright (c) 2025 LumaCoreTech
# SPDX-License-Identifier: MIT
# Project: https://github.com/LumaCoreTech/LumaCore

<#
.SYNOPSIS
    Packs the repository into a ZIP file, excluding build artifacts and Git metadata.

.DESCRIPTION
    Creates a clean ZIP archive of the repository for sharing or backup purposes.
    
    Excludes by default:
      - /.git/                           (Git metadata)
      - /build.net/.git/                 (Submodule Git metadata)
      - /artifacts/                      (Build output)
      - /build.net/BuildTools/artifacts/ (Tool build output)
      - /bin/ and /obj/ folders          (Project build output)
      - /.vs/                            (Visual Studio cache)
      - /*.zip                           (ZIP files in root only)

.PARAMETER OutputPath
    Path for the output ZIP file. Defaults to 'LumaCore-<timestamp>.zip' in the current directory.

.PARAMETER RepoRoot
    Path to the repository root. Defaults to the script's parent directory.

.EXAMPLE
    .\Pack-Repository.ps1
    
    Creates LumaCore-20251219-143052.zip in the current directory.

.EXAMPLE
    .\Pack-Repository.ps1 -OutputPath "C:\Temp\LumaCore.zip"
    
    Creates the ZIP at the specified path.
#>

#Requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(HelpMessage = "Path for the output ZIP file")]
    [string]$OutputPath,

    [Parameter(HelpMessage = "Path to the repository root")]
    [string]$RepoRoot = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

# ============================================================================
# Configuration
# ============================================================================

# Default output filename with timestamp
if ([string]::IsNullOrEmpty($OutputPath)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputPath = Join-Path (Get-Location) "LumaCore-$timestamp.zip"
}

# Patterns to exclude (relative to repo root)
$ExcludePatterns = @(
    '.git'
    'build.net/.git'
    'build.net\.git'
    'artifacts'
    'build.net/BuildTools/artifacts'
    'build.net\BuildTools\artifacts'
    'bin'
    'obj'
    '.vs'
    '*.user'
    '*.suo'
)

# Files to exclude only in root (not in subdirectories)
$RootExcludePatterns = @(
    '*.zip'
)

# ============================================================================
# Helper Functions
# ============================================================================

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Green
}

function Write-Err {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Test-ShouldExclude {
    param(
        [string]$RelativePath
    )
    
    # Normalize path separators
    $normalizedPath = $RelativePath -replace '\\', '/'
    
    # Check root-only patterns (files directly in repo root)
    if (!$normalizedPath.Contains('/')) {
        foreach ($pattern in $RootExcludePatterns) {
            if ($normalizedPath -like $pattern) {
                return $true
            }
        }
    }
    
    foreach ($pattern in $ExcludePatterns) {
        $normalizedPattern = $pattern -replace '\\', '/'
        
        # Check if path matches pattern (exact match or starts with pattern/)
        if ($normalizedPath -eq $normalizedPattern) {
            return $true
        }
        if ($normalizedPath -like "$normalizedPattern/*") {
            return $true
        }
        if ($normalizedPath -like "*/$normalizedPattern/*") {
            return $true
        }
        if ($normalizedPath -like "*/$normalizedPattern") {
            return $true
        }
        # Handle wildcards like *.user
        if ($pattern.Contains('*') -and $normalizedPath -like $normalizedPattern) {
            return $true
        }
    }
    
    return $false
}

# ============================================================================
# Main
# ============================================================================

Write-Info "Packing repository..."
Write-Host "  Source: $RepoRoot"
Write-Host "  Output: $OutputPath"
Write-Host ""

# Verify repo root exists
if (!(Test-Path $RepoRoot)) {
    Write-Err "Repository root not found: $RepoRoot"
    exit 1
}

# Remove existing output file
if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Force
}

# Get all files, excluding patterns
Write-Info "Collecting files..."

$allFiles = Get-ChildItem -Path $RepoRoot -Recurse -File -Force
$includedFiles = @()
$excludedCount = 0

foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/')
    
    if (Test-ShouldExclude -RelativePath $relativePath) {
        $excludedCount++
    }
    else {
        $includedFiles += @{
            FullName = $file.FullName
            RelativePath = $relativePath
        }
    }
}

Write-Info "Found $($includedFiles.Count) files to include ($excludedCount excluded)"

# Create ZIP using .NET (more control than Compress-Archive)
Write-Info "Creating ZIP archive..."

Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($OutputPath, 'Create')

try {
    foreach ($file in $includedFiles) {
        # Use forward slashes in ZIP for cross-platform compatibility
        $entryName = $file.RelativePath -replace '\\', '/'
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip, 
            $file.FullName, 
            $entryName,
            [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

# Get file size
$zipSize = (Get-Item $OutputPath).Length
$zipSizeMB = [math]::Round($zipSize / 1MB, 2)

# ============================================================================
# Success
# ============================================================================

Write-Host ""
Write-Info "Repository packed successfully!"
Write-Host ""
Write-Host "  Output: $OutputPath"
Write-Host "  Size:   $zipSizeMB MB"
Write-Host "  Files:  $($includedFiles.Count)"
Write-Host ""
