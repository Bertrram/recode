<#
.SYNOPSIS
    Builds the native image codec libraries that Recode loads at run time.

.DESCRIPTION
    Recode ships no binaries in source control. This script produces them.

    It builds, per architecture:

      libwebp    WebP encode and decode                     BSD-3-Clause
      libde265   HEVC decode, used by libheif               LGPL-3.0
      kvazaar    HEVC encode, used by libheif               BSD-3-Clause
      aom        AV1 encode and decode, used by libheif     BSD-2-Clause
      libheif    HEIC, HEIF and AVIF container handling     LGPL-3.0

    libwebp, libde265 and aom come from vcpkg. kvazaar and libheif are built
    directly from source because vcpkg has no kvazaar port, and because the
    vcpkg libheif port enables x265 by default.

    x265 is deliberately not used. It is GPL-2.0-or-later, and linking it would
    force the whole project to GPL. kvazaar is BSD licensed and produces HEVC
    that HEIC readers accept. This script passes -DWITH_X265=OFF explicitly and
    verifies afterwards that no x265 binary ended up in the output.

    Output lands in native/<arch>/ and is consumed by src/Recode.App at publish
    time. native/ is in .gitignore.

.PARAMETER Architecture
    arm64, x64, or all. Defaults to all.

.PARAMETER VcpkgRoot
    Where vcpkg lives. Defaults to $env:VCPKG_ROOT, otherwise a private clone
    under .native-build/vcpkg. Cloned automatically if absent.

.PARAMETER SkipVcpkg
    Reuse an existing vcpkg install tree instead of running vcpkg install again.
    Useful when iterating on the kvazaar or libheif build.

.PARAMETER Clean
    Delete build trees for the selected architectures before building.

.EXAMPLE
    pwsh -File tools/build-natives.ps1
    pwsh -File tools/build-natives.ps1 -Architecture arm64
    pwsh -File tools/build-natives.ps1 -Architecture x64 -SkipVcpkg
