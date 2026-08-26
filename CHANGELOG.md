# Changelog

## 0.2.0

Recode now appears in the first Windows 11 context menu, next to Paint and the
other packaged entries, instead of only under "Show more options".

- `Recode.Shell` implements `IExplorerCommand` and `IEnumExplorerCommand` and is
  compiled ahead of time into a native DLL. Explorer loads it through a COM
  surrogate, so no runtime has to start before the menu can be drawn, and a
  fault costs a process nobody sees rather than the desktop.
- A sparse MSIX package declares the handler. It carries the manifest and the
  logos; the binaries stay in an ordinary folder named at install time.
- The manifest's file type list is generated from `formats.json`, like the
  registry keys, so the two menus cannot offer different formats.
- `tools/diagnose-shell-extension.ps1` reports which precondition failed when
  the entry does not appear. A packaged extension otherwise fails silently.
- The format table moved to its own `Recode.Formats` assembly. The extension and
  the converter need the same table, but only one of them can afford WPF.
- Entries hide themselves rather than grey out. Right clicking a text file
  offers nothing, and a folder of PNG files is not offered a conversion to PNG.

Installing the packaged menu needs administrator rights once, because a self
signed package has to be trusted machine wide. The classic registry menu still
needs neither a certificate nor elevation, works on Windows 10, and is
unaffected by any of this. Either menu works on its own.

## 0.1.0

First release.

- Convert between JPEG, PNG, BMP, TIFF, GIF, HEIC, HEIF, AVIF and WebP, every
  readable format to every writable one.
- Explorer context menu, installed per user under `HKEY_CURRENT_USER` with no
  administrator rights. Appears under "Show more options" on Windows 11.
- Command line: `--to`, `--quality`, `--force`, `--outdir`, `--list`, `--about`.
- Support window listing every format, whether it can be read and written, and
  which backend handles it. Names the missing file if a bundled library will
  not load.
- JPEG, PNG, BMP, TIFF and GIF go through the Windows Imaging Component and
  need nothing bundled. HEIC, HEIF and AVIF go through libheif, WebP through
  libwebp, both shipped as replaceable DLLs.
- HEVC encoding uses kvazaar, not x265, so the project stays MIT. The build
  script disables x265 explicitly and fails if an x265 binary appears in the
  output.
- Originals are never deleted. An existing target file produces a numbered copy
  unless `--force` is given.
- EXIF orientation is applied to the pixels. Transparency is flattened onto
  white for formats without an alpha channel.
- A file that cannot be decoded is reported and skipped without stopping the
  batch. Exit code 0 when everything succeeded, 1 otherwise.
- Builds for x64 and ARM64, self contained, no installer.
