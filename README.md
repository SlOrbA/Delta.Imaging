Delta.Imaging
=============

[![Build nbis DLL](https://github.com/SlOrbA/Delta.Imaging/actions/workflows/build-nbis-dll.yml/badge.svg)](https://github.com/SlOrbA/Delta.Imaging/actions/workflows/build-nbis-dll.yml)
[![Test](https://github.com/SlOrbA/Delta.Imaging/actions/workflows/test.yml/badge.svg)](https://github.com/SlOrbA/Delta.Imaging/actions/workflows/test.yml)

Imaging libraries and tools, all sharing the [Ms-RL][msrl] license.
  
* **Delta.Wsq**: .NET WSQ (Image format primarily used to store fingerprints) encoder/decoder based on the [NBIS implementation](https://github.com/lessandro/nbis) by NIST. Provides also a [Paint.NET](http://www.getpaint.net/) plugin.
* **Delta.ImageRenameTool**: A simple GUI allowing to rename multiple images at one time.

## Using Delta.Wsq

The library requires `nbis32.dll` (32-bit) and `nbis64.dll` (64-bit) in the application directory. Download the latest pre-built DLLs from the [Releases](https://github.com/SlOrbA/Delta.Imaging/releases) page.

The DLLs have **no redistributable dependency** — only `KERNEL32.dll` and `msvcrt.dll` (built-in Windows system components).

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
