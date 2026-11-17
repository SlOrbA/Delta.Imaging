# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- `WsqCodec.Encode` validates its input instead of letting the native encoder read past the pixel buffer: dimensions must be positive, the depth 8 bpp, `Data` at least `Width * Height` bytes (`ArgumentException`) and the bit rate a positive finite number (`ArgumentOutOfRangeException`) (#15)
- Calls into the nbis DLL are serialized. NBIS keeps codec state in global variables, so concurrent `Encode` / `Decode` / `GetComments` calls could corrupt each other or crash (#16)
- Debug builds of `Delta.Wsq` load `nbis32.dll` / `nbis64.dll` like Release builds, instead of `nbis32d.dll` / `nbis64d.dll`, which are neither built by CI nor shipped. `dotnet test` now works in the default configuration (#17)
- Image resolution is rounded instead of truncated when converting GDI+ / WPF images: a 500 dpi PNG (stored as 499.9998 dpi) is no longer written as 499 ppi (#18)
- `Conversions.To8bppBitmap`: 8 bpp images with a non-grayscale palette are converted through their palette colors instead of using the palette indexes as gray levels, and the returned bitmap always has a grayscale palette (#19)
- 32-bit processes: the P/Invoke declarations in `NativeMethods` now use `CallingConvention.Cdecl`, matching the `__cdecl` exports of the nbis DLL. The default (StdCall) left the arguments on the stack after every call, reported by the debugger as `PInvokeStackImbalance`
- 64-bit processes: `Conversions.To8bppBitmap` (used by `WsqEncoder.EncodeGdi` with `autoConvertToGrayscale`) no longer casts bitmap pointers to `int`, which threw `OverflowException` on .NET Framework and crashed with an access violation on .NET when the bitmap buffer was above 2 GB
- `Conversions.GdiImageToImageInfo` / `ImageInfoToGdiImage` (and therefore `WsqEncoder.EncodeGdi` / `WsqDecoder.DecodeGdi`) threw `EntryPointNotFoundException`: they P/Invoked `CopyMemory` from `kernel32.dll`, which is only a macro in the Windows headers and not an exported function. Rows are now copied with `Buffer.MemoryCopy`

### Changed
- nbis DLL no longer prints NBIS debug/progress messages to stderr on every encode/decode (`int debug = 0` in `mingw_shim.cpp`). Console applications and services hosting the DLL see less output; genuine NBIS error messages are still printed (#11)

### Added
- Extended test coverage: edge cases, parametrized tests, corruption handling
- GitHub Actions Node.js 24 support (Node.js 20 deprecated)
- Documentation: CHANGELOG.md, DEVELOPERS.md, improved README
- Development guidelines: CONTRIBUTING.md, PR template
- Future roadmap: FUTURE_IMPROVEMENTS.md

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

| Version | Date | Status | Notable Changes |
|---------|------|--------|-----------------|
| 1.2.0 | 2026-05-29 | Released | Full automation, modern MinGW-w64, no CRT dependency |
| 1.1.0 | 2026-05-29 | Released | xUnit tests added |
| 1.0.0 | 2026-05-28 | Released | Initial release |

## Upgrade Path

### From 1.0.0 → 1.1.0
- Automatic: code-only changes, no breaking changes
- Tests are optional but recommended

### From 1.1.0 → 1.2.0
- **Breaking change**: None
- **Recommended**: Use new nbis32.dll/nbis64.dll (v1.2.0+) for static CRT (no Redistributable needed)
- Old v1.1.0 DLLs still work but require VC++ 2010 Redistributable

## Future Plans

See [FUTURE_IMPROVEMENTS.md](./FUTURE_IMPROVEMENTS.md) for:
- GitHub Actions deprecation management
- Extended test coverage
- CI/CD optimization
- Static analysis integration
- NuGet package distribution
