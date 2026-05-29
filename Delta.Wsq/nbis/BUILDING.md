# Building nbis32.dll / nbis64.dll

This document explains how `nbis32.dll` and `nbis64.dll` are built, why the build process
was changed from the original approach, and how to reproduce the build from source.

---

## Background

The DLL wraps the [NBIS](https://github.com/lessandro/nbis) (NIST Biometric Image Software)
C library, exposing WSQ encode/decode functionality to .NET via P/Invoke.

The DLL is a hybrid: C source files in `nbis/` are compiled with MSVC (Visual Studio), and
pre-built NBIS static libraries (`.a` files in `lib32/` and `lib64/`) — compiled with
MinGW GCC — are linked in by the MSVC linker. `mingw_shim.cpp` bridges the gap by providing
MSVC implementations of MinGW runtime functions that the `.a` files reference.

---

## Problem: VS 2010 Redistributable Dependency

The `.a` files originally checked into the repository were compiled with **MinGW GCC 4.5.2
(circa 2010)**, using a toolchain that linked against `msvcr100.dll` (the VS 2010 C runtime).
This forced a hard dependency on the **MS Visual C++ 2010 Redistributable** package on any
machine running the DLL — even though the MSVC project itself was built with a much newer
toolset (`v145`, Visual Studio 2022).

Additionally, `mingw_shim.cpp` was missing shims for three MinGW runtime functions that the
`.a` files reference: `__mingw_vprintf`, `__mingw_vsnprintf`, and `__mingw_vfscanf`.

### Root Cause (confirmed by binary analysis)

| Library | Problem symbol | Source |
|---|---|---|
| lib32 | `__imp___iob` | `msvcr100.dll` — VS 2010 CRT, needs redistribution |
| lib64 | `__imp___iob_func` | `msvcrt.dll` — Windows system DLL, always present |
| both | `__mingw_vprintf`, `__mingw_vsnprintf`, `__mingw_vfscanf` | Not shimmed → linker error |

---

## Solution

The fix has two parts:

### Part 1 — Rebuild `.a` files with MSYS2 + modern MinGW-w64

Modern MinGW-w64 (available via [MSYS2](https://www.msys2.org/)) links against `msvcrt.dll`
(a Windows system DLL, always present, no redistribution needed) instead of `msvcr100.dll`.
This eliminates the VS 2010 dependency.

### Part 2 — Static MSVC CRT (`/MT`) + complete shims

The MSVC project uses `/MT` (static C runtime) for Release builds, eliminating the dependency
on `vcruntime140.dll` (VS 2022 redistributable). The `mingw_shim.cpp` was updated to cover
all MinGW runtime functions referenced by the new `.a` files.

**Result:** The DLL depends only on `KERNEL32.dll` and `msvcrt.dll` — both Windows system
components present on every modern Windows installation. No redistributable package required.

---

## Reproducing the Build

### Prerequisites

- Windows (the build must happen on Windows)
- [MSYS2](https://www.msys2.org/) installed (e.g. to `C:\msys64`)
- [CMake for Windows](https://cmake.org/download/) added to PATH
- [Visual Studio 2022](https://visualstudio.microsoft.com/) with C++ workload

### Step 1: Install MinGW toolchains in MSYS2

Open an **MSYS2 MSYS** shell (not MinGW64) and run:

```bash
pacman -Syu
pacman -S mingw-w64-x86_64-gcc mingw-w64-i686-gcc make cmake git
```

### Step 2: Build the 32-bit static libraries

Open an **MSYS2 MinGW32** shell (`mingw32.exe`) and run:

```bash
git clone https://github.com/lessandro/nbis
cd nbis
./setup.sh C:/NBISBuild32 --MSYS --STDLIBS --32
make config
# GCC >= 10 defaults to -fno-common; NBIS defines globals in headers so we must add -fcommon:
sed -i 's/^CFLAGS = /CFLAGS = -fcommon /' Makefile.config
make it
```

The compiled `.a` files will be in `C:/NBISBuild32/lib/`.

### Step 3: Build the 64-bit static libraries

Open an **MSYS2 MinGW64** shell (`mingw64.exe`) and run:

```bash
cd nbis   # same clone as above
./setup.sh C:/NBISBuild64 --MSYS --STDLIBS --64
make config
sed -i 's/^CFLAGS = /CFLAGS = -fcommon /' Makefile.config
make it
```

The compiled `.a` files will be in `C:/NBISBuild64/lib/`.

### Step 4: Copy results into this repository

```
C:/NBISBuild32/lib/*.a  →  Delta.Wsq/nbis/lib32/
C:/NBISBuild64/lib/*.a  →  Delta.Wsq/nbis/lib64/
```

### Step 5: Verify no legacy CRT dependency

From an MSYS2 or Git Bash shell:

```bash
# Should print nothing if the VS 2010 problem is fixed:
nm lib32/libwsq.a | grep "__imp___iob"
```

### Step 6: Build the DLL with Visual Studio

Open `Delta.Wsq.sln` in Visual Studio 2022 and build `Release|Win32` and `Release|x64`,
or use the provided `build.cmd` script.

### Step 7: Verify DLL dependencies

From a Visual Studio Developer Command Prompt:

```cmd
dumpbin /dependents bin\Release\Win32\nbis32.dll
dumpbin /dependents bin\Release\x64\nbis64.dll
```

Expected output — only these two:
```
KERNEL32.dll
msvcrt.dll
```

If `msvcr100.dll`, `vcruntime140.dll`, or any other CRT DLL appears, the build is not clean.

---

## Notes

- The `--STDLIBS` flag builds NBIS without OpenJP2 and PNG support, keeping dependencies minimal.
  The WSQ codec (the only function this DLL exposes) does not require those formats.
- If `setup.sh` behaves unexpectedly under MSYS2 (it was written for an older MSYS environment),
  inspect and adapt the generated `Makefile.config` as needed.
- The `mingw_shim.cpp` bridges `__mingw_*` printf/scanf variants. If a future MinGW toolchain
  adds or renames functions, add corresponding shims there.
- Using `/MT` (static CRT) means the MSVC-compiled code and the MinGW-compiled code each have
  their own CRT instance. This is safe here because all memory allocated internally by the NBIS
  libraries is freed through the exported `free_mem()` function, which calls the correct `free()`.
