#Requires -Version 5.1
# Validate the local toolchain required to build AssemblyEngine on Windows x64 and Windows ARM64.

param(
    [string] $TargetArchitecture
)

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

function Get-VisualStudioInstallationPath {
    param([string] $VsWherePath)

    if ([string]::IsNullOrWhiteSpace($VsWherePath)) {
        return $null
    }

    $installationPath = & $VsWherePath -latest -products * -property installationPath
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($installationPath)) {
        return $null
    }

    return $installationPath.Trim()
}

function Get-VsDevCmdPathForLinkPath {
    param([string] $LinkPath)

    if ([string]::IsNullOrWhiteSpace($LinkPath)) {
        return $null
    }

    if ($LinkPath -match '^(?<InstallRoot>.+)\\VC\\Tools\\MSVC\\[^\\]+\\bin\\Host[^\\]+\\[^\\]+\\link\.exe$') {
        $vsDevCmdPath = Join-Path $Matches.InstallRoot 'Common7\Tools\VsDevCmd.bat'
        if (Test-Path $vsDevCmdPath) {
            return $vsDevCmdPath
        }
    }

    return $null
}

function Get-LinkToolInfo {
    param(
        [string] $VsWherePath,

        [ValidateSet('x64', 'arm64')]
        [string] $TargetArchitecture,

        [ValidateSet('amd64', 'arm64')]
        [string] $PreferredHostArchitecture
    )

    if ([string]::IsNullOrWhiteSpace($VsWherePath)) {
        return $null
    }

    $patterns = if ($TargetArchitecture -eq 'arm64') {
        if ($PreferredHostArchitecture -eq 'arm64') {
            @(
                'VC\Tools\MSVC\**\bin\HostARM64\arm64\link.exe'
                'VC\Tools\MSVC\**\bin\Hostx64\arm64\link.exe'
            )
        }
        else {
            @(
                'VC\Tools\MSVC\**\bin\Hostx64\arm64\link.exe'
                'VC\Tools\MSVC\**\bin\HostARM64\arm64\link.exe'
            )
        }
    }
    else {
        if ($PreferredHostArchitecture -eq 'arm64') {
            @(
                'VC\Tools\MSVC\**\bin\HostARM64\x64\link.exe'
                'VC\Tools\MSVC\**\bin\Hostx64\x64\link.exe'
            )
        }
        else {
            @(
                'VC\Tools\MSVC\**\bin\Hostx64\x64\link.exe'
                'VC\Tools\MSVC\**\bin\HostARM64\x64\link.exe'
            )
        }
    }

    foreach ($pattern in $patterns) {
        $linkPaths = @(& $VsWherePath -products * -find $pattern 2>$null | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        if ($LASTEXITCODE -eq 0 -and $linkPaths.Count -gt 0) {
            $path = ($linkPaths | Select-Object -First 1).Trim()
            $hostArchitecture = if ($path -match '\\HostARM64\\') { 'arm64' } else { 'amd64' }
            return [pscustomobject]@{
                Path = $path
                HostArchitecture = $hostArchitecture
            }
        }
    }

    return $null
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

$hostArchitecture = Get-HostArchitecture
$resolvedTargetArchitecture = Resolve-TargetArchitecture -RequestedArchitecture $TargetArchitecture -HostArchitecture $hostArchitecture
$missing = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

$dotnetPath = Find-Executable -Name 'dotnet' -FallbackPaths @(
    'C:\Program Files\dotnet\dotnet.exe',
    'C:\Program Files\dotnet\x64\dotnet.exe'
)

$x64DotnetPath = 'C:\Program Files\dotnet\x64\dotnet.exe'

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

if ($hostArchitecture -eq 'arm64') {
    if (Test-Path $x64DotnetPath) {
        Write-Status -Label '.NET x64 runtime host' -Message $x64DotnetPath -State 'OK'
    }
    else {
        $warnings.Add('Install the x64 .NET runtime or SDK under %ProgramFiles%\dotnet\x64 so the win-x64 sample can run under emulation.')
        Write-Status -Label '.NET x64 runtime host' -Message 'Recommended on Windows ARM64. Install the x64 .NET runtime or SDK to run SampleGame.exe.' -State 'WARN'
    }
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
    if ($resolvedTargetArchitecture -eq 'x64') {
        $missing.Add('NASM')
        Write-Status -Label 'NASM' -Message 'Install NASM and add it to PATH for x64 backend builds.' -State 'FAIL'
    }
    else {
        $warnings.Add('Install NASM if you also want to build the x64 assembly backend from this machine.')
        Write-Status -Label 'NASM' -Message 'Optional for ARM64 native builds. Install it if you also want x64 backend builds.' -State 'WARN'
    }
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
    $missing.Add('Visual Studio installation')
    Write-Status -Label 'Visual Studio C++ workload' -Message 'Install the Desktop development with C++ workload.' -State 'FAIL'
}

$primaryLinkTool = Get-LinkToolInfo -VsWherePath $vsWherePath -TargetArchitecture $resolvedTargetArchitecture -PreferredHostArchitecture $hostArchitecture
if ($null -ne $primaryLinkTool) {
    Write-Status -Label 'link.exe' -Message ("{0} ({1} host -> {2} target)" -f $primaryLinkTool.Path, $primaryLinkTool.HostArchitecture, $resolvedTargetArchitecture) -State 'OK'
}
else {
    $missing.Add('link.exe')
    Write-Status -Label 'link.exe' -Message ("Install the MSVC {0} linker through the C++ workload." -f $resolvedTargetArchitecture) -State 'FAIL'
}

$compatibilityArchitecture = if ($resolvedTargetArchitecture -eq 'x64') { 'arm64' } else { 'x64' }
$compatibilityLinkTool = Get-LinkToolInfo -VsWherePath $vsWherePath -TargetArchitecture $compatibilityArchitecture -PreferredHostArchitecture $hostArchitecture
if ($null -ne $compatibilityLinkTool) {
    Write-Status -Label ("link.exe ({0})" -f $compatibilityArchitecture) -Message ("{0} ({1} host -> {2} target)" -f $compatibilityLinkTool.Path, $compatibilityLinkTool.HostArchitecture, $compatibilityArchitecture) -State 'OK'
}
else {
    $warnings.Add(("Install the MSVC {0} target tools if you also want to build the {0} backend from this machine." -f $compatibilityArchitecture))
    Write-Status -Label ("link.exe ({0})" -f $compatibilityArchitecture) -Message ("Optional. Install the MSVC {0} target tools for compatibility builds." -f $compatibilityArchitecture) -State 'WARN'
}

$vsDevCmdPath = if ($null -ne $primaryLinkTool) {
    Get-VsDevCmdPathForLinkPath -LinkPath $primaryLinkTool.Path
}
else {
    $null
}

if ($null -ne $vsDevCmdPath) {
    Write-Status -Label 'VsDevCmd.bat' -Message $vsDevCmdPath -State 'OK'
}
else {
    $missing.Add('VsDevCmd.bat')
    Write-Status -Label 'VsDevCmd.bat' -Message 'Install the Desktop development with C++ workload.' -State 'FAIL'
}

Write-Host ''

if ($hostArchitecture -eq 'arm64') {
    Write-Host ("Windows ARM64 host detected. Default native build target: {0}." -f $resolvedTargetArchitecture)
    Write-Host 'The x64 compatibility path remains available when the x64 runtime, linker, and NASM are installed.'
    Write-Host ''
}

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

if ($warnings.Count -gt 0) {
    Write-Host 'Recommended follow-up:'
    foreach ($item in $warnings) {
        Write-Host (' - {0}' -f $item)
    }

    Write-Host ''
}

Write-Host 'Environment looks ready.'
Write-Host ''
Write-Host 'Next steps:'
Write-Host ("  1. powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\build.ps1 -TargetArchitecture {0}" -f $resolvedTargetArchitecture)
Write-Host '  2. .\build\output\SampleGame.exe'
