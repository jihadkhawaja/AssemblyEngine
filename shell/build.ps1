#Requires -Version 5.1
# ============================================================================
# AssemblyEngine Build Script
# Prerequisites: .NET 10 SDK, MSVC toolchain, NASM for x64 backend builds
# Builds the matching native backend for x64 or ARM64.
# ============================================================================

param(
    [string] $TargetArchitecture
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-ExecutablePath {
    param(
        [Parameter(Mandatory)]
        [string] $Name,

        [string[]] $FallbackPaths = @(),

        [Parameter(Mandatory)]
        [string] $HelpText
    )

    $command = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    foreach ($path in $FallbackPaths) {
        if (-not [string]::IsNullOrWhiteSpace($path) -and (Test-Path $path)) {
            return $path
        }
    }

    throw "Missing required tool '$Name'. $HelpText"
}

function Get-VsWherePath {
    $paths = @(
        'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe',
        'C:\Program Files\Microsoft Visual Studio\Installer\vswhere.exe'
    )

    foreach ($path in $paths) {
        if (Test-Path $path) {
            return $path
        }
    }

    throw 'Visual Studio installer metadata was not found. Install Visual Studio with the Desktop development with C++ workload.'
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
        if ($HostArchitecture -eq 'arm64') {
            return 'arm64'
        }

        return 'x64'
    }

    switch ($RequestedArchitecture.Trim().ToLowerInvariant()) {
        'x64' { return 'x64' }
        'amd64' { return 'x64' }
        'arm64' { return 'arm64' }
        default { throw "Unsupported target architecture '$RequestedArchitecture'. Use x64 or arm64." }
    }
}

Write-Host "==================================="
Write-Host " AssemblyEngine Build System"
Write-Host "==================================="
Write-Host ""

# --- Configuration ---
$CoreDir    = Join-Path $PSScriptRoot "..\src\core"
$RuntimeDir = Join-Path $PSScriptRoot "..\src\runtime"
$SampleDir  = Join-Path $PSScriptRoot "..\sample\basic"
$BuildDir   = Join-Path $PSScriptRoot "..\build"
$OutDir     = Join-Path $PSScriptRoot "..\build\output"
$HostArchitecture = Get-HostArchitecture
$ResolvedTargetArchitecture = Resolve-TargetArchitecture -RequestedArchitecture $TargetArchitecture -HostArchitecture $HostArchitecture
$CoreBuildScript = Join-Path $PSScriptRoot 'build_core.ps1'

$DotnetExe = Resolve-ExecutablePath -Name 'dotnet' -FallbackPaths @(
    'C:\Program Files\dotnet\dotnet.exe',
    'C:\Program Files\dotnet\x64\dotnet.exe'
) -HelpText '.NET 10 SDK is required to build the runtime and sample projects.'

# --- Create build directories ---
New-Item -ItemType Directory -Path $BuildDir, $OutDir -Force | Out-Null

# --- Step 1: Build native core ---
Write-Host ("[1/3] Building native core ({0})..." -f $ResolvedTargetArchitecture)
& $CoreBuildScript -TargetArchitecture $ResolvedTargetArchitecture
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build the native core for $ResolvedTargetArchitecture."
}
Write-Host "  Native core built successfully."
Write-Host ""

# --- Step 2: Build C# Runtime ---
Write-Host "[2/3] Building C# runtime..."
& $DotnetExe build (Join-Path $RuntimeDir "AssemblyEngine.Runtime.csproj") -c Release -o $OutDir
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build C# runtime."
}
Write-Host "  Runtime built successfully."
Write-Host ""

# --- Step 3: Publish Sample Game ---
Write-Host "[3/3] Publishing sample game..."
$samplePlatform = if ($ResolvedTargetArchitecture -eq 'arm64') { 'ARM64' } else { 'x64' }
$sampleRid = if ($ResolvedTargetArchitecture -eq 'arm64') { 'win-arm64' } else { 'win-x64' }
$samplePublishArtifacts = @(
    'SampleGame.exe',
    'SampleGame.dll',
    'SampleGame.pdb',
    'SampleGame.deps.json',
    'SampleGame.runtimeconfig.json'
)

foreach ($artifact in $samplePublishArtifacts) {
    $artifactPath = Join-Path $OutDir $artifact
    if (Test-Path $artifactPath) {
        Remove-Item $artifactPath -Force
    }
}

& $DotnetExe publish (Join-Path $SampleDir "SampleGame.csproj") -c Release -o $OutDir -p:Platform=$samplePlatform -p:RuntimeIdentifier=$sampleRid -p:SkipNativeCoreBuild=true
if ($LASTEXITCODE -ne 0) {
    throw "Failed to publish sample game."
}

# Copy UI files to output
$uiSource = Join-Path $SampleDir "ui"
if (Test-Path $uiSource) {
    $uiDest = Join-Path $OutDir "ui"
    New-Item -ItemType Directory -Path $uiDest -Force | Out-Null
    Copy-Item -Path "$uiSource\*" -Destination $uiDest -Force
}

Write-Host "  Sample game published successfully."
Write-Host ""

Write-Host "==================================="
Write-Host " Build Complete!"
Write-Host " Output: $OutDir"
Write-Host " Run:    $OutDir\SampleGame.exe"
Write-Host "==================================="
