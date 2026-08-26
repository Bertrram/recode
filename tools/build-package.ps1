<#
.SYNOPSIS
    Builds and signs the sparse MSIX package that puts Recode in the first
    Windows 11 context menu.

.DESCRIPTION
    The classic registry menu needs none of this. It works today, on Windows 10
    and 11, with no package and no administrator rights, and it is installed by
    tools/install-context-menu.ps1.

    Reaching the first menu on Windows 11 is a different mechanism entirely. It
    requires a COM handler declared by a packaged application, which means an
    MSIX package, which means a signature. There is no registry key for it.

    This script produces that package. It does not install anything. Installing
    is tools/install-shell-extension.ps1, which needs administrator rights
    because trusting a certificate is a machine wide decision.

    Sparse package means the .msix carries only the manifest and the logo
    images. recode.exe, Recode.Shell.dll and the native libraries stay in an
    ordinary folder, named at install time as the external location.

.PARAMETER Architecture
    arm64 or x64. Defaults to the machine's own.

.PARAMETER PayloadDirectory
    The folder holding recode.exe and Recode.Shell.dll. Defaults to
    dist/<architecture>.

.PARAMETER CertificateSubject
    Subject of the signing certificate. Must match the Publisher in the
    manifest, which this script keeps in step automatically.

.PARAMETER Version
    Package version, four parts. Defaults to 0.2.0.0.

.EXAMPLE
    pwsh -File tools/build-package.ps1
    pwsh -File tools/build-package.ps1 -Architecture x64
#>
[CmdletBinding()]
param(
    [ValidateSet('arm64', 'x64')]
    [string] $Architecture,

    [string] $PayloadDirectory,

    [string] $CertificateSubject = 'CN=Bertram Bech Larsen',

    [string] $PublisherDisplayName = 'Bertram Bech Larsen',

    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string] $Version = '0.2.0.0'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$PSNativeCommandUseErrorActionPreference = $false

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if (-not $Architecture) {
    $Architecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
}
if (-not $PayloadDirectory) {
    $PayloadDirectory = Join-Path $RepoRoot "dist\$Architecture"
}

$BuildRoot   = Join-Path $RepoRoot ".native-build\package\$Architecture"
$LayoutRoot  = Join-Path $BuildRoot 'layout'
$OutputMsix  = Join-Path $RepoRoot "dist\Recode-$Architecture.msix"
$OutputCer   = Join-Path $RepoRoot 'dist\Recode.cer'

# Must match ShellIds.ConvertCommandClsidText. Checked below rather than
# trusted, because a mismatch produces no menu and no error message anywhere.
$Clsid = '018E5409-E5B6-4961-8779-67741A425A20'

function Write-Stage([string] $Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Detail([string] $Message) {
    Write-Host "    $Message" -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# Toolchain
# ---------------------------------------------------------------------------

function Find-SdkTool([string] $Name) {
    $root = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (-not (Test-Path $root)) {
        throw "The Windows SDK was not found. Install Visual Studio Build Tools with the Windows 11 SDK component."
    }

    $hostArch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }

    $candidates = Get-ChildItem $root -Directory |
        Where-Object { $_.Name -match '^10\.' } |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName "$hostArch\$Name" }

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) { return $candidate }
    }

    throw "$Name was not found under $root."
}

# ---------------------------------------------------------------------------
# Consistency checks
# ---------------------------------------------------------------------------

function Assert-ClsidMatchesSource {
    $source = Join-Path $RepoRoot 'src\Recode.Shell\ShellIds.cs'
    if (-not (Test-Path $source)) { return }

    $text = Get-Content $source -Raw
    if ($text -notmatch [regex]::Escape($Clsid)) {
        throw @"
The CLSID in this script does not appear in src/Recode.Shell/ShellIds.cs.

Manifest: $Clsid

They have to be identical. When they are not, Explorer shows no menu and
reports nothing, which is a long afternoon.
"@
    }

    Write-Detail "CLSID matches ShellIds.cs"
}

function Assert-Payload {
    foreach ($required in @('recode.exe', 'Recode.Shell.dll')) {
        $path = Join-Path $PayloadDirectory $required
        if (-not (Test-Path $path)) {
            throw @"
$required is missing from $PayloadDirectory.

Publish both first:
  dotnet publish src/Recode.App -c Release -r win-$Architecture --self-contained -o dist/$Architecture
  pwsh -File tools/build-shell.ps1 -Architecture $Architecture
"@
        }
    }

    Write-Detail "Payload found in $PayloadDirectory"
}

# ---------------------------------------------------------------------------
# Manifest
# ---------------------------------------------------------------------------

