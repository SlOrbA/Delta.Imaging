Delta.Wsq
=========

A .NET WSQ (Wavelet Scalar Quantization, the fingerprint image format) encoder/decoder based on the
[NIST NBIS implementation](https://github.com/lessandro/nbis). The native codec is wrapped in
`nbis32.dll` / `nbis64.dll`, which `Delta.Wsq.dll` calls through P/Invoke.

## Requirements

- .NET 8 on Windows (`net8.0-windows`), x86 or x64 process
- `nbis32.dll` and `nbis64.dll` next to the application; the one matching the process is loaded.
  They need no Visual C++ Redistributable.

Each [release](https://github.com/SlOrbA/Delta.Imaging/releases) has a `Delta.Wsq-<version>.zip`
with the library and both native DLLs.

## Usage

Raw 8-bit grayscale data:

```csharp
using Delta.Wsq;

RawImageData image = WsqCodec.Decode(File.ReadAllBytes("fingerprint.wsq"));
// image.Width, image.Height, image.Resolution (ppi), image.Data (one byte per pixel)

byte[] wsq = WsqCodec.Encode(image, WsqCodec.Constants.DefaultBitrate, "my comment");
string[] comments = WsqCodec.GetComments(wsq);
```

`System.Drawing` and WPF images, through `WsqEncoder` / `WsqDecoder`:

```csharp
var encoder = new WsqEncoder { Comment = "my comment" };
byte[] fromBitmap = encoder.EncodeGdi(bitmap);            // System.Drawing.Bitmap
byte[] fromSource = encoder.EncodeQualityWpf(source, 80); // BitmapSource, quality 0..100

var decoder = new WsqDecoder();
System.Drawing.Bitmap gdi = decoder.DecodeGdi(wsq);
System.Windows.Media.Imaging.BitmapSource wpf = decoder.DecodeWpf(wsq);
```

Color images are converted to grayscale (average of the channels) unless `autoConvertToGrayscale`
is `false`. The amount of compression can be given as a bit rate (0.75 is the FBI default, about
15:1), a compression ratio or a quality percentage.

## Limitations

- 8 bits per pixel grayscale only; images must be at least 33 x 33 pixels
  (`WsqCodec.Constants.MinImageSize`).
- `Decode` and `GetComments` return an empty result for data they cannot read; they do not throw.
- Calls into the native codec are serialized: the API is safe to use from several threads, but
  encoding and decoding run one at a time per process.
- The header of a WSQ file is validated before decoding, but the NBIS decoder is not hardened
  against deliberately corrupted image data. Do not decode untrusted files in a process that
  must not crash.

## Projects in the solution

| Project | Description |
|---|---|
| `Delta.Wsq` | The library |
| `nbis` | Native DLL: a thin C++ wrapper linked with the NBIS static libraries (`nbis/lib32`, `nbis/lib64`) |
| `PdnWsq` | [Paint.NET](https://www.getpaint.net/) file type plugin, as a working example |
| `tests/Delta.Wsq.Tests` | xUnit tests |
| `tests/TestApplication` | Manual test application (GDI+ and WPF) |

## Building

`dotnet build Delta.Wsq/Delta.Wsq.csproj` builds the library. The native DLLs need Visual Studio
2022 with the C++ workload: build the `nbis` project, or run `build.cmd` from a Developer Command
Prompt to build every configuration of the solution. See [nbis/BUILDING.md](nbis/BUILDING.md) for
how the NBIS static libraries themselves are built.

-----------------------------------------------------------------------------------------
* License: [Ms-RL][msrl]
* History page: [Here][history]
* Credits page: [Here][credits]

  [msrl]: License.md "MS-RL License"
  [history]: History.md "History"
  [credits]: Credits.md "Credits"
