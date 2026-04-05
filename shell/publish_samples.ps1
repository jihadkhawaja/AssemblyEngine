#Requires -Version 5.1
# Publishes all sample applications for the selected Windows target.

param(
    [string] $TargetArchitecture,
    [string] $OutputRoot
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

function Resolve-OutputPath {
    param(
        [Parameter(Mandatory)]
        [string] $BasePath,

        [string] $RequestedPath
    )

    if ([string]::IsNullOrWhiteSpace($RequestedPath)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($RequestedPath)) {
        return [System.IO.Path]::GetFullPath($RequestedPath)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $RequestedPath))
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildCoreScript = Join-Path $PSScriptRoot 'build_core.ps1'
$dotnetExe = Resolve-ExecutablePath -Name 'dotnet' -FallbackPaths @(
    'C:\Program Files\dotnet\dotnet.exe',
    'C:\Program Files\dotnet\x64\dotnet.exe'
) -HelpText '.NET 10 SDK is required to publish the sample projects.'

$hostArchitecture = Get-HostArchitecture
$resolvedTargetArchitecture = Resolve-TargetArchitecture -RequestedArchitecture $TargetArchitecture -HostArchitecture $hostArchitecture
$resolvedOutputRoot = Resolve-OutputPath -BasePath $repoRoot -RequestedPath $OutputRoot
if ($null -eq $resolvedOutputRoot) {
    $resolvedOutputRoot = Join-Path $repoRoot (Join-Path 'build\sample-publish' $resolvedTargetArchitecture)
}

$samplePlatform = if ($resolvedTargetArchitecture -eq 'arm64') { 'ARM64' } else { 'x64' }
$sampleRid = if ($resolvedTargetArchitecture -eq 'arm64') { 'win-arm64' } else { 'win-x64' }
$publishMode = 'self-contained'

$sampleDefinitions = @(
    @{
        Key = 'basic'
        DisplayName = 'Dash Harvest'
        Project = Join-Path $repoRoot 'sample\basic\SampleGame.csproj'
    },
    @{
        Key = 'fps'
        DisplayName = 'FPS Sample'
        Project = Join-Path $repoRoot 'sample\fps\FpsSample.csproj'
    },
    @{
        Key = 'visual-novel'
        DisplayName = 'Visual Novel Sample'
        Project = Join-Path $repoRoot 'sample\visual-novel\VisualNovelSample.csproj'
    }
)

Write-Host '==================================='
Write-Host ' AssemblyEngine Sample Publisher'
Write-Host '==================================='
Write-Host ''
Write-Host ("Target architecture: {0}" -f $resolvedTargetArchitecture)
Write-Host ("Publish mode:        {0}" -f $publishMode)
Write-Host ("Output root:         {0}" -f $resolvedOutputRoot)
Write-Host ''

Write-Host ("[1/2] Building native core ({0})..." -f $resolvedTargetArchitecture)
& $buildCoreScript -TargetArchitecture $resolvedTargetArchitecture
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build the native core for $resolvedTargetArchitecture."
}
Write-Host '  Native core built successfully.'
Write-Host ''

if (Test-Path $resolvedOutputRoot) {
    Remove-Item $resolvedOutputRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $resolvedOutputRoot -Force | Out-Null

Write-Host '[2/2] Publishing sample applications...'
foreach ($sampleDefinition in $sampleDefinitions) {
    $sampleOutputDir = Join-Path $resolvedOutputRoot $sampleDefinition.Key
    New-Item -ItemType Directory -Path $sampleOutputDir -Force | Out-Null

    Write-Host ("  - {0}" -f $sampleDefinition.DisplayName)
    & $dotnetExe publish $sampleDefinition.Project -c Release -o $sampleOutputDir -p:Platform=$samplePlatform -p:RuntimeIdentifier=$sampleRid -p:SkipNativeCoreBuild=true -p:SelfContained=true
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to publish sample '$($sampleDefinition.Key)'."
    }
}

$bundleReadme = @(
    'AssemblyEngine sample binaries',
    '',
    ("Target platform: Windows {0}" -f $resolvedTargetArchitecture),
    ("Publish mode: {0}" -f $publishMode),
    '',
    'These files are runnable sample applications built from the sample projects.',
    'They are not source archives and not standalone engine SDK/runtime binaries.',
    '',
    'Included sample folders:',
    ' - basic (Dash Harvest)',
    ' - fps',
    ' - visual-novel'
) -join [Environment]::NewLine

Set-Content -Path (Join-Path $resolvedOutputRoot 'README.txt') -Value $bundleReadme -Encoding ascii

Write-Host ''
Write-Host '==================================='
Write-Host ' Publish Complete!'
Write-Host (" Output: {0}" -f $resolvedOutputRoot)
Write-Host '==================================='