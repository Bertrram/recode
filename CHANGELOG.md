# Changelog

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
