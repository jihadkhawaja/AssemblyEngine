#Requires -Version 5.1
# ============================================================================
# AssemblyEngine Setup Script
# Validates and installs prerequisites for building the engine.
# Prerequisites: .NET 10 SDK
# ============================================================================

param(
    [switch] $CheckOnly,
    [switch] $SkipRestore,
    [string] $TargetArchitecture
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ============================================================================
# Helpers
# ============================================================================

function Write-Status {
    param(
        [string] $Label,
        [string] $Value,
        [switch] $Ok,
        [switch] $Warn,
        [switch] $Fail
    )

    if ($Ok) {
        Write-Host ("  [OK]   {0}: {1}" -f $Label, $Value) -ForegroundColor Green
    }
    elseif ($Warn) {
        Write-Host ("  [WARN] {0}: {1}" -f $Label, $Value) -ForegroundColor Yellow
    }
    elseif ($Fail) {
        Write-Host ("  [FAIL] {0}: {1}" -f $Label, $Value) -ForegroundColor Red
    }
    else {
        Write-Host ("  [INFO] {0}: {1}" -f $Label, $Value)
    }
}

function Get-HostArchitecture {
    $architecture = if (-not [string]::IsNullOrWhiteSpace($env:PROCESSOR_ARCHITEW6432)) {
        $env:PROCESSOR_ARCHITEW6432
    }
    else {
        $env:PROCESSOR_ARCHITECTURE
    }

    if ([string]::IsNullOrWhiteSpace($architecture)) {
        return 'amd64'
    }

    switch ($architecture.Trim().ToUpperInvariant()) {
        'ARM64' { return 'arm64' }
        'AMD64' { return 'amd64' }
        default { return $architecture.Trim().ToLowerInvariant() }
    }
}

function Resolve-TargetArchitecture {
    param(
        [string] $RequestedArchitecture,

        [Parameter(Mandatory)]
        [ValidateSet('amd64', 'arm64')]
        [string] $HostArchitecture
    )

    if ([string]::IsNullOrWhiteSpace($RequestedArchitecture)) {
        if ($HostArchitecture -eq 'arm64') { return 'arm64' }
        return 'x64'
    }

    switch ($RequestedArchitecture.Trim().ToLowerInvariant()) {
        'x64' { return 'x64' }
        'amd64' { return 'x64' }
        'arm64' { return 'arm64' }
        default { throw "Unsupported target architecture '$RequestedArchitecture'. Use x64 or arm64." }
    }
}

function Find-DotnetSdk {
    $candidates = @(
        'dotnet'
    )

    $fallbackPaths = @(
        'C:\Program Files\dotnet\dotnet.exe',
        'C:\Program Files\dotnet\x64\dotnet.exe'
    )

    foreach ($name in $candidates) {
        $command = Get-Command $name -CommandType Application -ErrorAction SilentlyContinue
        if ($null -ne $command) {
            return $command.Source
        }
    }

    foreach ($path in $fallbackPaths) {
        if (Test-Path $path) {
            return $path
        }
    }

    return $null
}

function Test-DotnetSdkVersion {
    param(
        [Parameter(Mandatory)]
        [string] $DotnetExe,
        [int] $RequiredMajor = 10
    )

    try {
        $versionOutput = & $DotnetExe --version 2>&1
        if ($LASTEXITCODE -ne 0) { return $false }

        $versionString = ($versionOutput | Select-Object -First 1).ToString().Trim()
        $parts = $versionString -split '\.'
        if ($parts.Length -ge 1) {
            $major = [int]$parts[0]
            return $major -ge $RequiredMajor
        }
    }
    catch {
        return $false
    }

    return $false
}

function Install-DotnetSdk {
    $wingetCommand = Get-Command 'winget' -CommandType Application -ErrorAction SilentlyContinue
    if ($null -eq $wingetCommand) {
        throw ".NET 10 SDK is not installed and winget is not available. Install the .NET 10 SDK manually from https://dotnet.microsoft.com/download/dotnet/10.0"
    }

    Write-Host "  Installing .NET 10 SDK via winget..."
    & winget install Microsoft.DotNet.SDK.10 --accept-source-agreements --accept-package-agreements
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install .NET 10 SDK via winget. Install it manually from https://dotnet.microsoft.com/download/dotnet/10.0"
    }
}

# ============================================================================
# Main
# ============================================================================

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$hostArchitecture = Get-HostArchitecture
$resolvedTarget = Resolve-TargetArchitecture -RequestedArchitecture $TargetArchitecture -HostArchitecture $hostArchitecture

Write-Host '==================================='
Write-Host ' AssemblyEngine Setup'
Write-Host '==================================='
Write-Host ''
Write-Host ("  Host architecture:   {0}" -f $hostArchitecture)
Write-Host ("  Target architecture: {0}" -f $resolvedTarget)
Write-Host ("  Mode:                {0}" -f $(if ($CheckOnly) { 'Audit only' } else { 'Install + restore' }))
Write-Host ''

$allGood = $true

# --- .NET 10 SDK ---
Write-Host 'Checking .NET 10 SDK...'
$dotnetExe = Find-DotnetSdk

if ($null -ne $dotnetExe -and (Test-DotnetSdkVersion -DotnetExe $dotnetExe)) {
    $sdkVersion = (& $dotnetExe --version 2>&1 | Select-Object -First 1).ToString().Trim()
    Write-Status -Label '.NET SDK' -Value "$sdkVersion ($dotnetExe)" -Ok
}
elseif ($CheckOnly) {
    Write-Status -Label '.NET SDK' -Value 'Not found or below version 10' -Fail
    $allGood = $false
}
else {
    Write-Status -Label '.NET SDK' -Value 'Not found or below version 10 - installing' -Warn
    Install-DotnetSdk
    $dotnetExe = Find-DotnetSdk
    if ($null -eq $dotnetExe -or -not (Test-DotnetSdkVersion -DotnetExe $dotnetExe)) {
        Write-Status -Label '.NET SDK' -Value 'Installation did not produce a usable SDK' -Fail
        $allGood = $false
    }
    else {
        $sdkVersion = (& $dotnetExe --version 2>&1 | Select-Object -First 1).ToString().Trim()
        Write-Status -Label '.NET SDK' -Value "$sdkVersion (installed)" -Ok
    }
}

Write-Host ''

# --- Restore ---
if (-not $CheckOnly -and -not $SkipRestore -and $allGood) {
    Write-Host 'Restoring solution dependencies...'
    $slnxPath = Join-Path $repoRoot 'AssemblyEngine.slnx'
    if (Test-Path $slnxPath) {
        & $dotnetExe restore $slnxPath
        if ($LASTEXITCODE -ne 0) {
            Write-Status -Label 'Restore' -Value 'dotnet restore failed' -Fail
            $allGood = $false
        }
        else {
            Write-Status -Label 'Restore' -Value 'OK' -Ok
        }
    }
    else {
        Write-Status -Label 'Restore' -Value "Solution file not found at $slnxPath" -Warn
    }
}

Write-Host ''

if ($allGood) {
    Write-Host '==================================='
    Write-Host ' Setup Complete - Ready to build!'
    Write-Host '==================================='
}
else {
    Write-Host '==================================='
    Write-Host ' Setup found issues (see above)'
    Write-Host '==================================='
    exit 1
}