#>
[CmdletBinding()]
param(
    [ValidateSet('arm64', 'x64', 'all')]
    [string] $Architecture = 'all',

    [string] $VcpkgRoot,

    [switch] $SkipVcpkg,

    [switch] $Clean
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# PowerShell 7.4 and later turn a non-zero exit code from a native command into
# a terminating error. This script checks $LASTEXITCODE itself so that failures
# carry a useful message, so that behaviour is switched off here.
$PSNativeCommandUseErrorActionPreference = $false

# Pinned upstream versions. Bump deliberately, not incidentally.
$LibheifTag = 'v1.23.1'
$KvazaarTag = 'v2.3.1'

$RepoRoot  = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$WorkRoot  = Join-Path $RepoRoot '.native-build'
$OutRoot   = Join-Path $RepoRoot 'native'

function Write-Stage([string] $Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Detail([string] $Message) {
    Write-Host "    $Message" -ForegroundColor DarkGray
}

function Invoke-Checked([string] $Description, [scriptblock] $Action) {
    # Out-Host keeps tool output on the console instead of letting it leak into
    # the return value of whichever function called this.
    & $Action | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

# ---------------------------------------------------------------------------
# Toolchain discovery
# ---------------------------------------------------------------------------

function Find-VisualStudio {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        throw @'
Visual Studio Build Tools were not found.

Install them with:
  winget install --id Microsoft.VisualStudio.2022.BuildTools -e --override "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 --add Microsoft.VisualStudio.Component.VC.Tools.ARM64 --add Microsoft.VisualStudio.Component.Windows11SDK.22621 --add Microsoft.VisualStudio.Component.VC.CMake.Project"
'@
    }

    $path = & $vswhere -latest -products * `
        -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
        -property installationPath

    if ([string]::IsNullOrWhiteSpace($path)) {
        throw 'Visual Studio Build Tools are installed but the C++ toolset is missing. Rerun the installer with the VCTools workload.'
    }
    return $path.Trim()
}

function Find-CMake([string] $VsPath) {
    $bundled = Join-Path $VsPath 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
    if (Test-Path $bundled) { return $bundled }

    $onPath = Get-Command cmake -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    throw 'cmake.exe was not found. Install the "C++ CMake tools for Windows" component of Visual Studio Build Tools.'
}

# ---------------------------------------------------------------------------
# vcpkg
# ---------------------------------------------------------------------------

function Initialize-Vcpkg {
    if ([string]::IsNullOrWhiteSpace($script:VcpkgRoot)) {
        if ($env:VCPKG_ROOT -and (Test-Path $env:VCPKG_ROOT)) {
            $script:VcpkgRoot = $env:VCPKG_ROOT
        } else {
            $script:VcpkgRoot = Join-Path $WorkRoot 'vcpkg'
        }
    }

    if (-not (Test-Path (Join-Path $script:VcpkgRoot '.git'))) {
        Write-Detail "Cloning vcpkg into $($script:VcpkgRoot)"
        New-Item -ItemType Directory -Force -Path (Split-Path $script:VcpkgRoot) | Out-Null
        Invoke-Checked 'git clone vcpkg' { git clone --depth 1 https://github.com/microsoft/vcpkg.git $script:VcpkgRoot }
    }

    $exe = Join-Path $script:VcpkgRoot 'vcpkg.exe'
    if (-not (Test-Path $exe)) {
        Write-Detail 'Bootstrapping vcpkg'
        Invoke-Checked 'bootstrap-vcpkg' { & (Join-Path $script:VcpkgRoot 'bootstrap-vcpkg.bat') -disableMetrics }
    }
    return $exe
}

function Install-VcpkgPorts([string] $VcpkgExe, [string] $Triplet, [string] $HostTriplet) {
    # libheif is deliberately absent here. Its vcpkg port turns on x265 by
    # default, so it is built from source further down instead.
    $ports = @('libwebp', 'libde265', 'aom')

    Write-Detail "vcpkg install $($ports -join ' ') --triplet $Triplet"
    Invoke-Checked 'vcpkg install' {
        & $VcpkgExe install @ports `
            --triplet $Triplet `
            --host-triplet $HostTriplet `
            --disable-metrics `
            --clean-after-build
    }
}

# ---------------------------------------------------------------------------
# Source checkouts
# ---------------------------------------------------------------------------

function Get-Source([string] $Name, [string] $Url, [string] $Tag) {
    $dir = Join-Path (Join-Path $WorkRoot 'src') $Name
    if (Test-Path (Join-Path $dir '.git')) {
        $current = (& git -C $dir describe --tags --exact-match 2>$null | Select-Object -First 1)
        if ($LASTEXITCODE -eq 0 -and $current -eq $Tag) {
            Write-Detail "$Name $Tag already checked out"
            return $dir
        }
        Write-Detail "Removing stale $Name checkout"
        Remove-Item -Recurse -Force $dir
    }

    New-Item -ItemType Directory -Force -Path (Split-Path $dir) | Out-Null
    Write-Detail "Cloning $Name $Tag"
    Invoke-Checked "git clone $Name" {
        git clone --depth 1 --branch $Tag --recurse-submodules $Url $dir
    }
    return $dir
}

# ---------------------------------------------------------------------------
# CMake builds
# ---------------------------------------------------------------------------

function Invoke-CMakeBuild {
    param(
        [string]   $CMake,
        [string]   $Name,
        [string]   $SourceDir,
        [string]   $BuildDir,
        [string]   $Prefix,
        [string]   $VsArch,
        [string[]] $Options
    )

    Write-Detail "Configuring $Name"
    New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null

    $configure = @(
        '-S', $SourceDir,
        '-B', $BuildDir,
        '-G', 'Visual Studio 17 2022',
        '-A', $VsArch,
        "-DCMAKE_INSTALL_PREFIX=$Prefix",
        '-DCMAKE_BUILD_TYPE=Release',
        '-DBUILD_SHARED_LIBS=ON'
    ) + $Options

    Invoke-Checked "cmake configure $Name" { & $CMake @configure }

    Write-Detail "Building $Name"
    Invoke-Checked "cmake build $Name" {
        & $CMake --build $BuildDir --config Release --parallel
    }

    Write-Detail "Installing $Name into $Prefix"
    Invoke-Checked "cmake install $Name" {
        & $CMake --install $BuildDir --config Release
    }
}

function Build-Kvazaar([string] $CMake, [string] $Prefix, [string] $VsArch, [string] $ArchDir) {
    $src   = Get-Source 'kvazaar' 'https://github.com/ultravideo/kvazaar.git' $KvazaarTag
    $build = Join-Path (Join-Path $WorkRoot 'build') "kvazaar-$ArchDir"

    # BUILD_TESTS pulls in the 'greatest' submodule and a test runner that is of
    # no use here. On ARM64 the AVX2 strategy files compile to empty translation
    # units, and MSVC only warns about the /arch:AVX2 flag kvazaar sets.
    Invoke-CMakeBuild -CMake $CMake -Name 'kvazaar' `
        -SourceDir $src -BuildDir $build -Prefix $Prefix -VsArch $VsArch `
        -Options @(
            '-DBUILD_TESTS=OFF',
            '-DUSE_CRYPTO=OFF',
            '-DGIT_SUBMODULE=OFF'
        )
}

function Build-Libheif {
    param(
        [string] $CMake,
        [string] $Prefix,
        [string] $VsArch,
        [string] $ArchDir,
        [string] $VcpkgInstalled
    )

    $src   = Get-Source 'libheif' 'https://github.com/strukturag/libheif.git' $LibheifTag
    $build = Join-Path (Join-Path $WorkRoot 'build') "libheif-$ArchDir"

    $kvazaarInclude = Join-Path $Prefix 'include'
    $kvazaarLib     = Join-Path $Prefix 'lib\kvazaar.lib'
    if (-not (Test-Path $kvazaarLib)) {
        throw "kvazaar import library not found at $kvazaarLib. The kvazaar build did not produce what libheif needs."
    }

    # ENABLE_PLUGIN_LOADING=OFF keeps every codec compiled into heif.dll, so the
    # shipped folder is one DLL per library rather than a plugin directory.
    #
    # WITH_X265=OFF is the important line. It is ON upstream by default.
    Invoke-CMakeBuild -CMake $CMake -Name 'libheif' `
        -SourceDir $src -BuildDir $build -Prefix $Prefix -VsArch $VsArch `
        -Options @(
            "-DCMAKE_PREFIX_PATH=$VcpkgInstalled;$Prefix",
            '-DWITH_X265=OFF',
            '-DWITH_X264=OFF',
            '-DWITH_KVAZAAR=ON',
            '-DWITH_KVAZAAR_PLUGIN=OFF',
            "-DKVAZAAR_INCLUDE_DIR=$kvazaarInclude",
            "-DKVAZAAR_LIBRARY=$kvazaarLib",
            '-DWITH_LIBDE265=ON',
            '-DWITH_LIBDE265_PLUGIN=OFF',
            '-DWITH_AOM_ENCODER=ON',
            '-DWITH_AOM_DECODER=ON',
            '-DWITH_AOM_ENCODER_PLUGIN=OFF',
            '-DWITH_AOM_DECODER_PLUGIN=OFF',
            '-DWITH_DAV1D=OFF',
            '-DWITH_SvtEnc=OFF',
            '-DWITH_RAV1E=OFF',
            '-DWITH_JPEG_DECODER=OFF',
            '-DWITH_JPEG_ENCODER=OFF',
            '-DWITH_OpenJPEG_DECODER=OFF',
            '-DWITH_OpenJPEG_ENCODER=OFF',
            '-DWITH_OPENJPH_ENCODER=OFF',
            '-DWITH_OpenH264_DECODER=OFF',
            '-DWITH_FFMPEG_DECODER=OFF',
            '-DWITH_UVG266=OFF',
            '-DWITH_VVDEC=OFF',
            '-DWITH_VVENC=OFF',
            '-DWITH_LIBSHARPYUV=OFF',
            '-DWITH_UNCOMPRESSED_CODEC=OFF',
            '-DWITH_HEADER_COMPRESSION=OFF',
            '-DWITH_EXAMPLES=OFF',
            '-DWITH_GDK_PIXBUF=OFF',
            '-DWITH_REDUCED_VISIBILITY=OFF',
            '-DENABLE_PLUGIN_LOADING=OFF',
            '-DBUILD_TESTING=OFF',
            '-DCMAKE_COMPILE_WARNING_AS_ERROR=OFF'
        )
}

# ---------------------------------------------------------------------------
# Output assembly
# ---------------------------------------------------------------------------

function Copy-Outputs([string] $Prefix, [string] $VcpkgInstalled, [string] $ArchDir) {
    $dest = Join-Path $OutRoot $ArchDir
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Get-ChildItem $dest -Filter *.dll -ErrorAction SilentlyContinue | Remove-Item -Force

    # Curated list. Anything missing is a build failure, not something to ignore.
    $wanted = @(
        @{ Name = 'heif.dll';        From = (Join-Path $Prefix 'bin') },
        @{ Name = 'kvazaar.dll';     From = (Join-Path $Prefix 'bin') },
        @{ Name = 'libde265.dll';    From = (Join-Path $VcpkgInstalled 'bin') },
        @{ Name = 'aom.dll';         From = (Join-Path $VcpkgInstalled 'bin') },
        @{ Name = 'libwebp.dll';     From = (Join-Path $VcpkgInstalled 'bin') },
        @{ Name = 'libsharpyuv.dll'; From = (Join-Path $VcpkgInstalled 'bin') }
    )

    $missing = @()
    foreach ($item in $wanted) {
        $source = Join-Path $item.From $item.Name
        if (Test-Path $source) {
            Copy-Item $source $dest -Force
            $size = [math]::Round((Get-Item $source).Length / 1KB)
            Write-Detail ("{0,-18} {1,6} KB" -f $item.Name, $size)
        } else {
            $missing += $item.Name
        }
    }

    if ($missing.Count -gt 0) {
        throw "These libraries were not produced: $($missing -join ', '). Check the build log above."
    }

    # The whole point of avoiding x265 is defeated if a copy sneaks in.
    $forbidden = Get-ChildItem $dest -Filter '*x265*' -ErrorAction SilentlyContinue
    if ($forbidden) {
        throw "An x265 binary was found in $dest. x265 is GPL and must not be shipped. Remove it and rebuild."
    }

    $manifest = [ordered]@{
        architecture = $ArchDir
        builtOn      = (Get-Date).ToString('yyyy-MM-dd')
        libheif      = $LibheifTag
        kvazaar      = $KvazaarTag
        note         = 'Built by tools/build-natives.ps1. Not tracked in git.'
        libraries    = ($wanted | ForEach-Object { $_.Name })
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $dest 'manifest.json') -Encoding UTF8
}

# ---------------------------------------------------------------------------
# Per architecture driver
# ---------------------------------------------------------------------------

function Build-Architecture {
    param(
        [string] $ArchDir,
        [string] $VcpkgExe,
        [string] $CMake,
        [string] $HostTriplet
    )

    $vsArch  = if ($ArchDir -eq 'arm64') { 'ARM64' } else { 'x64' }
    $triplet = "$ArchDir-windows"
    $prefix  = Join-Path (Join-Path $WorkRoot 'prefix') $ArchDir

    Write-Stage "Building native libraries for $ArchDir"

    if ($Clean) {
        foreach ($stale in @(
            (Join-Path (Join-Path $WorkRoot 'build') "kvazaar-$ArchDir"),
            (Join-Path (Join-Path $WorkRoot 'build') "libheif-$ArchDir"),
            $prefix)) {
            if (Test-Path $stale) {
                Write-Detail "Removing $stale"
                Remove-Item -Recurse -Force $stale
            }
        }
    }

    New-Item -ItemType Directory -Force -Path $prefix | Out-Null

    $vcpkgInstalled = Join-Path (Join-Path $VcpkgRoot 'installed') $triplet
    if (-not $SkipVcpkg) {
        Install-VcpkgPorts -VcpkgExe $VcpkgExe -Triplet $triplet -HostTriplet $HostTriplet
    }
    if (-not (Test-Path $vcpkgInstalled)) {
        throw "vcpkg has nothing installed for $triplet. Rerun without -SkipVcpkg."
    }

    Build-Kvazaar -CMake $CMake -Prefix $prefix -VsArch $vsArch -ArchDir $ArchDir
    Build-Libheif -CMake $CMake -Prefix $prefix -VsArch $vsArch -ArchDir $ArchDir -VcpkgInstalled $vcpkgInstalled

    Write-Stage "Collecting $ArchDir output"
    Copy-Outputs -Prefix $prefix -VcpkgInstalled $vcpkgInstalled -ArchDir $ArchDir
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

$targets = if ($Architecture -eq 'all') { @('arm64', 'x64') } else { @($Architecture) }

Write-Stage 'Locating toolchain'
$vs = Find-VisualStudio
Write-Detail "Visual Studio: $vs"
$cmake = Find-CMake $vs
Write-Detail "CMake: $cmake"

$hostArch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
$hostTriplet = "$hostArch-windows"
Write-Detail "Host: $hostArch"

Write-Stage 'Preparing vcpkg'
$vcpkgExe = Initialize-Vcpkg
Write-Detail "vcpkg: $vcpkgExe"

New-Item -ItemType Directory -Force -Path $WorkRoot, $OutRoot | Out-Null

foreach ($arch in $targets) {
    Build-Architecture -ArchDir $arch -VcpkgExe $vcpkgExe -CMake $cmake -HostTriplet $hostTriplet
}

Write-Stage 'Done'
foreach ($arch in $targets) {
    $dir = Join-Path $OutRoot $arch
    Write-Host "  native/$arch"
    Get-ChildItem $dir -Filter *.dll | ForEach-Object {
        Write-Host ("    {0,-18} {1,7:N0} KB" -f $_.Name, ($_.Length / 1KB))
    }
}
Write-Host ''
Write-Host 'These files are gitignored. Rerun this script on a fresh clone.' -ForegroundColor DarkGray
