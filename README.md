Delta.Imaging
=============

[![Build nbis DLL](https://github.com/SlOrbA/Delta.Imaging/actions/workflows/build-nbis-dll.yml/badge.svg)](https://github.com/SlOrbA/Delta.Imaging/actions/workflows/build-nbis-dll.yml)
[![Test](https://github.com/SlOrbA/Delta.Imaging/actions/workflows/test.yml/badge.svg)](https://github.com/SlOrbA/Delta.Imaging/actions/workflows/test.yml)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=SlOrbA_Delta.Imaging&metric=alert_status)](https://sonarcloud.io/dashboard?id=SlOrbA_Delta.Imaging)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=SlOrbA_Delta.Imaging&metric=coverage)](https://sonarcloud.io/dashboard?id=SlOrbA_Delta.Imaging)
[![Latest Release](https://img.shields.io/github/v/release/SlOrbA/Delta.Imaging?color=blue&label=Latest%20Release)](https://github.com/SlOrbA/Delta.Imaging/releases)
[![License: Ms-RL](https://img.shields.io/badge/License-Ms--RL-blue.svg)](License.md)

Imaging libraries and tools, all sharing the [Ms-RL][msrl] license.
  
* **Delta.Wsq**: .NET WSQ (Image format primarily used to store fingerprints) encoder/decoder based on the [NIST NBIS implementation](https://github.com/lessandro/nbis). Provides also a [Paint.NET](http://www.getpaint.net/) plugin. ✅ No Redistributable dependency (static CRT)
* **Delta.ImageRenameTool**: A simple GUI allowing to rename multiple images at one time.

## Quick Start

### Using Delta.Wsq

The library requires `nbis32.dll` (32-bit) and `nbis64.dll` (64-bit) in the application directory. Download the latest pre-built DLLs from the [Releases](https://github.com/SlOrbA/Delta.Imaging/releases) page.

**DLL Features:**
- ✅ **No Redistributable dependency** — Built with static CRT (`/MT`)
- ✅ **KERNEL32.dll and msvcrt.dll only** — Built-in Windows components
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

## Building

### .NET library

Open `Delta.Wsq/Delta.Wsq.sln` in Visual Studio 2022 and build, or:

```
dotnet build Delta.Wsq/Delta.Wsq/Delta.Wsq.csproj
```

### Native DLLs (nbis32.dll / nbis64.dll)

The DLLs are built from NBIS C source using MSYS2 + MSVC. See [`Delta.Wsq/nbis/BUILDING.md`](Delta.Wsq/nbis/BUILDING.md) for detailed instructions.

The CI workflow ([`build-nbis-dll.yml`](.github/workflows/build-nbis-dll.yml)) builds them automatically on every push and attaches them to GitHub Releases.

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
