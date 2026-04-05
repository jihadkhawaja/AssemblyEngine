#Requires -Version 5.1
# Install or validate the local toolchain required to build and run AssemblyEngine on Windows x64 and Windows ARM64.

param(
    [string] $TargetArchitecture,
    [switch] $CheckOnly,
    [switch] $SkipRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solutionPath = Join-Path $repoRoot 'AssemblyEngine.slnx'
$buildToolsConfigPath = Join-Path $PSScriptRoot 'assemblyengine.buildtools.vsconfig'
$x64DotnetPath = 'C:\Program Files\dotnet\x64\dotnet.exe'
$dotnetFallbackPaths = @(
    'C:\Program Files\dotnet\dotnet.exe',
    $x64DotnetPath
)
$nasmFallbackPaths = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\NASM\nasm-3.01\nasm.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\NASM\nasm.exe'),
    'C:\Program Files\NASM\nasm.exe',
    'C:\Program Files (x86)\NASM\nasm.exe'
)
$dotnetSdkPackageId = 'Microsoft.DotNet.SDK.10'
$dotnetRuntimePackageId = 'Microsoft.DotNet.Runtime.10'
$nasmPackageId = 'NASM.NASM'
$buildToolsPackageId = 'Microsoft.VisualStudio.2022.BuildTools'

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

function Add-UniqueListItem {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.List[string]] $List,

        [Parameter(Mandatory)]
        [string] $Item
    )

    if (-not $List.Contains($Item)) {
        $List.Add($Item)
    }
}

