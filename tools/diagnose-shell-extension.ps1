<#
.SYNOPSIS
    Works out why the Windows 11 context menu entry is not appearing.

.DESCRIPTION
    A packaged context menu extension fails silently. Explorer shows no entry,
    logs nothing useful, and gives no indication of which of the half dozen
    preconditions was not met. This script checks them one at a time and says
    which one is wrong.

    It changes nothing. Run it as an ordinary user; a couple of checks report
    less detail without administrator rights and say so.

.EXAMPLE
    pwsh -File tools/diagnose-shell-extension.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('arm64', 'x64')]
    [string] $Architecture,

    # The folder the package was installed from. Only needed when it is not the
    # default, because Windows does not report it back.
    [string] $PayloadDirectory
)

$ErrorActionPreference = 'Continue'
Set-StrictMode -Version Latest
$PSNativeCommandUseErrorActionPreference = $false

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$PackageIdentityName = 'BertramBechLarsen.Recode'
$Clsid = '018E5409-E5B6-4961-8779-67741A425A20'

if (-not $Architecture) {
    $Architecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
}
if (-not $PayloadDirectory) {
    $PayloadDirectory = Join-Path $RepoRoot "dist\$Architecture"
}

$problems = [System.Collections.Generic.List[string]]::new()

function Report {
    param([bool] $Ok, [string] $Check, [string] $Detail, [string] $Fix)

    $mark = if ($Ok) { 'ok  ' } else { 'FAIL' }
    $colour = if ($Ok) { 'Green' } else { 'Red' }

    Write-Host "  [$mark] " -ForegroundColor $colour -NoNewline
    Write-Host $Check
    if ($Detail) { Write-Host "         $Detail" -ForegroundColor DarkGray }

    if (-not $Ok -and $Fix) { $problems.Add($Fix) }
}

Write-Host ""
Write-Host "Recode shell extension diagnostics" -ForegroundColor Cyan
Write-Host ""

# ---------------------------------------------------------------------------

$build = [Environment]::OSVersion.Version.Build
Report -Ok ($build -ge 22000) `
    -Check "Windows 11 or later" `
    -Detail "build $build" `
    -Fix "The first context menu does not exist before Windows 11 build 22000. Use tools\install-context-menu.ps1 instead."

# ---------------------------------------------------------------------------

$package = Get-AppxPackage -Name $PackageIdentityName -ErrorAction SilentlyContinue

Report -Ok ($null -ne $package) `
    -Check "Package registered" `
    -Detail $(if ($package) { $package.PackageFullName } else { "no package named $PackageIdentityName" }) `
    -Fix "Run tools\install-shell-extension.ps1 from an elevated PowerShell."

if ($package) {
    $architectureMatches = $package.Architecture -in @($Architecture, 'Neutral')
    Report -Ok $architectureMatches `
        -Check "Package architecture matches this machine" `
        -Detail "package is $($package.Architecture), machine is $Architecture" `
        -Fix "Build and install the $Architecture package: tools\build-package.ps1 -Architecture $Architecture"

    Report -Ok ($package.Status -eq 'Ok') `
        -Check "Package status" `
        -Detail "$($package.Status)" `
        -Fix "The package is registered but not healthy. Reinstall it with -Uninstall then install again."
}

# ---------------------------------------------------------------------------

# Get-AppxPackage reports InstallLocation, which for a sparse package is the
# manifest folder under WindowsApps rather than the folder holding the
# binaries. The external location is not exposed at all, so this checks the
# folder the package was installed from. Pass -PayloadDirectory if it was
# installed from somewhere else.
$payloadOk = (Test-Path (Join-Path $PayloadDirectory 'recode.exe')) -and
             (Test-Path (Join-Path $PayloadDirectory 'Recode.Shell.dll'))

Report -Ok $payloadOk `
    -Check "Payload present where it is expected" `
    -Detail $PayloadDirectory `
    -Fix "recode.exe or Recode.Shell.dll is missing from $PayloadDirectory. If the package was installed from a different folder, pass -PayloadDirectory. Otherwise rebuild the payload."

# ---------------------------------------------------------------------------

$trusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -like '*Bertram*' -or $_.FriendlyName -eq 'Recode package signing' }

Report -Ok ($null -ne $trusted) `
    -Check "Signing certificate trusted machine wide" `
    -Detail $(if ($trusted) { ($trusted | ForEach-Object { $_.Thumbprint }) -join ', ' } else { 'nothing matching in LocalMachine\TrustedPeople' }) `
    -Fix "Import the certificate: tools\install-shell-extension.ps1 from an elevated PowerShell."

$msix = Join-Path $RepoRoot "dist\Recode-$Architecture.msix"
if (Test-Path $msix) {
    $signature = Get-AuthenticodeSignature $msix
    Report -Ok ($signature.Status -eq 'Valid') `
        -Check "Package signature validates" `
        -Detail "$($signature.Status), $($signature.SignerCertificate.Subject)" `
        -Fix "The signature does not chain to a trusted certificate yet. That is expected until the certificate is imported."
}

# ---------------------------------------------------------------------------
# The decisive check. If the object can be created, the package registration,
# the surrogate and the DLL all work, and any remaining problem is in Explorer
# rather than in the extension.
# ---------------------------------------------------------------------------

$created = $false
$creationDetail = ''

try {
    $type = [Type]::GetTypeFromCLSID([Guid] $Clsid, $false)
    if ($null -eq $type) {
        $creationDetail = 'the CLSID does not resolve to a registered class'
    } else {
        $instance = [Activator]::CreateInstance($type)
        if ($instance) {
            $created = $true
            $creationDetail = 'the surrogate started and returned an object'
            [void][Runtime.InteropServices.Marshal]::ReleaseComObject($instance)
        }
    }
}
catch {
    $creationDetail = $_.Exception.Message
}

# REGDB_E_CLASSNOTREG means the registration never happened, which the package
# check above has already explained. Any other failure means the registration
# is there but the server will not start, which is a different problem with a
# different fix.
$notRegistered = $creationDetail -match '80040154|REGDB_E_CLASSNOTREG'

$creationFix = if ($notRegistered) {
    "The class is not registered, which follows from the package not being installed. Fix that first and run this again."
} else {
    "The class is registered but the server will not start. Usual causes: Recode.Shell.dll is built for a different architecture than Windows, or the CLSID in AppxManifest.xml does not match the one in src/Recode.Shell/ShellIds.cs."
}

Report -Ok $created `
    -Check "COM object can be created" `
    -Detail $creationDetail `
    -Fix $creationFix

# ---------------------------------------------------------------------------

Write-Host ""

if ($problems.Count -eq 0) {
    Write-Host "Everything checks out." -ForegroundColor Green
    Write-Host ""
    Write-Host "If the entry is still missing, Explorer is showing a cached menu:" -ForegroundColor DarkGray
    Write-Host "  Stop-Process -Name explorer" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Also check that you are right clicking a file type Recode handles." -ForegroundColor DarkGray
    Write-Host "The entry hides itself for anything it cannot convert." -ForegroundColor DarkGray
} else {
    Write-Host "What to do:" -ForegroundColor Yellow
    $index = 1
    foreach ($problem in $problems) {
        Write-Host "  $index. $problem"
        $index++
    }
}

Write-Host ""
