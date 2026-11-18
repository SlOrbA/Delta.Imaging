# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.4.0] - 2026-11-17

### Added
- Releases include `Delta.Wsq-<version>.zip`: `Delta.Wsq.dll`, stamped with the version of the release tag, with its PDB and XML documentation, together with `nbis32.dll` and `nbis64.dll`. Until now the library had to be built from source and reported version 1.1.0.0 regardless of the release (#24)
- CI builds every project in the repository (`Delta.Wsq.sln` in all four configurations, `Delta.ImageRenameTool.sln`) and runs the tests as a 32-bit process in addition to 64-bit (#23)

### Fixed
- `Delta.Wsq/build.cmd` works again: it builds the solution with `msbuild` instead of `dotnet build`, which cannot build the native project (#23)
- The nbis DLL validates WSQ data before handing it to NBIS: marker segments must lie inside the buffer, the tables, frame header and first block header must have the structure NBIS assumes, a stream to decode must contain all tables before the first block, and the image must be at least 33 x 33 pixels. Truncated or damaged headers previously made NBIS free memory twice or access memory outside its buffers, after which a later decode of a valid file could crash the process (#14)
- Images smaller than 33 x 33 pixels are rejected: `WsqCodec.Encode` throws `ArgumentException` (see `WsqCodec.Constants.MinImageSize`), `Decode` returns an empty image. The NBIS wavelet code reads and writes outside its buffers for such images, and the data it produced could not be decoded (#14)
- `wsq_get_comments` no longer leaks the comments it had collected when the stream turns out to be malformed (#20)
- The MinGW stdio shims in the nbis DLL forward to the real CRT functions (`vsscanf`, `vfscanf`, `vfprintf`, ...) instead of emulating them by reading 20 arguments that were never passed (#21)
- `WsqCodec.Encode` validates its input instead of letting the native encoder read past the pixel buffer: dimensions must be positive, the depth 8 bpp, `Data` at least `Width * Height` bytes (`ArgumentException`) and the bit rate a positive finite number (`ArgumentOutOfRangeException`) (#15)
- Calls into the nbis DLL are serialized. NBIS keeps codec state in global variables, so concurrent `Encode` / `Decode` / `GetComments` calls could corrupt each other or crash (#16)
- Debug builds of `Delta.Wsq` load `nbis32.dll` / `nbis64.dll` like Release builds, instead of `nbis32d.dll` / `nbis64d.dll`, which are neither published as CI artifacts nor shipped. `dotnet test` now works in the default configuration. The Debug configurations of `nbis.vcxproj` produce `nbis32.dll` / `nbis64.dll` as well (#17)
- Image resolution is rounded instead of truncated when converting GDI+ / WPF images: a 500 dpi PNG (stored as 499.9998 dpi) is no longer written as 499 ppi (#18)
- `Conversions.To8bppBitmap`: 8 bpp images with a non-grayscale palette are converted through their palette colors instead of using the palette indexes as gray levels, and the returned bitmap always has a grayscale palette (#19)
- 32-bit processes: the P/Invoke declarations in `NativeMethods` now use `CallingConvention.Cdecl`, matching the `__cdecl` exports of the nbis DLL. The default (StdCall) left the arguments on the stack after every call, reported by the debugger as `PInvokeStackImbalance`
- 64-bit processes: `Conversions.To8bppBitmap` (used by `WsqEncoder.EncodeGdi` with `autoConvertToGrayscale`) no longer casts bitmap pointers to `int`, which threw `OverflowException` on .NET Framework and crashed with an access violation on .NET when the bitmap buffer was above 2 GB
- `Conversions.GdiImageToImageInfo` / `ImageInfoToGdiImage` (and therefore `WsqEncoder.EncodeGdi` / `WsqDecoder.DecodeGdi`) threw `EntryPointNotFoundException`: they P/Invoked `CopyMemory` from `kernel32.dll`, which is only a macro in the Windows headers and not an exported function. Rows are now copied with `Buffer.MemoryCopy`

### Changed
- `Conversions.WpfImageToImageInfo` and `ToGray8BitmapSource` convert `Indexed8` images through their palette colors instead of using the palette indexes as gray levels (#30)
- A process that is neither x86 nor x64 (e.g. ARM64) gets a `PlatformNotSupportedException` that says so, instead of a `BadImageFormatException` from loading the x64 DLL (#25)
- Null arguments are reported with `ArgumentNullException` (a subclass of the `ArgumentException` thrown before) (#28)
- Delta.ImageRenameTool: selecting another image while a preview is loading no longer calls `Thread.Abort`, which throws on .NET (#28)
- Delta.ImageRenameTool: a preview that is still loading is dropped when the preview is switched off or the window is closed

### Removed
- `IMPROVEMENTS.md` and `FUTURE_IMPROVEMENTS.md`: mostly completed work; planning now happens in GitHub issues (#27)

## [1.3.3] - 2026-11-16

### Changed
- nbis DLL no longer prints NBIS debug/progress messages to stderr on every encode/decode (`int debug = 0` in `mingw_shim.cpp`). Console applications and services hosting the DLL see less output; genuine NBIS error messages are still printed (#11)
- CI fails if NBIS debug output reappears (#12)

## [1.3.2] - 2026-11-16

### Fixed
- `Debug|Win32` build of `nbis.vcxproj`: the Debug configurations use the static debug CRT (`/MTd`) like Release (`/MT`). The build failed with `LNK2019: unresolved external symbol _sqrtf` (#10)
- Test coverage is collected (Coverlet, OpenCover) and reported to SonarCloud (#9)

### Changed
- Fail-early CI pipeline (`ci.yml`): lint, DLL build, then tests and SonarCloud, one build per commit (#9)
- All four configurations of `nbis.vcxproj` are built in CI (#10)
- Release assets are built and attached by `release.yml` (#9)

## [1.3.1] - 2026-11-16

### Fixed
- The NBIS static libraries in `Delta.Wsq/nbis/lib32` and `lib64` were still the 2013 MinGW GCC 4.5.2 builds, which reference `__imp___iob` (`msvcr100.dll`) and caused `LNK2001` errors in local builds. They are replaced with the MinGW-w64 builds produced by CI (#8)
- `nbis.rc` includes `winres.h` instead of the MFC header `afxres.h`, which is not available on current GitHub runners and broke the v1.3.0 release build (#8)

### Added
- The NBIS static libraries are published: as the `nbis-static-libs` workflow artifact and as `nbis-lib32.zip` / `nbis-lib64.zip` on every release (#8)

## [1.3.0] - 2026-05-29

### Added
- SonarCloud code analysis
- Extended test coverage: edge cases, parametrized tests, corruption handling (18 tests)
- GitHub Actions Node.js 24 support (Node.js 20 deprecated)
- Documentation: CHANGELOG.md, DEVELOPERS.md, improved README
- Development guidelines: CONTRIBUTING.md, PR template, EditorConfig

## [1.2.0] - 2026-05-29

### Added
- GitHub Actions workflow for automated NBIS DLL builds (`build-nbis-dll.yml`)
- Automated test execution with xUnit (`test.yml`)
- Support for modern MinGW-w64 (GCC 14+) toolchain
- WSQ codec available as pre-built nbis32.dll and nbis64.dll
- Release artifacts with DLL binaries attached to GitHub Releases

### Changed
- PlatformToolset updated from v145 (VS 2025) to v143 (VS 2022) for CI compatibility
- Removed dependency on MS Visual C++ 2010 Redistributable (now uses static CRT linking)

### Fixed
- GCC 10+ `-fcommon` compatibility for NBIS source code
- SAFESEH errors for Win32 builds (required for MinGW-w64 object compatibility)
- Missing MinGW runtime shims: `__mingw_fprintf`, `__mingw_sprintf`, `__mingw_sscanf`
- DLL artifact structure flattening in test workflow
- Test reporter permissions for GitHub checks integration

### Removed
- Deprecated workflow configurations (manual build steps)

## [1.1.0] - 2026-05-29

### Added
- xUnit test project for Delta.Wsq codec (`WsqCodecTests`)
- 10 comprehensive tests covering:
  - Decode from known WSQ files
  - Encode with custom bitrates and comments
  - Round-trip encode/decode preservation
  - Comment extraction and metadata handling

### Fixed
- `RawImageData.Empty` made readonly to prevent accidental mutation
- `WsqCodec.Encode` returns `Array.Empty<byte>()` instead of null for consistency

## [1.0.0] - 2026-05-28

### Added
- Initial Delta.Imaging project structure
- Delta.Wsq component with native WSQ codec support
- WSQ decode/encode functionality
- GDI+ and WPF image conversion support
- Paint.NET plugin for WSQ format
- Delta.ImageRenameTool utility
- Comprehensive README and documentation

### Notes
- Initial release with manual build process for NBIS DLLs
- Dependency on MS Visual C++ 2010 Redistributable

---

## Version History Summary

| Version | Date | Notable Changes |
|---------|------|-----------------|
| 1.4.0 | 2026-11-17 | Input validation, thread safety, 32/64-bit P/Invoke fixes, versioned library zip |
| 1.3.3 | 2026-11-16 | NBIS debug output disabled |
| 1.3.2 | 2026-11-16 | Debug builds fixed, fail-early CI, release workflow |
| 1.3.1 | 2026-11-16 | Modern NBIS static libraries committed and published |
| 1.3.0 | 2026-05-29 | SonarCloud, more tests, developer documentation |
| 1.2.0 | 2026-05-29 | Automated DLL builds, modern MinGW-w64, no CRT dependency |
| 1.1.0 | 2026-05-29 | xUnit tests added |
| 1.0.0 | 2026-05-28 | Initial release |

## Upgrade Notes

### From 1.1.0 → 1.2.0
- Use the new nbis32.dll / nbis64.dll (v1.2.0+): static CRT, no Visual C++ Redistributable needed
- The v1.1.0 DLLs still work but require the VC++ 2010 Redistributable

### From 1.3.x → 1.4.0
- Replace `nbis32.dll` and `nbis64.dll` together with `Delta.Wsq.dll`. The validation of WSQ data and the other decoder fixes are in the native DLLs: `Delta.Wsq.dll` 1.4.0 with the 1.3.x native DLLs still crashes on damaged files
- `WsqCodec.Encode` throws `ArgumentException` / `ArgumentOutOfRangeException` for input it previously passed to the native encoder unchecked: images smaller than 33 x 33 pixels, a depth other than 8 bpp, too little pixel data, a non-positive bit rate
- `WsqCodec.GetComments` returns an empty array for a file whose header is damaged (a marker segment outside the data, a malformed transform, quantization or Huffman table, frame header or block header), even when the comment segments themselves are intact. 1.3.x could return the comments of some such files
- Debug builds of `Delta.Wsq` load `nbis32.dll` / `nbis64.dll`; `nbis32d.dll` / `nbis64d.dll` are no longer used
- The version of `Delta.Wsq.dll` follows the release tag (it was 1.1.0.0 in every release so far)
- Converted images can differ from earlier versions where those were wrong:
  - `Conversions.To8bppBitmap` (and `WsqEncoder.EncodeGdi` with `autoConvertToGrayscale`, the default), `Conversions.WpfImageToImageInfo` and `ToGray8BitmapSource` convert 8 bpp indexed images through their palette colors. This changes the result for every palette other than the identity grayscale palette (entry *i* = gray level *i*): colored palettes, but also grayscale palettes in another order, such as an inverted one
  - `Conversions.GdiImageToImageInfo`, and `EncodeGdi` with `autoConvertToGrayscale: false`, are unchanged: they still use the palette indexes as gray levels
  - Resolutions are rounded instead of truncated (499.9998 dpi becomes 500 ppi, not 499)
- Calls into the nbis DLL run one at a time per process; code that encoded or decoded on several threads keeps working but no longer runs in parallel

## Future Plans

Planned work and known problems are tracked as [GitHub issues](https://github.com/SlOrbA/Delta.Imaging/issues).
