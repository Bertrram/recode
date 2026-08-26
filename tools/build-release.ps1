<#
.SYNOPSIS
    Assembles the archives published on the releases page.

.DESCRIPTION
    Takes what dotnet publish, build-shell.ps1 and build-package.ps1 produced
    and arranges it the way somebody downloading it expects: one folder, the
    executable at the top, the scripts that install it in tools beside it.

    That layout is why the install scripts look for recode.exe in two places.
    In a working copy it is under dist\<architecture>; in an unpacked release it
    is next to the tools folder. Both are handled.

    Build tools are deliberately left out. Nobody downloading a release needs
    the script that compiles libheif from source.

.PARAMETER Architecture
    arm64, x64, or both. Defaults to both.

.PARAMETER Version
    Used in the archive name. Read from Directory.Build.props when not given.

.EXAMPLE
    pwsh -File tools/build-release.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('arm64', 'x64', 'both')]
    [string] $Architecture = 'both',

    [string] $Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$StagingRoot = Join-Path $RepoRoot '.native-build\release'

if (-not $Version) {
    $props = Get-Content (Join-Path $RepoRoot 'Directory.Build.props') -Raw
    if ($props -match '<Version>([^<]+)</Version>') {
        $Version = $Matches[1]
    } else {
        throw 'Could not read <Version> from Directory.Build.props. Pass -Version instead.'
    }
}

# What a user needs, and nothing else. The build scripts stay behind.
$ScriptsToShip = @(
    'install-context-menu.ps1',
    'uninstall-context-menu.ps1',
    'install-shell-extension.ps1',
    'diagnose-shell-extension.ps1'
)

function Write-Stage([string] $Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Detail([string] $Message) {
    Write-Host "    $Message" -ForegroundColor DarkGray
}

function Build-Archive([string] $Arch) {
    $payload = Join-Path $RepoRoot "dist\$Arch"
    $msix = Join-Path $RepoRoot "dist\Recode-$Arch.msix"
    $certificate = Join-Path $RepoRoot 'dist\Recode.cer'

    foreach ($required in @($payload, $msix, $certificate)) {
        if (-not (Test-Path $required)) {
            throw @"
$required is missing.

Build everything for $Arch first:
  dotnet publish src/Recode.App -c Release -r win-$Arch --self-contained -o dist/$Arch
  pwsh -File tools/build-shell.ps1 -Architecture $Arch
  pwsh -File tools/build-package.ps1 -Architecture $Arch
"@
        }
    }

    foreach ($required in @('recode.exe', 'Recode.Shell.dll', 'heif.dll', 'libwebp.dll')) {
        if (-not (Test-Path (Join-Path $payload $required))) {
            throw "$required is missing from $payload."
        }
    }

    $staging = Join-Path $StagingRoot "Recode-$Version-$Arch"
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
    New-Item -ItemType Directory -Force -Path (Join-Path $staging 'tools') | Out-Null

    Copy-Item (Join-Path $payload '*') $staging -Recurse -Force
    Copy-Item $msix $staging -Force
    Copy-Item $certificate $staging -Force

    foreach ($script in $ScriptsToShip) {
        Copy-Item (Join-Path $PSScriptRoot $script) (Join-Path $staging 'tools') -Force
    }

    Copy-Item (Join-Path $RepoRoot 'third-party-licenses') $staging -Recurse -Force
    foreach ($document in @('LICENSE', 'README.md', 'CHANGELOG.md')) {
        Copy-Item (Join-Path $RepoRoot $document) $staging -Force
    }

    # A published .exe carries no signature, and Windows will have marked the
    # download as coming from the internet. Saying so here saves the first
    # confused issue.
    $notice = @"
Recode $Version, $Arch

Two menus, pick one. Both are described in README.md.

  Classic menu, under "Show more options" on Windows 11, no elevation:
    pwsh -File tools\install-context-menu.ps1

  First Windows 11 menu, needs administrator rights once:
    pwsh -File tools\install-shell-extension.ps1

Keep this folder where you put it. The packaged menu points at it rather
than copying from it, so moving the folder breaks the menu.

These files are not code signed. Windows may show a SmartScreen warning
the first time, and may mark the downloaded archive as blocked. Unblock it
before unpacking:

  Unblock-File .\Recode-$Version-$Arch.zip

If the packaged menu does not appear:
  pwsh -File tools\diagnose-shell-extension.ps1
"@
    Set-Content (Join-Path $staging 'INSTALL.txt') -Value $notice -Encoding UTF8

    $archive = Join-Path $RepoRoot "dist\Recode-$Version-$Arch.zip"
    if (Test-Path $archive) { Remove-Item $archive -Force }

    Write-Detail "Compressing $((Get-ChildItem $staging -Recurse -File).Count) files"
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $archive -CompressionLevel Optimal

    $size = [math]::Round((Get-Item $archive).Length / 1MB, 1)
    Write-Host ("  {0,-34} {1,7} MB" -f (Split-Path $archive -Leaf), $size)

    return $archive
}

$targets = if ($Architecture -eq 'both') { @('arm64', 'x64') } else { @($Architecture) }

Write-Stage "Building release archives for Recode $Version"
New-Item -ItemType Directory -Force -Path $StagingRoot | Out-Null

$archives = foreach ($arch in $targets) {
    Build-Archive $arch
}

Write-Stage 'Done'
foreach ($archive in $archives) {
    Write-Host "  $archive"
}
