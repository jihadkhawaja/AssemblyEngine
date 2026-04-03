#Requires -Version 5.1
# ============================================================================
# AssemblyEngine Build Script
# Prerequisites: NASM, MSVC link.exe (Visual Studio), .NET 10 SDK
# ============================================================================

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

function Get-VisualStudioInstallationPath {
    $vswhere = Get-VsWherePath
    $installationPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($installationPath)) {
        throw 'The MSVC x64 toolchain was not found. Install the Desktop development with C++ workload in Visual Studio.'
    }

    return $installationPath.Trim()
}

function Get-LinkPath {
    $vswhere = Get-VsWherePath
    $linkPaths = & $vswhere -latest -products * -find 'VC\Tools\MSVC\**\bin\Hostx64\x64\link.exe'
    if ($LASTEXITCODE -ne 0 -or $null -eq $linkPaths -or $linkPaths.Count -eq 0) {
        throw 'The MSVC x64 linker was not found. Install the Desktop development with C++ workload in Visual Studio.'
    }

    return ($linkPaths | Select-Object -First 1).Trim()
}

function Format-CmdArgument {
    param([Parameter(Mandatory)][string] $Value)

    if ($Value -match '[\s"]') {
        return '"{0}"' -f $Value.Replace('"', '\"')
    }

    return $Value
}

function Invoke-MsvcLink {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments,

        [Parameter(Mandatory)]
        [string] $WorkingDirectory
    )

    $installationPath = Get-VisualStudioInstallationPath
    $vcVars = Join-Path $installationPath 'VC\Auxiliary\Build\vcvars64.bat'
    $linkPath = Get-LinkPath

    if (-not (Test-Path $vcVars)) {
        throw "The MSVC environment script was not found at '$vcVars'."
    }

    $argString = ($Arguments | ForEach-Object { Format-CmdArgument -Value $_ }) -join ' '
    $cmdScript = "call `"$vcVars`" >nul && `"$linkPath`" $argString"

    Push-Location $WorkingDirectory
    try {
        & cmd.exe /d /s /c $cmdScript
        if ($LASTEXITCODE -ne 0) {
            throw 'Link failed. See errors above.'
        }
    } finally {
        Pop-Location
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

$NasmExe = Resolve-ExecutablePath -Name 'nasm' -FallbackPaths @(
    (Join-Path $env:LOCALAPPDATA 'Programs\NASM\nasm-3.01\nasm.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\NASM\nasm.exe'),
    'C:\Program Files\NASM\nasm.exe',
    'C:\Program Files (x86)\NASM\nasm.exe'
) -HelpText 'Install NASM and add it to PATH before running this script.'
$DotnetExe = Resolve-ExecutablePath -Name 'dotnet' -FallbackPaths @(
    'C:\Program Files\dotnet\dotnet.exe',
    'C:\Program Files\dotnet\x64\dotnet.exe'
) -HelpText '.NET 10 SDK is required to build the runtime and sample projects.'

# --- Create build directories ---
New-Item -ItemType Directory -Path $BuildDir, $OutDir -Force | Out-Null

# --- Step 1: Assemble NASM modules ---
Write-Host "[1/4] Assembling native core modules..."

$asmFiles = @('platform_win64', 'renderer', 'sprite', 'input', 'timer', 'memory', 'audio', 'math')

Push-Location $CoreDir
try {
    foreach ($f in $asmFiles) {
        Write-Host "  Assembling $f.asm..."
        & $NasmExe -f win64 -I . "$f.asm" -o (Join-Path $BuildDir "$f.obj")
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to assemble $f.asm"
        }
    }
} finally {
    Pop-Location
}

Write-Host "  Assembly complete."
Write-Host ""

# --- Step 2: Link into DLL ---
Write-Host "[2/4] Linking assemblycore.dll..."

$objFiles = $asmFiles | ForEach-Object { "$_.obj" }
$defFile  = Join-Path $CoreDir "exports.def" | Resolve-Path

$linkArgs = @(
    '/DLL'
    '/MACHINE:X64'
    '/OUT:output\assemblycore.dll'
    "/DEF:$defFile"
) + $objFiles + @(
    'kernel32.lib', 'user32.lib', 'gdi32.lib', 'winmm.lib'
    '/NODEFAULTLIB', '/NOENTRY'
)

Invoke-MsvcLink -Arguments $linkArgs -WorkingDirectory $BuildDir

Write-Host "  DLL linked successfully."
Write-Host ""

# --- Step 3: Build C# Runtime ---
Write-Host "[3/4] Building C# runtime..."
& $DotnetExe build (Join-Path $RuntimeDir "AssemblyEngine.Runtime.csproj") -c Release -o $OutDir
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build C# runtime."
}
Write-Host "  Runtime built successfully."
Write-Host ""

# --- Step 4: Build Sample Game ---
Write-Host "[4/4] Building sample game..."
& $DotnetExe build (Join-Path $SampleDir "SampleGame.csproj") -c Release -o $OutDir
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build sample game."
}

# Copy UI files to output
$uiSource = Join-Path $SampleDir "ui"
if (Test-Path $uiSource) {
    $uiDest = Join-Path $OutDir "ui"
    New-Item -ItemType Directory -Path $uiDest -Force | Out-Null
    Copy-Item -Path "$uiSource\*" -Destination $uiDest -Force
}

Write-Host "  Sample game built successfully."
Write-Host ""

Write-Host "==================================="
Write-Host " Build Complete!"
Write-Host " Output: $OutDir"
Write-Host " Run:    $OutDir\SampleGame.exe"
Write-Host "==================================="