function Get-WingetPath {
    return Find-Executable -Name 'winget'
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

function Get-VisualStudioInstallerPath {
    param([string] $VsWherePath)

    $candidates = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($VsWherePath)) {
        Add-UniqueListItem -List $candidates -Item (Join-Path (Split-Path $VsWherePath -Parent) 'setup.exe')
    }

    Add-UniqueListItem -List $candidates -Item 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\setup.exe'
    Add-UniqueListItem -List $candidates -Item 'C:\Program Files\Microsoft Visual Studio\Installer\setup.exe'

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
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

function Invoke-Process {
    param(
        [Parameter(Mandatory)]
        [string] $FilePath,

        [string[]] $Arguments = @(),

        [Parameter(Mandatory)]
        [string] $FailureMessage
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw ("{0} (exit code {1})." -f $FailureMessage, $LASTEXITCODE)
    }
}

function Assert-VisualStudioClosed {
    $devenv = Get-Process -Name 'devenv' -ErrorAction SilentlyContinue
    if ($null -ne $devenv) {
        throw 'Close Visual Studio before running setup so the required C++ workloads and components can be installed.'
    }
}

function Get-SetupState {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('amd64', 'arm64')]
        [string] $HostArchitecture,

        [Parameter(Mandatory)]
        [ValidateSet('x64', 'arm64')]
        [string] $ResolvedTargetArchitecture
    )

    $missing = New-Object System.Collections.Generic.List[string]
    $warnings = New-Object System.Collections.Generic.List[string]

    $dotnetPath = Find-Executable -Name 'dotnet' -FallbackPaths $dotnetFallbackPaths
    $sdks = @()
    $net10Sdk = $null
    if ($null -ne $dotnetPath) {
        $sdks = @(& $dotnetPath --list-sdks 2>$null)
        $net10Sdk = $sdks | Where-Object { $_ -match '^10\.' } | Select-Object -First 1
    }

    if ($null -eq $dotnetPath) {
        Add-UniqueListItem -List $missing -Item 'dotnet'
    }
    elseif ($null -eq $net10Sdk) {
        Add-UniqueListItem -List $missing -Item '.NET 10 SDK'
    }

    $hasX64Dotnet = Test-Path $x64DotnetPath
    if ($HostArchitecture -eq 'arm64' -and -not $hasX64Dotnet) {
        Add-UniqueListItem -List $warnings -Item 'Install the x64 .NET runtime or SDK under %ProgramFiles%\dotnet\x64 so the win-x64 sample can run under emulation.'
    }

    $nasmPath = Find-Executable -Name 'nasm' -FallbackPaths $nasmFallbackPaths
    if ($null -eq $nasmPath) {
        if ($ResolvedTargetArchitecture -eq 'x64') {
            Add-UniqueListItem -List $missing -Item 'NASM'
        }
        else {
            Add-UniqueListItem -List $warnings -Item 'Install NASM if you also want to build the x64 assembly backend from this machine.'
        }
    }

    $vsWherePath = Get-VsWherePath
    if ($null -eq $vsWherePath) {
        Add-UniqueListItem -List $missing -Item 'Visual Studio installer metadata'
    }

    $installationPath = Get-VisualStudioInstallationPath -VsWherePath $vsWherePath
    if ($null -eq $installationPath) {
        Add-UniqueListItem -List $missing -Item 'Visual Studio installation'
    }

    $primaryLinkTool = Get-LinkToolInfo -VsWherePath $vsWherePath -TargetArchitecture $ResolvedTargetArchitecture -PreferredHostArchitecture $HostArchitecture
    if ($null -eq $primaryLinkTool) {
        Add-UniqueListItem -List $missing -Item 'link.exe'
    }

    $compatibilityArchitecture = if ($ResolvedTargetArchitecture -eq 'x64') { 'arm64' } else { 'x64' }
    $compatibilityLinkTool = Get-LinkToolInfo -VsWherePath $vsWherePath -TargetArchitecture $compatibilityArchitecture -PreferredHostArchitecture $HostArchitecture
    if ($null -eq $compatibilityLinkTool) {
        Add-UniqueListItem -List $warnings -Item (("Install the MSVC {0} target tools if you also want to build the {0} backend from this machine." -f $compatibilityArchitecture))
    }

    $vsDevCmdPath = if ($null -ne $primaryLinkTool) {
        Get-VsDevCmdPathForLinkPath -LinkPath $primaryLinkTool.Path
    }
    else {
        $null
    }

    if ($null -eq $vsDevCmdPath) {
        Add-UniqueListItem -List $missing -Item 'VsDevCmd.bat'
    }

    return [pscustomobject]@{
        HostArchitecture = $HostArchitecture
        ResolvedTargetArchitecture = $ResolvedTargetArchitecture
        DotnetPath = $dotnetPath
        Net10Sdk = if ($null -ne $net10Sdk) { $net10Sdk.Trim() } else { $null }
        HasX64Dotnet = $hasX64Dotnet
        NasmPath = $nasmPath
        VsWherePath = $vsWherePath
        InstallationPath = $installationPath
        PrimaryLinkTool = $primaryLinkTool
        CompatibilityArchitecture = $compatibilityArchitecture
        CompatibilityLinkTool = $compatibilityLinkTool
        VsDevCmdPath = $vsDevCmdPath
        Missing = $missing
        Warnings = $warnings
    }
}

