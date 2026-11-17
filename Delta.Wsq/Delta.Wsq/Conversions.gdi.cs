using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Delta.Wsq
{
    public static partial class Conversions
    {
        public static unsafe Bitmap ImageInfoToGdiImage(RawImageData info)
        {
            ArgumentNullException.ThrowIfNull(info);
            if (info.IsEmpty) throw new ArgumentException("The image is empty.", nameof(info));

            // 16bpp grayscale is not really supported by the underlying codec.
            if (info.PixelDepth != 8) throw new NotSupportedException(
                "Source image must have a pixel depth of exactly 8 bpp.");

            var pformat = info.PixelDepth == 8 ?
                PixelFormat.Format8bppIndexed : PixelFormat.Format16bppGrayScale;

            var mul = info.PixelDepth == 8 ? 1 : 2;
            var w = info.Width;
            var h = info.Height;

            var image = new Bitmap(w, h, pformat);

            var bmpData = image.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, pformat);
            try
            {
                fixed (byte* infoData = info.Data)
                {
                    var source = infoData;
                    var dest = (byte*)bmpData.Scan0;
                    for (int i = 0; i < h; i++)
                    {
                        Buffer.MemoryCopy(source, dest, w * mul, w * mul);
                        dest += bmpData.Stride;
                        source += w * mul;
                    }
                }

            }
            finally
            {
                image.UnlockBits(bmpData);
            }

            if (info.PixelDepth == 8)
                SetGrayscalePalette(image);

            SafeSetResolution(image, info.Resolution);
            return image;
        }

        public static unsafe RawImageData GdiImageToImageInfo(Bitmap image)
        {
            ArgumentNullException.ThrowIfNull(image);

            // 16bpp grayscale is not really supported by the underlying codec.
            if (/* image.PixelFormat != PixelFormat.Format16bppGrayScale && */ image.PixelFormat != PixelFormat.Format8bppIndexed)
                throw new ArgumentException("Unsupported image format.");

            var info = new RawImageData()
            {
                Resolution = (int)Math.Round(image.HorizontalResolution),
                Width = image.Width,
                Height = image.Height,
                PixelDepth = image.PixelFormat == PixelFormat.Format8bppIndexed ? 8 : 16
            };

            var mul = info.PixelDepth == 8 ? 1 : 2;
            var w = info.Width;
            var h = info.Height;

            info.Data = new byte[w * h * mul];
            var bmpData = image.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, image.PixelFormat);
            try
            {
                fixed (byte* infoData = info.Data)
                {
                    var dest = infoData;
                    var source = (byte*)bmpData.Scan0;
                    for (int i = 0; i < h; i++)
                    {
                        Buffer.MemoryCopy(source, dest, w * mul, w * mul);
                        dest += w * mul;
                        source += bmpData.Stride;
                    }
                }
            }
            finally
            {
                image.UnlockBits(bmpData);
            }

            return info;
        }

        public unsafe static Bitmap To8bppBitmap(Bitmap input)
        {
            ArgumentNullException.ThrowIfNull(input);
            // An indexed image can only be used as is when its palette indexes
            // are the gray levels; otherwise convert it through its colors.
            if (input.PixelFormat == PixelFormat.Format8bppIndexed && HasGrayscalePalette(input))
            {
                var clone = (Bitmap)input.Clone();
                clone.SetResolution(input.HorizontalResolution, input.VerticalResolution);
                return clone;
            }

            var result = new Bitmap(input.Width, input.Height, PixelFormat.Format8bppIndexed);

            // Temporary image is 24bpp
            using (var bitmap = new Bitmap(input.Width, input.Height, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(bitmap))
                    g.DrawImage(input, 0, 0, input.Width, input.Height);

                var w = bitmap.Width;
                var h = bitmap.Height;
                var bitmapData = bitmap.LockBits(new Rectangle(0, 0, w, h),
                    ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                var resultData = result.LockBits(new Rectangle(0, 0, w, h),
                    ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);

                try
                {
                    // Pointer arithmetic, not (int)Scan0: on 64-bit the bitmap
                    // buffers can live above 2 GB, where an int cannot hold the
                    // address (OverflowException on .NET Framework, a truncated
                    // pointer and an access violation on .NET).
                    var inputScan0 = (byte*)bitmapData.Scan0;
                    var outputScan0 = (byte*)resultData.Scan0;

                    for (var y = 0; y < h; y++)
                    {
                        var pixptr = inputScan0 + y * bitmapData.Stride;
                        var resptr = outputScan0 + y * resultData.Stride;
                        for (var x = 0; x < w; x++, pixptr += 3)
                            resptr[x] = (byte)((pixptr[0] + pixptr[1] + pixptr[2]) / 3);
                    }
                }
                finally
                {
                    result.UnlockBits(resultData);
                    bitmap.UnlockBits(bitmapData);
                }
            }

            SetGrayscalePalette(result);
            result.SetResolution(input.HorizontalResolution, input.VerticalResolution);

            return result;
        }

        // Palette index i = gray level i.
        private static void SetGrayscalePalette(Bitmap image)
        {
            // Bitmap.Palette returns a copy: modify it, then assign it back.
            var palette = image.Palette;
            for (int i = 0; i < palette.Entries.Length; i++)
                palette.Entries[i] = Color.FromArgb(i, i, i);
            image.Palette = palette;
        }

        // A full, opaque 256-entry palette where index i is gray level i. Images
        // loaded from files can carry shorter palettes; those are converted.
        private static bool HasGrayscalePalette(Bitmap image)
        {
            var entries = image.Palette.Entries;
            if (entries.Length != 256) return false;
            for (int i = 0; i < entries.Length; i++)
            {
                var c = entries[i];
                if (c.A != 255 || c.R != i || c.G != i || c.B != i) return false;
            }

            return true;
        }

        private static void SafeSetResolution(Bitmap image, int resolution)
        {
            const int defaultResolution = 96;
            if (resolution <= 0) // Invalid (or not set) 
                image.SetResolution((float)defaultResolution, (float)defaultResolution);
            else
            {
                try
                {
                    image.SetResolution((float)resolution, (float)resolution);
                }
                catch (ArgumentException) // a resolution GDI+ does not accept
                {
                    image.SetResolution((float)defaultResolution, (float)defaultResolution);
                }
            }
        }
    }
}