function Get-ReadableExtensions {
    $exe = Join-Path $PayloadDirectory 'recode.exe'
    $stdout = [System.IO.Path]::GetTempFileName()
    $stderr = [System.IO.Path]::GetTempFileName()

    try {
        # recode.exe is a Windows application, so PowerShell will not wait for
        # it and will not set $LASTEXITCODE. Start-Process is the only reliable
        # way to get both the output and the exit code.
        $process = Start-Process -FilePath $exe -ArgumentList '--emit-extensions' `
            -Wait -PassThru -NoNewWindow `
            -RedirectStandardOutput $stdout -RedirectStandardError $stderr

        if ($process.ExitCode -ne 0) {
            throw "recode.exe --emit-extensions failed: $(Get-Content $stderr -Raw)"
        }

        $extensions = Get-Content $stdout | Where-Object { $_.Trim() } | ForEach-Object { $_.Trim() }

        if (-not $extensions) {
            throw "recode.exe --emit-extensions produced nothing."
        }

        return , @($extensions)
    }
    finally {
        Remove-Item $stdout, $stderr -Force -ErrorAction SilentlyContinue
    }
}

function New-FileTypeXml([string[]] $Extensions) {
    $indent = '            '
    $lines = foreach ($extension in $Extensions) {
        "$indent<desktop5:ItemType Type=`"$extension`">"
        "$indent  <desktop5:Verb Id=`"Recode`" Clsid=`"$Clsid`" />"
        "$indent</desktop5:ItemType>"
    }
    return ($lines -join "`r`n")
}

function New-Manifest([string[]] $Extensions) {
    $template = Get-Content (Join-Path $RepoRoot 'packaging\AppxManifest.template.xml') -Raw

    $manifest = $template.
        Replace('{{IDENTITY_NAME}}', 'BertramBechLarsen.Recode').
        Replace('{{PUBLISHER}}', $CertificateSubject).
        Replace('{{PUBLISHER_DISPLAY_NAME}}', $PublisherDisplayName).
        Replace('{{VERSION}}', $Version).
        Replace('{{ARCHITECTURE}}', $Architecture).
        Replace('{{CLSID}}', $Clsid).
        Replace('{{FILE_TYPES}}', (New-FileTypeXml $Extensions))

    $path = Join-Path $LayoutRoot 'AppxManifest.xml'
    Set-Content -Path $path -Value $manifest -Encoding UTF8

    # Parsing it here turns a malformed manifest into a clear error rather than
    # an opaque makeappx failure.
    [xml](Get-Content $path -Raw) | Out-Null

    return $path
}

# ---------------------------------------------------------------------------
# Certificate
# ---------------------------------------------------------------------------

function Get-SigningCertificate {
    $existing = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $CertificateSubject -and $_.HasPrivateKey } |
        Where-Object { $_.NotAfter -gt (Get-Date) } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if ($existing) {
        Write-Detail "Reusing certificate $($existing.Thumbprint)"
        return $existing
    }

    Write-Detail "Creating a self signed certificate for $CertificateSubject"

    # Code signing EKU (1.3.6.1.5.5.7.3.3) and a basic constraints extension.
    # Both are required for a certificate that will sign an MSIX package.
    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $CertificateSubject `
        -KeyUsage DigitalSignature `
        -FriendlyName 'Recode package signing' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -NotAfter (Get-Date).AddYears(3) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')

    Write-Detail "Created $($certificate.Thumbprint)"
    return $certificate
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

Write-Stage "Building the sparse package for $Architecture"

Assert-ClsidMatchesSource
Assert-Payload

if (Test-Path $LayoutRoot) { Remove-Item $LayoutRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $LayoutRoot 'Assets') | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path $OutputMsix) | Out-Null

Write-Stage 'Rendering package logos'
& (Get-Process -Id $PID).Path -NoProfile -File (Join-Path $PSScriptRoot 'make-icon.ps1') `
    -PackageAssets (Join-Path $LayoutRoot 'Assets') | Out-Host
if ($LASTEXITCODE -ne 0) { throw "make-icon.ps1 failed." }

Write-Stage 'Writing the manifest'
$extensions = Get-ReadableExtensions
Write-Detail "File types from formats.json: $($extensions -join ' ')"
$manifestPath = New-Manifest $extensions
Write-Detail "Wrote $manifestPath"

Write-Stage 'Packing'
$makeappx = Find-SdkTool 'makeappx.exe'
Write-Detail $makeappx
if (Test-Path $OutputMsix) { Remove-Item $OutputMsix -Force }

# /nv skips validation, which otherwise objects to a package whose payload is
# not inside it. That is exactly what a sparse package is.
& $makeappx pack /d $LayoutRoot /p $OutputMsix /nv | Out-Host
if ($LASTEXITCODE -ne 0) { throw "makeappx failed with exit code $LASTEXITCODE." }

Write-Stage 'Signing'
$certificate = Get-SigningCertificate
$signtool = Find-SdkTool 'signtool.exe'
Write-Detail $signtool

& $signtool sign /fd SHA256 /sha1 $certificate.Thumbprint $OutputMsix | Out-Host
if ($LASTEXITCODE -ne 0) { throw "signtool failed with exit code $LASTEXITCODE." }

Export-Certificate -Cert $certificate -FilePath $OutputCer -Force | Out-Null

Write-Stage 'Done'
Write-Host "  package     $OutputMsix ($([math]::Round((Get-Item $OutputMsix).Length / 1KB)) KB)"
Write-Host "  certificate $OutputCer"
Write-Host ""
Write-Host "Install it with, in an elevated PowerShell:" -ForegroundColor DarkGray
Write-Host "  pwsh -File tools\install-shell-extension.ps1" -ForegroundColor DarkGray
