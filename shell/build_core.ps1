#Requires -Version 5.1
# Quick build: produces the native DLL only (x64 NASM backend or ARM64 NativeAOT backend)

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

function Get-VsDevCmdPathForLinkPath {
    param(
        [Parameter(Mandatory)]
        [string] $LinkPath
    )

    if ($LinkPath -match '^(?<InstallRoot>.+)\\VC\\Tools\\MSVC\\[^\\]+\\bin\\Host[^\\]+\\x64\\link\.exe$') {
        $vsDevCmdPath = Join-Path $Matches.InstallRoot 'Common7\Tools\VsDevCmd.bat'
        if (Test-Path $vsDevCmdPath) {
            return $vsDevCmdPath
        }
    }

    throw "The Visual Studio developer shell could not be resolved from '$LinkPath'."
}

function Get-LinkToolInfo {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('amd64', 'arm64')]
        [string] $PreferredHostArchitecture
    )

    $vswhere = Get-VsWherePath
    $patterns = if ($PreferredHostArchitecture -eq 'arm64') {
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

    foreach ($pattern in $patterns) {
        $linkPaths = @(& $vswhere -products * -find $pattern 2>$null | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        if ($LASTEXITCODE -eq 0 -and $linkPaths.Count -gt 0) {
            $path = ($linkPaths | Select-Object -First 1).Trim()
            $hostArchitecture = if ($path -match '\\HostARM64\\') { 'arm64' } else { 'amd64' }
            return [pscustomobject]@{
                Path = $path
                HostArchitecture = $hostArchitecture
            }
        }
    }

    throw 'The MSVC x64 linker was not found. Install Visual Studio with the Desktop development with C++ workload and the x64 target tools.'
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
        [string] $WorkingDirectory,

        [Parameter(Mandatory)]
        [string] $LinkPath,

        [Parameter(Mandatory)]
        [string] $VsDevCmdPath,

        [Parameter(Mandatory)]
        [ValidateSet('amd64', 'arm64')]
        [string] $HostArchitecture
    )

    if (-not (Test-Path $VsDevCmdPath)) {
        throw "The Visual Studio developer shell was not found at '$VsDevCmdPath'."
    }

    $argString = ($Arguments | ForEach-Object { Format-CmdArgument -Value $_ }) -join ' '
    $cmdScript = "call `"$VsDevCmdPath`" -arch=amd64 -host_arch=$HostArchitecture >nul && `"$LinkPath`" $argString"

    Push-Location $WorkingDirectory
    try {
        & cmd.exe /d /s /c $cmdScript
        if ($LASTEXITCODE -ne 0) {
            throw 'Link failed.'
        }
    } finally {
        Pop-Location
    }
}

