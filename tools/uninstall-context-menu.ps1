<#
.SYNOPSIS
    Removes the Recode entries from the Explorer context menu.

.DESCRIPTION
    Deletes every key Recode owns under
    HKCU\Software\Classes\SystemFileAssociations and leaves nothing behind.

    The keys are found by walking the registry rather than by asking recode.exe
    what it would have written. That matters: uninstalling has to work after the
    executable has already been deleted, and it has to catch keys left by an
    older version whose format table listed extensions the current one does not.

.PARAMETER WhatIf
    List what would be removed without removing anything.

.EXAMPLE
    pwsh -File tools/uninstall-context-menu.ps1
    pwsh -File tools/uninstall-context-menu.ps1 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$AssociationsRoot = 'HKCU:\Software\Classes\SystemFileAssociations'

# Everything this project writes is named Recode.something, so one prefix finds
# all of it without needing a record of what was installed.
$KeyPrefix = 'Recode.'

if (-not (Test-Path $AssociationsRoot)) {
    Write-Host "Nothing to remove: $AssociationsRoot does not exist."
    return
}

$found = [System.Collections.Generic.List[string]]::new()

foreach ($extension in Get-ChildItem $AssociationsRoot -ErrorAction SilentlyContinue) {
    $shellPath = Join-Path $extension.PSPath 'shell'
    if (-not (Test-Path $shellPath)) { continue }

    foreach ($verb in Get-ChildItem $shellPath -ErrorAction SilentlyContinue) {
        if ($verb.PSChildName.StartsWith($KeyPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            $found.Add($verb.PSPath)
        }
    }
}

if ($found.Count -eq 0) {
    Write-Host "Nothing to remove. No Recode keys found under $AssociationsRoot."
    return
}

Write-Host "Found $($found.Count) Recode keys."

foreach ($path in $found) {
    $display = $path -replace '^Microsoft\.PowerShell\.Core\\Registry::', ''

    if ($PSCmdlet.ShouldProcess($display, 'Remove registry key')) {
        Remove-Item -Path $path -Recurse -Force
    } else {
        Write-Host "  $display"
    }
}

if (-not $PSCmdlet.ShouldProcess('', '')) {
    return
}

# Extensions that now have an empty shell key were created by the installer and
# would otherwise be left behind as clutter. Only empty ones are removed, so a
# key another program uses is never touched.
foreach ($extension in Get-ChildItem $AssociationsRoot -ErrorAction SilentlyContinue) {
    $shellPath = Join-Path $extension.PSPath 'shell'

    if ((Test-Path $shellPath) -and -not (Get-ChildItem $shellPath -ErrorAction SilentlyContinue)) {
        Remove-Item $shellPath -Force -ErrorAction SilentlyContinue
    }

    if (-not (Get-ChildItem $extension.PSPath -ErrorAction SilentlyContinue) -and
        -not (Get-ItemProperty $extension.PSPath -ErrorAction SilentlyContinue |
              Get-Member -MemberType NoteProperty |
              Where-Object { $_.Name -notmatch '^PS' })) {
        Remove-Item $extension.PSPath -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "Removed. The menu entries disappear as soon as Explorer refreshes."
