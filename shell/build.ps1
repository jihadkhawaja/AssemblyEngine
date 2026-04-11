#Requires -Version 5.1
# ============================================================================
# AssemblyEngine Build Script
# Prerequisites: .NET 10 SDK
# ============================================================================

param(
    [string] $TargetArchitecture,
    [ValidateSet('basic', 'visual-novel', 'fps', 'rts')]
    [string] $Sample = 'basic'
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
$RuntimeDir = Join-Path $PSScriptRoot "..\src\runtime"
$McpServerDir = Join-Path $PSScriptRoot "..\src\tools\AssemblyEngine.RuntimeMcpServer"
$BuildDir   = Join-Path $PSScriptRoot "..\build"
$OutDir     = Join-Path $PSScriptRoot "..\build\output"
$McpOutDir  = Join-Path $OutDir "mcp"
$HostArchitecture = Get-HostArchitecture
$ResolvedTargetArchitecture = Resolve-TargetArchitecture -RequestedArchitecture $TargetArchitecture -HostArchitecture $HostArchitecture

$DotnetExe = Resolve-ExecutablePath -Name 'dotnet' -FallbackPaths @(
    'C:\Program Files\dotnet\dotnet.exe',
    'C:\Program Files\dotnet\x64\dotnet.exe'
) -HelpText '.NET 10 SDK is required to build the runtime and sample projects.'

$SampleProjects = @{
    'basic' = @{
        Directory = Join-Path $PSScriptRoot '..\sample\basic'
        ProjectFile = 'SampleGame.csproj'
        AssemblyName = 'SampleGame'
    }
    'fps' = @{
        Directory = Join-Path $PSScriptRoot '..\sample\fps'
        ProjectFile = 'FpsSample.csproj'
        AssemblyName = 'FpsSample'
    }
    'rts' = @{
        Directory = Join-Path $PSScriptRoot '..\sample\rts'
        ProjectFile = 'RtsSample.csproj'
        AssemblyName = 'RtsSample'
    }
    'visual-novel' = @{
        Directory = Join-Path $PSScriptRoot '..\sample\visual-novel'
        ProjectFile = 'VisualNovelSample.csproj'
        AssemblyName = 'VisualNovelSample'
    }
}

$SelectedSample = $SampleProjects[$Sample]
if ($null -eq $SelectedSample) {
    throw "Unknown sample '$Sample'."
}

$SampleDir = $SelectedSample.Directory
$SampleProject = Join-Path $SampleDir $SelectedSample.ProjectFile
$SampleAssemblyName = $SelectedSample.AssemblyName

# --- Create build directories ---
New-Item -ItemType Directory -Path $BuildDir, $OutDir -Force | Out-Null

# --- Step 1: Build C# Runtime ---
Write-Host "[1/3] Building C# runtime..."
& $DotnetExe build (Join-Path $RuntimeDir "AssemblyEngine.Runtime.csproj") -c Release -o $OutDir
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build C# runtime."
}
Write-Host "  Runtime built successfully."
Write-Host ""

# --- Step 2: Build MCP Server ---
Write-Host "[2/3] Building runtime MCP server..."
New-Item -ItemType Directory -Path $McpOutDir -Force | Out-Null
& $DotnetExe build (Join-Path $McpServerDir "AssemblyEngine.RuntimeMcpServer.csproj") -c Release -o $McpOutDir
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build the runtime MCP server."
}
Write-Host "  Runtime MCP server built successfully."
Write-Host ""

# --- Step 3: Publish Sample Game ---
Write-Host ("[3/3] Publishing sample '{0}'..." -f $Sample)
$samplePlatform = if ($ResolvedTargetArchitecture -eq 'arm64') { 'ARM64' } else { 'x64' }
$sampleRid = if ($ResolvedTargetArchitecture -eq 'arm64') { 'win-arm64' } else { 'win-x64' }
$samplePublishArtifacts = @()
foreach ($sampleDefinition in $SampleProjects.Values) {
    $assemblyName = $sampleDefinition.AssemblyName
    $samplePublishArtifacts += @(
        ("{0}.exe" -f $assemblyName),
        ("{0}.dll" -f $assemblyName),
        ("{0}.pdb" -f $assemblyName),
        ("{0}.deps.json" -f $assemblyName),
        ("{0}.runtimeconfig.json" -f $assemblyName)
    )
}

foreach ($artifact in $samplePublishArtifacts) {
    $artifactPath = Join-Path $OutDir $artifact
    if (Test-Path $artifactPath) {
        Remove-Item $artifactPath -Force
    }
}

& $DotnetExe publish $SampleProject -c Release -o $OutDir -p:Platform=$samplePlatform -p:RuntimeIdentifier=$sampleRid
if ($LASTEXITCODE -ne 0) {
    throw "Failed to publish sample game."
}

# Copy UI files to output
$uiSource = Join-Path $SampleDir "ui"
if (Test-Path $uiSource) {
    $uiDest = Join-Path $OutDir "ui"
    if (Test-Path $uiDest) {
        Remove-Item $uiDest -Recurse -Force
    }
    New-Item -ItemType Directory -Path $uiDest -Force | Out-Null
    Copy-Item -Path "$uiSource\*" -Destination $uiDest -Force
}

Write-Host ("  Sample '{0}' published successfully." -f $Sample)
Write-Host ""

Write-Host "==================================="
Write-Host " Build Complete!"
Write-Host " Output: $OutDir"
Write-Host (" Sample: $Sample")
Write-Host (" Run:    {0}\{1}.exe" -f $OutDir, $SampleAssemblyName)
Write-Host " MCP:    $McpOutDir\AssemblyEngine.RuntimeMcpServer.exe"
Write-Host "==================================="
