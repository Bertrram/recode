<#
.SYNOPSIS
    Compiles the Explorer context menu extension ahead of time.

.DESCRIPTION
    Recode.Shell is a native DLL, not a managed assembly. Explorer loads it
    through a COM surrogate on every right click, and a managed build would have
    to start a runtime in that process before the menu could be drawn.

    Ahead of time compilation needs the MSVC linker, and the .NET compiler
    locates it through vswhere. Neither is on PATH in an ordinary shell, which
    is the entire reason this script exists rather than a bare dotnet publish.
    Without it the build fails with a message about vswhere not being
    recognised, having also picked the wrong linker.

    The result is copied next to recode.exe, which is where the packaged
    manifest expects it and where the extension looks for the executable.

.PARAMETER Architecture
    arm64 or x64. Defaults to the machine's own.

.PARAMETER Output
    Where to copy the DLL. Defaults to dist/<architecture>, beside recode.exe.

.EXAMPLE
    pwsh -File tools/build-shell.ps1
    pwsh -File tools/build-shell.ps1 -Architecture x64
#>
[CmdletBinding()]
param(
    [ValidateSet('arm64', 'x64')]
    [string] $Architecture,

    [string] $Output
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$PSNativeCommandUseErrorActionPreference = $false

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if (-not $Architecture) {
    $Architecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
}
if (-not $Output) {
    $Output = Join-Path $RepoRoot "dist\$Architecture"
}

$HostArchitecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }

function Write-Detail([string] $Message) {
    Write-Host "    $Message" -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# MSVC environment
# ---------------------------------------------------------------------------

function Import-MsvcEnvironment {
    $installer = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
    $vswhere = Join-Path $installer 'vswhere.exe'

    if (-not (Test-Path $vswhere)) {
        throw @'
Visual Studio Build Tools were not found, and ahead of time compilation needs
the MSVC linker.

  winget install --id Microsoft.VisualStudio.2022.BuildTools -e --override "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 --add Microsoft.VisualStudio.Component.VC.Tools.ARM64 --add Microsoft.VisualStudio.Component.Windows11SDK.22621"
'@
    }

    $visualStudio = (& $vswhere -latest -products * `
        -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
        -property installationPath | Select-Object -First 1)

    if ([string]::IsNullOrWhiteSpace($visualStudio)) {
        throw 'Visual Studio Build Tools are installed but the C++ toolset is missing.'
    }

    $vcvarsall = Join-Path $visualStudio.Trim() 'VC\Auxiliary\Build\vcvarsall.bat'
    if (-not (Test-Path $vcvarsall)) {
        throw "vcvarsall.bat was not found at $vcvarsall."
    }

    # vcvarsall takes host_target when they differ, and just the target when
    # they are the same.
    $argument = if ($HostArchitecture -eq $Architecture) { $Architecture } else { "${HostArchitecture}_${Architecture}" }

    Write-Detail "vcvarsall $argument"

    $captured = cmd /c "`"$vcvarsall`" $argument >nul 2>&1 && set"
    if (-not $captured) {
        throw "vcvarsall.bat $argument produced no environment. The $Architecture toolset may not be installed."
    }

    foreach ($line in $captured) {
        if ($line -match '^([^=]+)=(.*)$') {
            Set-Item -Path "env:$($Matches[1])" -Value $Matches[2] -ErrorAction SilentlyContinue
        }
    }

    # vcvarsall does not add vswhere, and the compiler shells out to it while
    # locating the linker.
    $env:PATH = "$installer;$env:PATH"

    $link = Get-Command link.exe -ErrorAction SilentlyContinue
    if (-not $link) {
        throw "link.exe is still not on PATH after loading the $Architecture environment."
    }

    Write-Detail "link.exe $($link.Source)"
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "==> Compiling Recode.Shell for $Architecture" -ForegroundColor Cyan

Import-MsvcEnvironment

$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

$publishDirectory = Join-Path $RepoRoot ".native-build\shell\$Architecture"

& $dotnet publish (Join-Path $RepoRoot 'src\Recode.Shell\Recode.Shell.csproj') `
    -c Release -r "win-$Architecture" -o $publishDirectory `
    -v minimal --nologo | Out-Host

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$dll = Join-Path $publishDirectory 'Recode.Shell.dll'
if (-not (Test-Path $dll)) {
    throw "Recode.Shell.dll was not produced in $publishDirectory."
}

New-Item -ItemType Directory -Force -Path $Output | Out-Null
Copy-Item $dll $Output -Force

$size = [math]::Round((Get-Item $dll).Length / 1KB)
Write-Host ""
Write-Host "Wrote $(Join-Path $Output 'Recode.Shell.dll') ($size KB, native $Architecture)"
