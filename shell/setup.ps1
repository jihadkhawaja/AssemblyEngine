#Requires -Version 5.1
# Validate the local toolchain required to build AssemblyEngine on Windows x64.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-Executable {
    param(
        [Parameter(Mandatory)]
        [string] $Name,

        [string[]] $FallbackPaths = @()
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

    return $null
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

    return $null
}

function Get-VisualStudioInstallationPath {
    param([string] $VsWherePath)

    if ([string]::IsNullOrWhiteSpace($VsWherePath)) {
        return $null
    }

    $installationPath = & $VsWherePath -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($installationPath)) {
        return $null
    }

    return $installationPath.Trim()
}

function Get-LinkPath {
    param([string] $VsWherePath)

    if ([string]::IsNullOrWhiteSpace($VsWherePath)) {
        return $null
    }

    $linkPaths = & $VsWherePath -latest -products * -find 'VC\Tools\MSVC\**\bin\Hostx64\x64\link.exe'
    if ($LASTEXITCODE -ne 0 -or $null -eq $linkPaths -or $linkPaths.Count -eq 0) {
        return $null
    }

    return ($linkPaths | Select-Object -First 1).Trim()
}

function Write-Status {
    param(
        [Parameter(Mandatory)]
        [string] $Label,

        [Parameter(Mandatory)]
        [string] $Message,

        [Parameter(Mandatory)]
        [ValidateSet('OK', 'FAIL', 'WARN')]
        [string] $State
    )

    Write-Host ('[{0}] {1}: {2}' -f $State, $Label, $Message)
}

Write-Host '==================================='
Write-Host ' AssemblyEngine Setup Check'
Write-Host '==================================='
Write-Host ''

$missing = New-Object System.Collections.Generic.List[string]

$dotnetPath = Find-Executable -Name 'dotnet' -FallbackPaths @(
    'C:\Program Files\dotnet\dotnet.exe',
    'C:\Program Files\dotnet\x64\dotnet.exe'
)

if ($null -ne $dotnetPath) {
    Write-Status -Label '.NET CLI' -Message $dotnetPath -State 'OK'

    $sdks = & $dotnetPath --list-sdks 2>$null
    $net10Sdk = $sdks | Where-Object { $_ -match '^10\.' } | Select-Object -First 1
    if ($null -ne $net10Sdk) {
        Write-Status -Label '.NET 10 SDK' -Message $net10Sdk.Trim() -State 'OK'
    }
    else {
        $missing.Add('.NET 10 SDK')
        Write-Status -Label '.NET 10 SDK' -Message 'Install the .NET 10 SDK.' -State 'FAIL'
    }
}
else {
    $missing.Add('dotnet')
    Write-Status -Label '.NET CLI' -Message 'Install the .NET 10 SDK and ensure dotnet is on PATH.' -State 'FAIL'
}

$nasmPath = Find-Executable -Name 'nasm' -FallbackPaths @(
    (Join-Path $env:LOCALAPPDATA 'Programs\NASM\nasm-3.01\nasm.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\NASM\nasm.exe'),
    'C:\Program Files\NASM\nasm.exe',
    'C:\Program Files (x86)\NASM\nasm.exe'
)

if ($null -ne $nasmPath) {
    Write-Status -Label 'NASM' -Message $nasmPath -State 'OK'
}
else {
    $missing.Add('NASM')
    Write-Status -Label 'NASM' -Message 'Install NASM and add it to PATH.' -State 'FAIL'
}

$vsWherePath = Get-VsWherePath
if ($null -ne $vsWherePath) {
    Write-Status -Label 'vswhere' -Message $vsWherePath -State 'OK'
}
else {
    $missing.Add('Visual Studio installer metadata')
    Write-Status -Label 'vswhere' -Message 'Install Visual Studio or Visual Studio Build Tools.' -State 'FAIL'
}

$installationPath = Get-VisualStudioInstallationPath -VsWherePath $vsWherePath
if ($null -ne $installationPath) {
    Write-Status -Label 'Visual Studio C++ workload' -Message $installationPath -State 'OK'
}
else {
    $missing.Add('MSVC x64 toolchain')
    Write-Status -Label 'Visual Studio C++ workload' -Message 'Install the Desktop development with C++ workload.' -State 'FAIL'
}

$linkPath = Get-LinkPath -VsWherePath $vsWherePath
if ($null -ne $linkPath) {
    Write-Status -Label 'link.exe' -Message $linkPath -State 'OK'
}
else {
    $missing.Add('link.exe')
    Write-Status -Label 'link.exe' -Message 'Install the MSVC x64 linker through the C++ workload.' -State 'FAIL'
}

Write-Host ''

if ($missing.Count -gt 0) {
    Write-Host 'Missing prerequisites:'
    foreach ($item in $missing) {
        Write-Host (' - {0}' -f $item)
    }

    Write-Host ''
    Write-Host 'After installing the missing items, run:'
    Write-Host '  .\setup.ps1'
    throw 'Setup check failed. See messages above.'
}

Write-Host 'Environment looks ready.'
Write-Host ''
Write-Host 'Next steps:'
Write-Host '  1. powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\build.ps1'
Write-Host '  2. .\build\output\SampleGame.exe'
