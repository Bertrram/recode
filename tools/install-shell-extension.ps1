<#
.SYNOPSIS
    Registers the packaged shell extension, putting Recode in the first
    Windows 11 context menu.

.DESCRIPTION
    This is the one part of Recode that needs administrator rights, and it is
    worth being clear about why.

    Reaching the first Windows 11 menu requires a COM handler declared by a
    packaged application. Windows will only register a package that is signed by
    a certificate the machine trusts. Recode is signed with a self signed
    certificate, so that certificate has to be added to the machine's trusted
    store, and writing to a machine wide certificate store is an administrator
    operation. It cannot be done per user.

    If that is not a trade you want to make, do not run this. The classic menu
    installed by tools/install-context-menu.ps1 needs no certificate, no package
    and no elevation. It lives under "Show more options" and does the same job.

    What this script changes:
      1. Adds one certificate to Local Machine\Trusted People
      2. Registers the Recode package, pointing at the folder holding the
         executable

    Both are undone by -Uninstall.

.PARAMETER PayloadDirectory
    Folder holding recode.exe and Recode.Shell.dll. This folder must stay where
    it is: the package points at it rather than copying from it.

.PARAMETER Uninstall
    Remove the package and the certificate.

.EXAMPLE
    pwsh -File tools/install-shell-extension.ps1
    pwsh -File tools/install-shell-extension.ps1 -Uninstall
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('arm64', 'x64')]
    [string] $Architecture,

    [string] $PayloadDirectory,

    [string] $PackagePath,

    [string] $CertificatePath,

    [switch] $Uninstall
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$PSNativeCommandUseErrorActionPreference = $false

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
# The Identity Name from AppxManifest.xml, which is what Get-AppxPackage -Name
# matches on. Not the package family name, which carries a publisher hash.
$PackageIdentityName = 'BertramBechLarsen.Recode'

if (-not $Architecture) {
    $Architecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
}
# Two layouts. An unpacked release has everything in one folder with tools
# beside it. A working copy has it under dist\<architecture>. Whichever holds
# recode.exe is the one meant.
$Distribution = Test-Path (Join-Path $RepoRoot 'recode.exe')

if (-not $PayloadDirectory) {
    $PayloadDirectory = if ($Distribution) { $RepoRoot } else { Join-Path $RepoRoot "dist\$Architecture" }
}
if (-not $PackagePath) {
    $PackagePath = if ($Distribution) {
        Join-Path $RepoRoot "Recode-$Architecture.msix"
    } else {
        Join-Path $RepoRoot "dist\Recode-$Architecture.msix"
    }
}
if (-not $CertificatePath) {
    $CertificatePath = if ($Distribution) {
        Join-Path $RepoRoot 'Recode.cer'
    } else {
        Join-Path $RepoRoot 'dist\Recode.cer'
    }
}

function Write-Stage([string] $Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Detail([string] $Message) {
    Write-Host "    $Message" -ForegroundColor DarkGray
}

function Test-Elevated {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Elevated)) {
    throw @"
This script needs administrator rights, because trusting a certificate is a
machine wide change.

Open PowerShell as administrator and run it again:
  pwsh -File "$PSCommandPath"$(if ($Uninstall) { ' -Uninstall' })

If you would rather not elevate, use the classic menu instead. It needs no
certificate and no elevation:
  pwsh -File "$(Join-Path $PSScriptRoot 'install-context-menu.ps1')"
"@
}

# ---------------------------------------------------------------------------
# Uninstall
# ---------------------------------------------------------------------------

if ($Uninstall) {
    Write-Stage 'Removing the packaged shell extension'

    $installed = Get-AppxPackage -Name $PackageIdentityName -ErrorAction SilentlyContinue
    if ($installed) {
        foreach ($package in $installed) {
            if ($PSCmdlet.ShouldProcess($package.PackageFullName, 'Remove package')) {
                Remove-AppxPackage -Package $package.PackageFullName
                Write-Detail "Removed $($package.PackageFullName)"
            }
        }
    } else {
        Write-Detail 'The package was not registered.'
    }

    $certificates = Get-ChildItem Cert:\LocalMachine\TrustedPeople -ErrorAction SilentlyContinue |
        Where-Object { $_.FriendlyName -eq 'Recode package signing' -or $_.Subject -like '*Recode*' }

    if ($certificates) {
        foreach ($certificate in $certificates) {
            if ($PSCmdlet.ShouldProcess($certificate.Thumbprint, 'Remove certificate')) {
                Remove-Item $certificate.PSPath -Force
                Write-Detail "Removed certificate $($certificate.Thumbprint)"
            }
        }
    } else {
        Write-Detail 'No Recode certificate was in the trusted store.'
    }

    Write-Host ''
    Write-Host 'Done. The classic menu, if installed, is untouched.'
    return
}

# ---------------------------------------------------------------------------
# Install
# ---------------------------------------------------------------------------

foreach ($required in @($PackagePath, $CertificatePath)) {
    if (-not (Test-Path $required)) {
        throw @"
$required is missing.

Build the package first:
  pwsh -File tools\build-package.ps1 -Architecture $Architecture
"@
    }
}

foreach ($required in @('recode.exe', 'Recode.Shell.dll')) {
    if (-not (Test-Path (Join-Path $PayloadDirectory $required))) {
        throw "$required is missing from $PayloadDirectory."
    }
}

$PayloadDirectory = (Resolve-Path $PayloadDirectory).Path

Write-Stage 'Trusting the signing certificate'
Write-Detail "From $CertificatePath"

if ($PSCmdlet.ShouldProcess('LocalMachine\TrustedPeople', 'Import certificate')) {
    $imported = Import-Certificate -FilePath $CertificatePath -CertStoreLocation Cert:\LocalMachine\TrustedPeople
    $imported | ForEach-Object { Write-Detail "Trusted $($_.Thumbprint)  $($_.Subject)" }
}

Write-Stage 'Registering the package'
Write-Detail "Package  $PackagePath"
Write-Detail "Payload  $PayloadDirectory"

if ($PSCmdlet.ShouldProcess($PackagePath, 'Register package')) {
    # ExternalLocation is what makes this a sparse installation: the package
    # holds the manifest, the folder holds the binaries, and the folder has to
    # stay where it is.
    Add-AppxPackage -Path $PackagePath -ExternalLocation $PayloadDirectory
}

$installed = Get-AppxPackage -Name $PackageIdentityName -ErrorAction SilentlyContinue

Write-Stage 'Done'
if ($installed) {
    Write-Host "  $($installed.PackageFullName)"
    Write-Host ""
    Write-Host "Right click an image. 'Convert to' is now in the first menu."
    Write-Host "Explorer caches menus, so restart it if nothing changed:" -ForegroundColor DarkGray
    Write-Host "  Stop-Process -Name explorer" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "The folder $PayloadDirectory must stay where it is." -ForegroundColor DarkGray
    Write-Host "Remove everything again with -Uninstall." -ForegroundColor DarkGray
} else {
    Write-Warning 'The package does not appear to be registered. Check the output above.'
}
