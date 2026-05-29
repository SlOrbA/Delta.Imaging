# Developers Guide

This guide explains the Delta.Imaging project structure and how to contribute to it.

## Project Overview

**Delta.Imaging** is a C# library for biometric image processing, with WSQ (Wavelet Scalar Quantization) codec support.

```
Delta.Imaging/
├── Delta.Wsq/              # Main WSQ codec library
│   ├── Delta.Wsq/          # Managed C# wrapper
│   │   ├── NativeMethods.cs    # P/Invoke declarations
│   │   ├── WsqCodec.cs         # Public API
│   │   ├── RawImageData.cs     # Image data struct
│   │   ├── Conversions.*.cs    # GDI+/WPF converters
│   │   └── WsqCodec.helpers.cs # Implementation details
│   ├── nbis/               # Native DLL project
│   │   ├── nbis.vcxproj    # MSVC project (DLL build)
│   │   ├── mingw_shim.cpp  # MinGW runtime compatibility
│   │   ├── nistwsq.cpp     # DLL exports
│   │   └── BUILDING.md     # Manual build instructions
│   ├── tests/              # Test projects
│   │   ├── Delta.Wsq.Tests/      # Unit tests (xUnit)
│   │   └── TestApplication/      # Manual test GUI
│   ├── dependencies/       # External libraries (Paint.NET DLLs)
│   └── images/             # Test WSQ files
├── Delta.ImageRenameTool/  # Image renaming utility
├── .github/workflows/      # CI/CD automation
│   ├── build-nbis-dll.yml  # Build nbis DLLs (MSYS2 + MSVC)
│   └── test.yml            # Run tests after build
└── Documentation/
    ├── README.md           # Project overview
    ├── CHANGELOG.md        # Version history
    ├── FUTURE_IMPROVEMENTS.md  # Roadmap
    └── DEVELOPERS.md       # This file
```

## Technology Stack

- **Language:** C# (.NET 8.0)
- **Test Framework:** xUnit.net
- **Native Build:** MSYS2 MinGW-w64 (GCC 14+), MSVC 2022
- **CI/CD:** GitHub Actions
- **WSQ Codec:** NIST NBIS library (~2010)

## NBIS DLL Compilation

### Why Custom DLLs?

The NIST NBIS library is from ~2010 and uses:
- Old GCC code patterns (triggers GCC 10+ warnings)
- MinGW runtime functions (requires shims for MSVC linking)
- Manual build process not suitable for CI

Our solution: Automated build via GitHub Actions + compatibility layer

### Build Flow

```
1. Clone NBIS source (github.com/lessandro/nbis)
2. Run MSYS2 MinGW-w64 build
   - setup.sh: Configure for MinGW32/MinGW64
   - Patch rules.mak: Add -fcommon (GCC 10+ compat)
   - make: Compile → static .a libraries
3. Link in MSVC
   - Read .a files as object files
   - Add mingw_shim.cpp: Provide missing runtime functions
   - Link → nbis32.dll / nbis64.dll
```

### MinGW Shim Pattern

NBIS compiled with MinGW-w64 calls internal MinGW runtime functions like:
```c
__mingw_fprintf()   // MinGW's printf variant
__mingw_sprintf()   // MinGW's sprintf variant
```

These don't exist in MSVC. We provide implementations:

```cpp
int __cdecl __mingw_fprintf(FILE* file, const char* fmt, ...) {
    va_list args;
    va_start(args, fmt);
    int result = vfprintf_s(file, fmt, args);  // Use MSVC equivalent
    va_end(args);
    return result;
}
```

**Location:** `Delta.Wsq/nbis/mingw_shim.cpp`
**Current shims:** `__mingw_fprintf`, `__mingw_sprintf`, `__mingw_sscanf` (+ variadic versions)

### Building Locally

See [Delta.Wsq/nbis/BUILDING.md](./Delta.Wsq/nbis/BUILDING.md) for manual build instructions.

## Testing

### Unit Tests (xUnit)

Location: `Delta.Wsq/tests/Delta.Wsq.Tests/WsqCodecTests.cs`

Current coverage:
- Decode functionality
- Encode with various bitrates
- Round-trip preservation
- Comment handling
- Edge cases (small/large images, corruption)

