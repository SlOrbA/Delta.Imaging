Delta.Imaging
=============

[![CI](https://github.com/SlOrbA/Delta.Imaging/actions/workflows/ci.yml/badge.svg)](https://github.com/SlOrbA/Delta.Imaging/actions/workflows/ci.yml)
[![NBIS source build](https://github.com/SlOrbA/Delta.Imaging/actions/workflows/build-nbis-dll.yml/badge.svg?event=schedule)](https://github.com/SlOrbA/Delta.Imaging/actions/workflows/build-nbis-dll.yml)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=SlOrbA_Delta.Imaging&metric=alert_status)](https://sonarcloud.io/dashboard?id=SlOrbA_Delta.Imaging)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=SlOrbA_Delta.Imaging&metric=coverage)](https://sonarcloud.io/dashboard?id=SlOrbA_Delta.Imaging)
[![Latest Release](https://img.shields.io/github/v/release/SlOrbA/Delta.Imaging?color=blue&label=Latest%20Release)](https://github.com/SlOrbA/Delta.Imaging/releases)
[![License: Ms-RL](https://img.shields.io/badge/License-Ms--RL-blue.svg)](License.md)

Imaging libraries and tools, all sharing the [Ms-RL][msrl] license.
  
* **Delta.Wsq**: .NET WSQ (Image format primarily used to store fingerprints) encoder/decoder based on the [NIST NBIS implementation](https://github.com/lessandro/nbis). Provides also a [Paint.NET](http://www.getpaint.net/) plugin. ✅ No Redistributable dependency (static CRT)
* **Delta.ImageRenameTool**: A simple GUI allowing to rename multiple images at one time.

## Quick Start

### Using Delta.Wsq

The library requires `nbis32.dll` (32-bit) and `nbis64.dll` (64-bit) in the application directory. [Releases](https://github.com/SlOrbA/Delta.Imaging/releases) from v1.4.0 on have a `Delta.Wsq-<version>.zip` with `Delta.Wsq.dll` and both native DLLs; the native DLLs are also attached separately. Releases up to v1.3.3 contain the native DLLs only, so for those the library has to be [built from source](#building).

**DLL Features:**
- ✅ **No Redistributable dependency** — Built with static CRT (`/MT`)
- ✅ **KERNEL32.dll only** — the C runtime is linked statically, nothing else is imported
- ✅ **Modern toolchain** — MSVC 2022, MinGW-w64 GCC 14+
- ✅ **Automated builds** — GitHub Actions builds on every release

### API Usage Example

```csharp
using Delta.Wsq;

// Load WSQ image from file
byte[] wsqData = File.ReadAllBytes("fingerprint.wsq");
var image = WsqCodec.Decode(wsqData);

// Or encode an existing image
var encoded = WsqCodec.Encode(image, bitrate: 0.75f, comment: "Test");

// Extract metadata
var comments = WsqCodec.GetComments(wsqData);
foreach (var comment in comments)
    Console.WriteLine(comment);
```

### Limitations

- Images must be 8 bits per pixel grayscale and at least 33 x 33 pixels (`WsqCodec.Constants.MinImageSize`).
- Damaged or truncated WSQ data is rejected with a `WsqException`. The decoder is fuzzed with corrupted files under AddressSanitizer (see [`Delta.Wsq/nbis/fuzz`](Delta.Wsq/nbis/fuzz)), but a file can still claim a very large image: decoding allocates memory in proportion to the width and height in its header before the data is checked, up to the limit of about 536 million pixels. Only the standard WSQ wavelet filter pair is supported (filter lengths 9 and 7, the pair NBIS itself writes); files with other filters are rejected.

### Thread safety

The underlying NBIS implementation keeps its state in global variables. `Delta.Wsq` therefore serializes all calls into the native DLL: the API can be used from several threads, but encoding and decoding run one at a time per process.

## Building

### .NET library

Open `Delta.Wsq/Delta.Wsq.sln` in Visual Studio 2022 and build, or:

```
dotnet build Delta.Wsq/Delta.Wsq/Delta.Wsq.csproj
```

### Native DLLs (nbis32.dll / nbis64.dll)

The DLLs are built from NBIS C source using MSYS2 + MSVC. See [`Delta.Wsq/nbis/BUILDING.md`](Delta.Wsq/nbis/BUILDING.md) for detailed instructions.

[`ci.yml`](.github/workflows/ci.yml) builds them on every PR and push to `master` against the committed NBIS static libraries; [`build-nbis-dll.yml`](.github/workflows/build-nbis-dll.yml) additionally verifies weekly that those libraries can still be rebuilt from source. [`release.yml`](.github/workflows/release.yml) attaches the DLLs, the versioned .NET library and the static libraries to GitHub Releases.

## Running tests

```
dotnet test Delta.Wsq/tests/Delta.Wsq.Tests/Delta.Wsq.Tests.csproj
```

> **Note:** Tests require `nbis32.dll` / `nbis64.dll` in `Delta.Wsq/dependencies/`. Download them from the latest release or build them first.

## Credits

Third-party libraries or various persons to be credited are so in the _Credits.md_ file of each project.

## Licensing

[Ms-RL][msrl]

  [msrl]: License.md "MS-RL License"