function Show-SetupState {
    param(
        [Parameter(Mandatory)]
        [pscustomobject] $State
    )

    if ($null -ne $State.DotnetPath) {
        Write-Status -Label '.NET CLI' -Message $State.DotnetPath -State 'OK'
    }
    else {
        Write-Status -Label '.NET CLI' -Message 'Install the .NET 10 SDK and ensure dotnet is on PATH.' -State 'FAIL'
    }

    if ($null -ne $State.Net10Sdk) {
        Write-Status -Label '.NET 10 SDK' -Message $State.Net10Sdk -State 'OK'
    }
    else {
        Write-Status -Label '.NET 10 SDK' -Message 'Install the .NET 10 SDK.' -State 'FAIL'
    }

    if ($State.HostArchitecture -eq 'arm64') {
        if ($State.HasX64Dotnet) {
            Write-Status -Label '.NET x64 runtime host' -Message $x64DotnetPath -State 'OK'
        }
        else {
            Write-Status -Label '.NET x64 runtime host' -Message 'Recommended on Windows ARM64. Install the x64 .NET runtime or SDK to run win-x64 sample builds.' -State 'WARN'
        }
    }

    if ($null -ne $State.NasmPath) {
        Write-Status -Label 'NASM' -Message $State.NasmPath -State 'OK'
    }
    else {
        if ($State.ResolvedTargetArchitecture -eq 'x64') {
            Write-Status -Label 'NASM' -Message 'Install NASM and add it to PATH for x64 backend builds.' -State 'FAIL'
        }
        else {
            Write-Status -Label 'NASM' -Message 'Optional for ARM64 native builds. Install it if you also want x64 backend builds.' -State 'WARN'
        }
    }

    if ($null -ne $State.VsWherePath) {
        Write-Status -Label 'vswhere' -Message $State.VsWherePath -State 'OK'
    }
    else {
        Write-Status -Label 'vswhere' -Message 'Install Visual Studio or Visual Studio Build Tools.' -State 'FAIL'
    }

    if ($null -ne $State.InstallationPath) {
        Write-Status -Label 'Visual Studio installation' -Message $State.InstallationPath -State 'OK'
    }
    else {
        Write-Status -Label 'Visual Studio installation' -Message 'Install Visual Studio or Visual Studio Build Tools with the required C++ components.' -State 'FAIL'
    }

    if ($null -ne $State.PrimaryLinkTool) {
        Write-Status -Label 'link.exe' -Message ("{0} ({1} host -> {2} target)" -f $State.PrimaryLinkTool.Path, $State.PrimaryLinkTool.HostArchitecture, $State.ResolvedTargetArchitecture) -State 'OK'
    }
    else {
        Write-Status -Label 'link.exe' -Message ("Install the MSVC {0} linker through the C++ workload." -f $State.ResolvedTargetArchitecture) -State 'FAIL'
    }

    if ($null -ne $State.CompatibilityLinkTool) {
        Write-Status -Label ("link.exe ({0})" -f $State.CompatibilityArchitecture) -Message ("{0} ({1} host -> {2} target)" -f $State.CompatibilityLinkTool.Path, $State.CompatibilityLinkTool.HostArchitecture, $State.CompatibilityArchitecture) -State 'OK'
    }
    else {
        Write-Status -Label ("link.exe ({0})" -f $State.CompatibilityArchitecture) -Message ("Optional. Install the MSVC {0} target tools for compatibility builds." -f $State.CompatibilityArchitecture) -State 'WARN'
    }

    if ($null -ne $State.VsDevCmdPath) {
        Write-Status -Label 'VsDevCmd.bat' -Message $State.VsDevCmdPath -State 'OK'
    }
    else {
        Write-Status -Label 'VsDevCmd.bat' -Message 'Install the Desktop development with C++ workload.' -State 'FAIL'
    }
}

function Install-WingetPackage {
    param(
        [Parameter(Mandatory)]
        [string] $WingetPath,

        [Parameter(Mandatory)]
        [string] $PackageId,

        [Parameter(Mandatory)]
        [string] $DisplayName,

        [string] $Architecture,

        [string[]] $AdditionalArguments = @()
    )

    $arguments = @(
        'install',
        '--id', $PackageId,
        '--exact',
        '--accept-package-agreements',
        '--accept-source-agreements'
    )

    if (-not [string]::IsNullOrWhiteSpace($Architecture)) {
        $arguments += @('--architecture', $Architecture)
    }

    $arguments += $AdditionalArguments

    Write-Host ("Installing {0}..." -f $DisplayName)
    Invoke-Process -FilePath $WingetPath -Arguments $arguments -FailureMessage ("Failed to install {0}" -f $DisplayName)
}

