<#
.SYNOPSIS
    Adds the Recode entries to the Explorer context menu for the current user.

.DESCRIPTION
    Writes to HKCU\Software\Classes\SystemFileAssociations, so no administrator
    rights are needed and nothing outside the current user account is touched.

    The keys are not written from a list kept in this script. They come from
    recode.exe --emit-registry, which builds them from formats.json. Adding a
    format to the table therefore adds it to the menu, and this script does not
    need to change.

    On Windows 11 the entries appear under "Show more options". Reaching the
    top level of the Windows 11 menu needs a packaged shell extension, which is
    installed separately by tools/install-shell-extension.ps1.

.PARAMETER ExecutablePath
    Full path to recode.exe. Defaults to a published build next to this
    repository, then to recode.exe beside this script.

.PARAMETER WhatIf
    Show what would be written without writing anything.

.EXAMPLE
    pwsh -File tools/install-context-menu.ps1
    pwsh -File tools/install-context-menu.ps1 -ExecutablePath "C:\Tools\Recode\recode.exe"
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $ExecutablePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$PSNativeCommandUseErrorActionPreference = $false

function Resolve-Executable([string] $Requested) {
    if ($Requested) {
        if (-not (Test-Path $Requested)) {
            throw "recode.exe was not found at $Requested."
        }
        return (Resolve-Path $Requested).Path
    }

    $architecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }

    $candidates = @(
        # An unpacked release, where tools sits beside the executable.
        (Join-Path $PSScriptRoot '..\recode.exe'),

        # A working copy, after dotnet publish.
        (Join-Path $PSScriptRoot "..\dist\$architecture\recode.exe"),

        # A working copy, after dotnet build.
        (Join-Path $PSScriptRoot "..\src\Recode.App\bin\Release\net8.0-windows\win-$architecture\recode.exe"),
        (Join-Path $PSScriptRoot '..\src\Recode.App\bin\Release\net8.0-windows\recode.exe')
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw @'
recode.exe was not found.

Publish it first:
  dotnet publish src/Recode.App -c Release -r win-arm64 -o dist

Or pass the path directly:
  pwsh -File tools/install-context-menu.ps1 -ExecutablePath C:\path\to\recode.exe
'@
}

# recode.exe is a Windows application rather than a console application, so
# PowerShell does not wait for it and never sets $LASTEXITCODE. Start-Process
# with -Wait is the only way to get both the output and a trustworthy exit code.
function Get-RegistryPlan([string] $Executable) {
    $stdout = [System.IO.Path]::GetTempFileName()
    $stderr = [System.IO.Path]::GetTempFileName()

    try {
        $process = Start-Process -FilePath $Executable `
            -ArgumentList '--emit-registry' `
            -Wait -PassThru -NoNewWindow `
            -RedirectStandardOutput $stdout `
            -RedirectStandardError $stderr

        $json = Get-Content $stdout -Raw
        $errors = Get-Content $stderr -Raw

        if ($process.ExitCode -ne 0) {
            throw "recode.exe --emit-registry failed with exit code $($process.ExitCode).`n$errors"
        }

        if ([string]::IsNullOrWhiteSpace($json)) {
            throw "recode.exe --emit-registry produced no output.`n$errors"
        }

        try {
            return ($json | ConvertFrom-Json)
        }
        catch {
            throw "recode.exe --emit-registry did not return JSON:`n$json"
        }
    }
    finally {
        Remove-Item $stdout, $stderr -Force -ErrorAction SilentlyContinue
    }
}

function ConvertTo-PSDrivePath([string] $Path) {
    # The plan uses HKCU\... , which PowerShell needs as HKCU:\...
    return $Path -replace '^HKCU\\', 'HKCU:\'
}

function Write-Key($Key) {
    $path = ConvertTo-PSDrivePath $Key.path

    if (-not (Test-Path $path)) {
        New-Item -Path $path -Force | Out-Null
    }

    foreach ($value in $Key.values) {
        $name = if ([string]::IsNullOrEmpty($value.name)) { '(default)' } else { $value.name }
        $type = if ($value.kind -eq 'Dword') { 'DWord' } else { 'String' }
        $data = if ($type -eq 'DWord') { [int]$value.value } else { [string]$value.value }

        New-ItemProperty -Path $path -Name $name -Value $data -PropertyType $type -Force | Out-Null
    }
}

# Explorer caches the menu. Without this the entries only turn up after a
# restart of explorer.exe, which looks like the installer did nothing.
function Update-Shell {
    $signature = @'
using System;
using System.Runtime.InteropServices;
public static class ShellNotify
{
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    public static void AssocChanged()
    {
        // SHCNE_ASSOCCHANGED with SHCNF_IDLIST
        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
    }
}
'@
    try {
        if (-not ('ShellNotify' -as [type])) {
            Add-Type -TypeDefinition $signature -Language CSharp
        }
        [ShellNotify]::AssocChanged()
    }
    catch {
        Write-Warning "Could not notify Explorer. Restart explorer.exe or sign out to see the menu."
    }
}

# ---------------------------------------------------------------------------

$executable = Resolve-Executable $ExecutablePath
Write-Host "Using $executable"

$plan = Get-RegistryPlan $executable
$keyCount = @($plan.keys).Count
$rootCount = @($plan.rootKeys).Count

Write-Host "Writing $keyCount keys across $rootCount file extensions"

if ($PSCmdlet.ShouldProcess("HKCU\Software\Classes", "Write $keyCount context menu keys")) {
    foreach ($key in $plan.keys) {
        Write-Key $key
    }

    Update-Shell

    Write-Host ""
    Write-Host "Done. Right click an image, choose 'Show more options', then 'Convert to'."
    Write-Host "Remove it again with tools/uninstall-context-menu.ps1"
} else {
    foreach ($key in $plan.keys) {
        Write-Host "  $($key.path)"
    }
}