function Build-X64Core {
    param(
        [Parameter(Mandatory)]
        [string] $CoreDir,

        [Parameter(Mandatory)]
        [string] $BuildDir,

        [Parameter(Mandatory)]
        [string] $OutDir,

        [Parameter(Mandatory)]
        [ValidateSet('amd64', 'arm64')]
        [string] $HostArchitecture
    )

    $linkTool = Get-LinkToolInfo -PreferredHostArchitecture $HostArchitecture
    $vsDevCmdPath = Get-VsDevCmdPathForLinkPath -LinkPath $linkTool.Path
    $nasmExe = Resolve-ExecutablePath -Name 'nasm' -FallbackPaths @(
        (Join-Path $env:LOCALAPPDATA 'Programs\NASM\nasm-3.01\nasm.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\NASM\nasm.exe'),
        'C:\Program Files\NASM\nasm.exe',
        'C:\Program Files (x86)\NASM\nasm.exe'
    ) -HelpText 'Install NASM and add it to PATH before running this script.'

    $workingBuildDir = Join-Path $BuildDir 'x64'
    $nativeOutDir = Join-Path $OutDir 'native\x64'
    New-Item -ItemType Directory -Path $workingBuildDir, $nativeOutDir -Force | Out-Null

    $asmFiles = @('platform_win64', 'renderer', 'sprite', 'input', 'timer', 'memory', 'audio', 'math')
    Write-Host 'Assembling native x64 core...'

    Push-Location $CoreDir
    try {
        foreach ($file in $asmFiles) {
            & $nasmExe -f win64 -I . "$file.asm" -o (Join-Path $workingBuildDir "$file.obj")
            if ($LASTEXITCODE -ne 0) {
                throw "FAIL: $file.asm"
            }
        }
    } finally {
        Pop-Location
    }

    Write-Host ("Linking x64 assemblycore.dll with MSVC host tools: {0} -> x64" -f $linkTool.HostArchitecture)
    $objFiles = $asmFiles | ForEach-Object { "$_.obj" }
    $defFile = Join-Path $CoreDir 'exports.def' | Resolve-Path
    $relativeOutput = '..\output\native\x64\assemblycore.dll'

    $linkArgs = @(
        '/DLL'
        '/MACHINE:X64'
        "/OUT:$relativeOutput"
        "/DEF:$defFile"
    ) + $objFiles + @(
        'kernel32.lib', 'user32.lib', 'gdi32.lib', 'winmm.lib', 'dwmapi.lib'
        '/NODEFAULTLIB', '/NOENTRY'
    )

    Invoke-MsvcLink -Arguments $linkArgs -WorkingDirectory $workingBuildDir -LinkPath $linkTool.Path -VsDevCmdPath $vsDevCmdPath -HostArchitecture $linkTool.HostArchitecture

    Copy-Item -Path (Join-Path $nativeOutDir 'assemblycore.dll') -Destination (Join-Path $OutDir 'assemblycore.dll') -Force
}

function Build-Arm64Core {
    param(
        [Parameter(Mandatory)]
        [string] $Arm64Project,

        [Parameter(Mandatory)]
        [string] $BuildDir,

        [Parameter(Mandatory)]
        [string] $OutDir
    )

    $dotnetExe = Resolve-ExecutablePath -Name 'dotnet' -FallbackPaths @(
        'C:\Program Files\dotnet\dotnet.exe',
        'C:\Program Files\dotnet\x64\dotnet.exe'
    ) -HelpText '.NET 10 SDK is required to build the ARM64 native core.'

    $publishDir = Join-Path $BuildDir 'arm64-publish'
    $nativeOutDir = Join-Path $OutDir 'native\arm64'
    New-Item -ItemType Directory -Path $publishDir, $nativeOutDir -Force | Out-Null

    Write-Host 'Publishing native ARM64 core...'
    & $dotnetExe publish $Arm64Project -c Release -r win-arm64 -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to publish the ARM64 native core.'
    }

    $nativeDll = Join-Path $publishDir 'assemblycore.dll'
    if (-not (Test-Path $nativeDll)) {
        throw "ARM64 native core publish output was missing '$nativeDll'."
    }

    Copy-Item -Path $nativeDll -Destination (Join-Path $nativeOutDir 'assemblycore.dll') -Force
    Copy-Item -Path $nativeDll -Destination (Join-Path $OutDir 'assemblycore.dll') -Force
}

$CoreDir  = Join-Path $PSScriptRoot "..\src\core"
$Arm64Project = Join-Path $PSScriptRoot "..\src\nativearm64\AssemblyEngine.NativeArm64.csproj"
$BuildDir = Join-Path $PSScriptRoot "..\build"
$OutDir   = Join-Path $PSScriptRoot "..\build\output"
$HostArchitecture = Get-HostArchitecture
$ResolvedTargetArchitecture = Resolve-TargetArchitecture -RequestedArchitecture $TargetArchitecture -HostArchitecture $HostArchitecture

New-Item -ItemType Directory -Path $BuildDir, $OutDir -Force | Out-Null

switch ($ResolvedTargetArchitecture) {
    'x64' {
        Build-X64Core -CoreDir $CoreDir -BuildDir $BuildDir -OutDir $OutDir -HostArchitecture $HostArchitecture
    }
    'arm64' {
        Build-Arm64Core -Arm64Project $Arm64Project -BuildDir $BuildDir -OutDir $OutDir
    }
}

Write-Host ("Done. Output: build\output\native\{0}\assemblycore.dll" -f $ResolvedTargetArchitecture)
