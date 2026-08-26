# Recode

Convert images between formats from the Explorer context menu on Windows.

Right click one or more image files, pick a target format, and the converted
files appear next to the originals. The originals are never deleted.

Everything needed is included. There is nothing to download from the Microsoft
Store, no codec pack to install, and no account to create. That is the point of
the program.

## The problem

Windows opens HEIC photographs from an iPhone only if you install a codec from
the Microsoft Store. It cannot write HEIC at all. It cannot read or write AVIF
without another Store download. WebP support arrived late and is incomplete.

Meanwhile the everyday task, turning one image into another format, needs a web
uploader, a large editor, or a command line tool that has to be found and
configured first.

Recode does that one task. One executable, one menu, no background service, no
tray icon, no telemetry, no settings to get wrong.

## Install

Download the archive for your architecture from the
[releases page](https://github.com/Bertrram/recode/releases), unpack it anywhere,
and run:

```powershell
pwsh -File tools\install-context-menu.ps1
```

That writes to `HKEY_CURRENT_USER`. **No administrator rights are required.**
Nothing is written outside your own user account, no service is installed, and
nothing runs at startup.

To remove it:

```powershell
pwsh -File tools\uninstall-context-menu.ps1
```

The uninstaller finds its keys by walking the registry rather than by reading a
list, so it also cleans up after older versions.

On Windows 11 the entries appear under **Show more options**. Reaching the top
level of the Windows 11 menu requires a packaged shell extension, which is
tracked separately.

The menu looks like this:

```
Convert to  >   JPG
                JPEG
                PNG
                BMP
                TIF
                TIFF
                GIF
                HEIF
                AVIF
                WebP
                ----------------
                Format support
```

The source format is left out of its own submenu. Selecting several files at
once works, including files of different formats.

<!--
  To replace the block above with a real screenshot, save one as
  docs/context-menu.png and put this line here instead:
  ![The context menu](docs/context-menu.png)
-->


### Unsigned binaries

Recode is not code signed. Two consequences:

Windows SmartScreen shows a warning the first time you run a downloaded copy.
Choose "More info" then "Run anyway", or unblock the file in its properties.

If your machine enforces the Defender rule "Block executable files from running
unless they meet a prevalence, age, or trusted list criterion", Recode will be
blocked with an access denied error. That is a policy decision by whoever
manages the machine. Ask them for an exclusion rather than working around it.

## Use

The context menu covers the common case. The command line covers the rest.

```
recode --to png photo.heic
recode --to jpg --quality 92 *.heic
recode --to webp --outdir converted image1.png image2.png
recode --list
recode --about
```

| Flag | Meaning |
|---|---|
| `--to <format>` | Target format. Use the extension, for example `png` or `jpg`. |
| `--quality <1-100>` | Applies to JPEG, WebP, HEIC and AVIF. Default 85. Out of range values are clamped. |
| `--force` | Replace an existing file instead of writing a numbered copy. |
| `--outdir <path>` | Write results to this folder instead of beside each input. Created if missing. |
| `--list` | Print the format table and which backend handles each entry. |
| `--about` | Open the support window. |
| `--help` | Print usage. |

Behaviour worth knowing:

- Several files at once is fine, and they may be of different input formats.
- If the target file already exists, `name (1).png` is written, then `(2)`, and
  so on. `--force` overwrites instead.
- EXIF orientation is applied to the pixels, so a photograph taken sideways
  comes out the right way up even in a format with nowhere to store an
  orientation tag.
- Transparency is composited onto white when the target format has no alpha
  channel.
- Converting to the same format is allowed as a recompression. Without
  `--force` it writes a numbered copy, so the original survives either way.
- A file that cannot be decoded is reported and skipped, and the rest of the
  batch continues. The exit code is 0 when everything succeeded and 1 when
  anything did not.

## Formats

| Format | Extensions | Read | Write | Backend |
|---|---|---|---|---|
| JPEG | `.jpg` `.jpeg` | yes | yes | WIC |
| PNG | `.png` | yes | yes | WIC |
| BMP | `.bmp` | yes | yes | WIC |
| TIFF | `.tif` `.tiff` | yes | yes | WIC |
| GIF | `.gif` | yes | yes | WIC |
| HEIC | `.heic` | yes | yes | libheif with libde265 and kvazaar |
| HEIF | `.heif` | yes | yes | libheif with libde265 and kvazaar |
| AVIF | `.avif` | yes | yes | libheif with libaom |
| WebP | `.webp` | yes | yes | libwebp |

Every readable format converts to every writable format. JPEG and TIFF each
appear twice in the menu, once per extension, because the extension you pick is
the extension you get.

WIC is the Windows Imaging Component, part of Windows itself, so five of the
nine formats work even if every bundled file is deleted. The rest come from
libraries shipped alongside the executable.

The table above is generated from [`formats.json`](formats.json), which is the
single source of truth. The context menu, the `--list` output and the support
window all read it. Adding a format means editing that file, not hunting
through the code.

## Licences and patents

Recode's own source is MIT. See [LICENSE](LICENSE).

The bundled libraries keep their own licences, collected in
[third-party-licenses](third-party-licenses/), along with a longer explanation
of how the LGPL libraries are handled.

The short version: libheif and libde265 are LGPL-3.0 and ship as separate DLLs
that Recode loads at run time and that you can replace. kvazaar, libaom and
libwebp are BSD licensed. x265 is deliberately not used, because it is GPL and
linking it would require this project to be GPL as well.

On patents, plainly:

**HEVC, which is what HEIC files contain, is covered by patent pools** including
Access Advance and Via LA. Recode provides HEVC support the way other open
source projects do, by building on kvazaar and libde265. It grants no patent
licence and makes no claim about whether you need one.

**AVIF is royalty free.** AV1 was designed that way, and the Alliance for Open
Media operates a patent licence covering it.

JPEG, PNG, BMP, GIF and WebP have no known active patent encumbrance.

## Limitations

An honest list of what this version does not do.

**Windows 11 menu placement.** The entries sit under "Show more options", not in
the first menu. The top level requires an `IExplorerCommand` handler in a
packaged application, which is more machinery than a registry key.

**Metadata is not carried over.** EXIF orientation is applied to the pixels, but
nothing else survives the conversion. Capture date, camera model, GPS location,
ICC colour profiles and XMP are all dropped. If you need them, this is not the
tool yet.

**Colour management is ignored.** Images are treated as sRGB. A photograph in
Display P3 or Adobe RGB will convert without complaint and the colours will
shift.

**One frame only.** Animated GIF and WebP convert their first frame. Multi page
TIFF converts the first page. Nothing warns you about the frames that were
dropped.

**Whole images are held in memory.** A conversion allocates roughly four bytes
per pixel, twice over during the encode. A 100 megapixel scan will want more
than a gigabyte. Ordinary photographs are nowhere near this.

**GIF and BMP output loses transparency.** Both are marked as having no alpha
channel, so transparency is flattened onto white. GIF's single transparent
colour index and BMP's rarely honoured alpha are more trouble than they are
worth.

**The shell prompt returns before the output does.** Recode is built as a
Windows application so that context menu conversions do not flash a console
window. The cost is that `cmd` and PowerShell do not wait for it, so in a
terminal the prompt reappears and the output arrives after it. Scripts that need
the exit code should use `Start-Process -Wait`.

**Large selections may be split.** The menu asks Explorer to hand all selected
files to one invocation. Explorer does not always oblige for very large
selections, in which case it starts several conversions instead. The result is
the same, it is just less efficient.

**No progress display.** A batch of large HEIC files takes time and shows
nothing while it works. Only failures produce a window.

## Building

Requirements:

- Windows 10 or 11, x64 or ARM64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio Build Tools 2022 with the C++ workload, needed only for the
  native libraries

```powershell
winget install --id Microsoft.DotNet.SDK.8 -e
```

### Native libraries

The repository contains no binaries. `native/` is in `.gitignore` and is
produced from source:

```powershell
pwsh -File tools\build-natives.ps1
```

This clones vcpkg if needed, builds libwebp, libde265 and libaom through it,
then builds kvazaar and libheif directly from pinned source tags. The first run
takes a while. Afterwards the output lands in `native/arm64` and `native/x64`.

The script passes `-DWITH_X265=OFF` explicitly, because upstream libheif enables
x265 by default, and it fails the build if an x265 binary appears in the output.
That check is the reason the licensing claims above can be made with a straight
face.

Build one architecture only with `-Architecture arm64` or `-Architecture x64`.

### Application

```powershell
pwsh -File tools\make-icon.ps1
dotnet publish src\Recode.App -c Release -r win-arm64 --self-contained -o dist\arm64
```

Use `win-x64` for the other architecture. The published folder holds
`recode.exe` and the six native DLLs, which is everything needed to run.

`tools\make-icon.ps1` renders `assets/Recode.svg` and `assets/Recode-small.svg`
into a multi resolution `app.ico`. The simplified variant is used at 16, 20 and
24 pixels, where the full mark has more detail than there are pixels to draw it
with. Explorer draws context menu entries at 16, so that variant is the one most
people see.

### Tests

```powershell
dotnet test
```

The unit tests need nothing bundled: every native call sits behind an interface
and is exercised with a stub, including the case where a DLL is missing. The end
to end tests use real images and do need `tools\build-natives.ps1` to have run
first.

## Layout

```
formats.json               the format table, single source of truth
src/Recode.Core            format table, codecs, conversion, registry generation
src/Recode.App             command line and the support window
tests/Recode.Tests         xUnit tests and small real test images
tools/build-natives.ps1    builds the bundled libraries from source
tools/make-icon.ps1        renders the SVG logos into app.ico
tools/install-context-menu.ps1
tools/uninstall-context-menu.ps1
third-party-licenses/      licences for the bundled libraries
```

Backends sit behind a common `IImageCodec`. The conversion logic does not know
whether a file is going through WIC, libheif or libwebp, which is what keeps
adding a format to a table change rather than a code change.

## Licence

MIT. See [LICENSE](LICENSE).