Run tests:
```bash
cd Delta.Wsq
dotnet test tests/Delta.Wsq.Tests/Delta.Wsq.Tests.csproj
```

### Automated Testing

CI workflow (`test.yml`):
1. Build DLLs from source (via `build-nbis-dll.yml`)
2. Download DLL artifacts
3. Build test project
4. Run all tests
5. Report results via GitHub Checks

## CI/CD Workflows

### build-nbis-dll.yml

**Trigger:** Every push, PR, release
**Steps:**
1. Setup MSYS2 MinGW toolchains (cached)
2. Clone NBIS source
3. Patch + build 32-bit static libraries
4. Patch + build 64-bit static libraries
5. Build MSVC DLL projects
6. Verify no CRT dependencies
7. Upload DLL artifacts
8. (On release) Attach to GitHub Release

**Environment:**
- OS: Windows Latest (windows-2025-vs2026)
- MSYS2: MINGW32 + MINGW64
- MSVC: Visual Studio 2022 (v143 toolset)

### test.yml

**Trigger:** Every push, PR
**Steps:**
1. Invoke `build-nbis-dll.yml` (gets DLLs)
2. Download DLL artifacts
3. Setup .NET 8.0
4. Restore NuGet packages
5. Build test project
6. Run xUnit tests
7. Publish results as GitHub Checks

**Permissions:** Requires `checks: write` for test-reporter action

## Code Style

No official style guide yet, but follow C# conventions:
- PascalCase for public types/methods
- camelCase for local variables
- Avoid `var` where type isn't obvious
- Comments for non-obvious logic only

## Common Tasks

### Adding a New Test

```csharp
[Fact]
public void SomeFeature_Condition_ExpectedOutcome()
{
    // Arrange
    var input = ...;
    
    // Act
    var result = SomeMethod(input);
    
    // Assert
    Assert.Equal(expected, result);
}
```

Add to `WsqCodecTests.cs` and run `dotnet test`.

### Updating NBIS Build

If NBIS source needs updating:
1. Update GitHub remote in `build-nbis-dll.yml`
2. Test in PR (workflow will attempt new build)
3. If new MinGW shims needed, update `mingw_shim.cpp`
4. Document in BUILDING.md

### Releasing a New Version

1. Update version in `Delta.Wsq/Properties/AssemblyInfo.cs`
2. Update `CHANGELOG.md`
3. Commit: `git commit -am "Bump version to X.Y.Z"`
4. Tag: `git tag -a vX.Y.Z -m "Release vX.Y.Z"`
5. Push: `git push --tags`
6. GitHub Actions builds & creates release automatically

## Troubleshooting

### Linker error: unresolved external symbol `__mingw_*`

**Cause:** MinGW NBIS uses internal runtime functions not available in MSVC

**Solution:** Add shim in `mingw_shim.cpp`, following existing pattern

### Build fails with `-fcommon` or multiple definition errors

**Cause:** GCC 10+ changed default to `-fno-common`; NBIS has globals in headers

**Solution:** Patch `rules.mak` in build workflow (already done)

### Tests fail: "Unable to load DLL 'nbis64.dll'"

**Cause:** DLL path wrong or artifact not downloaded

**Solution:** Check `test.yml` flattens artifact structure correctly

### "Node.js 20 actions are deprecated" warnings

**Cause:** Actions running on old Node.js runtime

**Solution:** Set `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24: true` in workflow env

## Performance & Memory

- WSQ encoding is CPU-bound (no optimizations yet)
- No known memory leaks
- Large images (> 5000x5000) untested

Future: Add benchmarking with BenchmarkDotNet

## Security

- No external dependencies on suspicious packages
- Dependabot enabled for automated CVE scanning
- No hardcoded secrets
- MSVC uses `/SDL` (security enhancements) flag

See also: `.github/dependabot.yml`

## Resources

- [NIST NBIS GitHub](https://github.com/lessandro/nbis)
- [WSQ Format Spec](https://nvlpubs.nist.gov/nistpubs/Legacy/IR/nistir6844.pdf)
- [C# Interop](https://learn.microsoft.com/dotnet/standard/native-interop/pinvoke)
- [GitHub Actions](https://docs.github.com/actions)

## Questions?

Open an issue or discussion on GitHub!
