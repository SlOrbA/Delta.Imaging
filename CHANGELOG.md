# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