function Install-VisualStudioBuildTools {
    param(
        [Parameter(Mandatory)]
        [string] $WingetPath,

        [string] $VsWherePath,

        [string] $InstallationPath
    )

    if (-not (Test-Path $buildToolsConfigPath)) {
        throw "Build Tools config file was not found at '$buildToolsConfigPath'."
    }

    $resolvedConfigPath = (Resolve-Path $buildToolsConfigPath).Path
    Assert-VisualStudioClosed

    if (-not [string]::IsNullOrWhiteSpace($InstallationPath)) {
        $installerPath = Get-VisualStudioInstallerPath -VsWherePath $VsWherePath
        if ($null -eq $installerPath) {
            throw 'The Visual Studio installer was not found. Repair the Visual Studio installer and rerun setup.'
        }

        Write-Host 'Adding the required Visual Studio C++ workloads and components...'
        Invoke-Process -FilePath $installerPath -Arguments @(
            'modify',
            '--installPath', $InstallationPath,
            '--config', $resolvedConfigPath,
            '--passive',
            '--norestart'
        ) -FailureMessage 'Failed to modify the Visual Studio installation'
        return
    }

    $override = "--passive --norestart --config `"$resolvedConfigPath`""
    Write-Host 'Installing Visual Studio Build Tools with the required C++ workloads and components...'
    Invoke-Process -FilePath $WingetPath -Arguments @(
        'install',
        '--id', $buildToolsPackageId,
        '--exact',
        '--accept-package-agreements',
        '--accept-source-agreements',
        '--override', $override
    ) -FailureMessage 'Failed to install Visual Studio Build Tools'
}

function Install-MissingPrerequisites {
    param(
        [Parameter(Mandatory)]
        [pscustomobject] $State
    )

    $didInstall = $false
    $requiresVisualStudioInstall = ($null -eq $State.InstallationPath) -or ($null -eq $State.PrimaryLinkTool) -or ($null -eq $State.CompatibilityLinkTool) -or ($null -eq $State.VsDevCmdPath)
    $requiresDotnetSdk = $null -eq $State.Net10Sdk
    $requiresX64Runtime = $State.HostArchitecture -eq 'arm64' -and -not $State.HasX64Dotnet
    $requiresNasm = $null -eq $State.NasmPath

    if (-not ($requiresVisualStudioInstall -or $requiresDotnetSdk -or $requiresX64Runtime -or $requiresNasm)) {
        return $false
    }

    $wingetPath = Get-WingetPath
    if ($null -eq $wingetPath) {
        if ($State.Missing.Count -gt 0) {
            throw 'winget was not found. Install App Installer or install the missing prerequisites manually, then rerun .\setup.ps1.'
        }

        Write-Host 'winget was not found. Skipping optional compatibility installs.'
        Write-Host ''
        return $false
    }

    if ($requiresDotnetSdk) {
        Install-WingetPackage -WingetPath $wingetPath -PackageId $dotnetSdkPackageId -DisplayName '.NET 10 SDK' -AdditionalArguments @('--silent')
        $didInstall = $true
    }

    if ($requiresX64Runtime) {
        Install-WingetPackage -WingetPath $wingetPath -PackageId $dotnetRuntimePackageId -DisplayName '.NET 10 x64 runtime' -Architecture 'x64' -AdditionalArguments @('--silent')
        $didInstall = $true
    }

    if ($requiresNasm) {
        Install-WingetPackage -WingetPath $wingetPath -PackageId $nasmPackageId -DisplayName 'NASM' -AdditionalArguments @('--silent')
        $didInstall = $true
    }

    if ($requiresVisualStudioInstall) {
        Install-VisualStudioBuildTools -WingetPath $wingetPath -VsWherePath $State.VsWherePath -InstallationPath $State.InstallationPath
        $didInstall = $true
    }

    return $didInstall
}

function Restore-ManagedDependencies {
    param(
        [Parameter(Mandatory)]
        [string] $DotnetPath,

        [Parameter(Mandatory)]
        [ValidateSet('x64', 'arm64')]
        [string] $ResolvedTargetArchitecture
    )

    if (-not (Test-Path $solutionPath)) {
        throw "Solution file was not found at '$solutionPath'."
    }

    Write-Host 'Restoring managed solution dependencies...'
    Invoke-Process -FilePath $DotnetPath -Arguments @('restore', $solutionPath) -FailureMessage 'dotnet restore failed for the solution'

    if ($ResolvedTargetArchitecture -eq 'arm64') {
        $arm64Project = Join-Path $repoRoot 'src\nativearm64\AssemblyEngine.NativeArm64.csproj'
        if (Test-Path $arm64Project) {
            Write-Host 'Restoring ARM64 native backend dependencies...'
            Invoke-Process -FilePath $DotnetPath -Arguments @('restore', $arm64Project, '-r', 'win-arm64') -FailureMessage 'dotnet restore failed for the ARM64 native backend'
        }
    }
}

function Show-FollowUpLists {
    param(
        [Parameter(Mandatory)]
        [pscustomobject] $State,

        [switch] $AfterBootstrap
    )

    if ($State.HostArchitecture -eq 'arm64') {
        Write-Host ("Windows ARM64 host detected. Default native build target: {0}." -f $State.ResolvedTargetArchitecture)
        Write-Host 'The x64 compatibility path remains available when the x64 runtime, linker, and NASM are installed.'
        Write-Host ''
    }

    if ($State.Missing.Count -gt 0) {
        Write-Host 'Missing prerequisites:'
        foreach ($item in $State.Missing) {
            Write-Host (' - {0}' -f $item)
        }

        Write-Host ''
        if ($AfterBootstrap) {
            throw 'Setup failed. See messages above.'
        }

        Write-Host 'Run .\setup.ps1 again after installing the missing items, or rerun with -CheckOnly to audit without making changes.'
        throw 'Setup check failed. See messages above.'
    }

    if ($State.Warnings.Count -gt 0) {
        Write-Host 'Recommended follow-up:'
        foreach ($item in $State.Warnings) {
            Write-Host (' - {0}' -f $item)
        }

        Write-Host ''
    }
}

Write-Host '==================================='
Write-Host ' AssemblyEngine Setup'
Write-Host '==================================='
Write-Host ''

$hostArchitecture = Get-HostArchitecture
$resolvedTargetArchitecture = Resolve-TargetArchitecture -RequestedArchitecture $TargetArchitecture -HostArchitecture $hostArchitecture
$state = Get-SetupState -HostArchitecture $hostArchitecture -ResolvedTargetArchitecture $resolvedTargetArchitecture

Show-SetupState -State $state
Write-Host ''

if (-not $CheckOnly) {
    $performedBootstrap = Install-MissingPrerequisites -State $state
    if ($performedBootstrap) {
        $state = Get-SetupState -HostArchitecture $hostArchitecture -ResolvedTargetArchitecture $resolvedTargetArchitecture

        Write-Host 'Post-install verification:'
        Show-SetupState -State $state
        Write-Host ''
    }
}

Show-FollowUpLists -State $state -AfterBootstrap:(-not $CheckOnly)

if (-not $CheckOnly -and -not $SkipRestore) {
    if ($null -eq $state.DotnetPath) {
        throw 'dotnet could not be resolved after setup. Restart the shell and rerun .\setup.ps1.'
    }

    Restore-ManagedDependencies -DotnetPath $state.DotnetPath -ResolvedTargetArchitecture $resolvedTargetArchitecture
    Write-Host ''
}

if ($CheckOnly) {
    Write-Host 'Environment looks ready.'
    Write-Host ''
    Write-Host 'Run .\setup.ps1 without -CheckOnly to install anything missing and restore managed dependencies.'
    Write-Host ''
}
else {
    Write-Host 'Environment looks ready.'
    if ($SkipRestore) {
        Write-Host 'Managed dependency restore was skipped.'
    }
    else {
        Write-Host 'Managed dependencies restored.'
    }

    Write-Host ''
}

Write-Host 'Next steps:'
Write-Host ("  1. powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\build.ps1 -TargetArchitecture {0}" -f $resolvedTargetArchitecture)
Write-Host '  2. .\build\output\SampleGame.exe'