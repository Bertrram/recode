# Third party licenses

Recode ships five native libraries alongside `recode.exe`. This folder holds
their licence texts. Recode's own source is MIT, in [LICENSE](../LICENSE) at the
root of the repository.

| Library | Licence | What it does |
|---|---|---|
| [libheif](https://github.com/strukturag/libheif) | LGPL-3.0-only | HEIC, HEIF and AVIF container handling |
| [libde265](https://github.com/strukturag/libde265) | LGPL-3.0-only | HEVC decoding |
| [kvazaar](https://github.com/ultravideo/kvazaar) | BSD-3-Clause | HEVC encoding |
| [libaom](https://aomedia.googlesource.com/aom) | BSD-2-Clause and others | AV1 encoding and decoding |
| [libwebp](https://chromium.googlesource.com/webm/libwebp) | BSD-3-Clause | WebP encoding and decoding |

JPEG, PNG, BMP, TIFF and GIF need no bundled library. They go through the
Windows Imaging Component, which is part of Windows.

## How the LGPL libraries are handled

libheif and libde265 are LGPL-3.0. Recode's own code is MIT and stays MIT. That
combination works because of how the libraries are linked:

- They ship as separate DLLs next to `recode.exe`, not linked into it.
- Recode loads them at run time with `LoadLibrary`, resolving each function it
  needs by name. There is no import table entry for `heif.dll` anywhere in
  `recode.exe`.
- A user can replace `heif.dll` and `libde265.dll` with their own builds and
  Recode will use them, provided the exported functions Recode calls are still
  present. The list of those functions is in
  [`HeifNative.cs`](../src/Recode.Core/Codecs/HeifNative.cs).
- [`tools/build-natives.ps1`](../tools/build-natives.ps1) reproduces the exact
  binaries that ship, including the upstream versions used.

This is why `ExcludeFromSingleFile` is set on the native libraries in
`Recode.App.csproj`. Bundling them into the single file executable would remove
the ability to replace them.

## Why kvazaar and not x265

Both encode HEVC, and libheif supports either. x265 is GPL-2.0-or-later, and
linking it would require Recode to be GPL as well. kvazaar is BSD-3-Clause and
carries no such requirement.

`tools/build-natives.ps1` passes `-DWITH_X265=OFF` explicitly, because upstream
libheif enables x265 by default, and the vcpkg port lists it as a default
feature. The script also fails the build if an x265 binary is found in the
output folder.

The build log records the result. From the libheif configuration step:

```
libde265 HEVC decoder                 : + built-in
Kvazaar HEVC encoder                  : + built-in
x265 HEVC encoder                     : - disabled
AOM AV1 decoder                       : + built-in
AOM AV1 encoder                       : + built-in
```

## Patents

Licences cover copyright. They do not cover patents, and the two are separate
questions.

HEVC, which is what HEIC files contain, is covered by patent pools including
Access Advance and Via LA. Recode provides HEVC support the way other open
source projects do, by building on kvazaar and libde265, and makes no claim to
grant any patent licence. Whether a patent licence is needed for a given use is
a question for the user, not something this project can answer.

AV1, which is what AVIF files contain, was designed to be royalty free. The
Alliance for Open Media operates a patent licence covering it. libaom carries an
additional patent grant, included in `libaom-LICENSE.txt`.

JPEG, PNG, BMP, GIF and WebP have no known active patent encumbrance.

## Files

| File | Source |
|---|---|
| `libheif-LICENSE.txt` | `COPYING` from libheif v1.23.1 |
| `libde265-LICENSE.txt` | vcpkg copyright file for libde265 1.1.1 |
| `kvazaar-LICENSE.txt` | `LICENSE` from kvazaar v2.3.1 |
| `libaom-LICENSE.txt` | vcpkg copyright file for aom 3.14.1 |
| `libwebp-LICENSE.txt` | vcpkg copyright file for libwebp |
