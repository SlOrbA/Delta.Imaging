using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Delta.Wsq
{
    partial class Conversions
    {
        public static BitmapSource ImageInfoToWpfImage(RawImageData info)
        {
            const int defaultDpi = 96; // Use this one if resolution not set in the wsq file.

            if (info == null) throw new ArgumentNullException(nameof(info));
            if (info.IsEmpty) throw new ArgumentException("The image is empty.", nameof(info));

            // 16bpp grayscale is not really supported by the underlying codec.
            if (info.PixelDepth != 8) throw new NotSupportedException(
                "Source image must have a pixel depth of exactly 8 bpp.");

            var dpi = info.Resolution > 0 ? info.Resolution : defaultDpi;

            // One byte per pixel, rows without padding.
            return BitmapSource.Create(
                info.Width, info.Height, dpi, dpi, PixelFormats.Gray8, null, info.Data, info.Width);
        }

        public static RawImageData WpfImageToImageInfo(BitmapSource image)
        {
            if (image == null) return RawImageData.Empty;
            EnsureSupportedFormat(image.Format);

            return new RawImageData()
            {
                Data = ReadGrayPixels(image),
                Width = image.PixelWidth,
                Height = image.PixelHeight,
                Resolution = (int)Math.Round(image.DpiX),
                PixelDepth = 8
            };
        }

        public static BitmapSource ToGray8BitmapSource(BitmapSource input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Format == PixelFormats.Gray8)
                return input; // We are already good!
            EnsureSupportedFormat(input.Format);

            var w = input.PixelWidth;
            return BitmapSource.Create(
                w, input.PixelHeight, input.DpiX, input.DpiY, PixelFormats.Gray8, null, ReadGrayPixels(input), w);
        }

        private static void EnsureSupportedFormat(PixelFormat format)
        {
            if (format != PixelFormats.Bgr24 &&
                format != PixelFormats.Bgr32 &&
                format != PixelFormats.Bgra32 &&
                format != PixelFormats.Gray8 &&
                format != PixelFormats.Indexed8)
                throw new NotSupportedException(string.Format(
                    "Pixel Format {0} is not supported.", format));
        }

        // The image as one gray byte per pixel, rows without padding. Color
        // pixels become the average of their channels; indexed pixels are
        // looked up in the palette first.
        private static byte[] ReadGrayPixels(BitmapSource image)
        {
            var w = image.PixelWidth;
            var h = image.PixelHeight;
            var bytesPerPixel = image.Format.BitsPerPixel / 8;

            var pixels = new byte[w * h * bytesPerPixel];
            image.CopyPixels(pixels, w * bytesPerPixel, 0);

            if (image.Format == PixelFormats.Gray8)
                return pixels;

            var gray = new byte[w * h];
            if (bytesPerPixel >= 3)
            {
                for (var i = 0; i < gray.Length; i++)
                {
                    var j = i * bytesPerPixel;
                    gray[i] = (byte)((pixels[j] + pixels[j + 1] + pixels[j + 2]) / 3);
                }
            }
            else // Indexed8
            {
                var levels = PaletteGrayLevels(image.Palette);
                for (var i = 0; i < gray.Length; i++)
                    gray[i] = levels[pixels[i]];
            }

            return gray;
        }

        // Gray level of every palette index. Without a palette the index is
        // the gray level; indexes beyond the palette are black.
        private static byte[] PaletteGrayLevels(BitmapPalette palette)
        {
            var levels = new byte[256];
            if (palette == null)
            {
                for (var i = 0; i < levels.Length; i++)
                    levels[i] = (byte)i;
                return levels;
            }

            var colors = palette.Colors;
            for (var i = 0; i < colors.Count && i < levels.Length; i++)
                levels[i] = (byte)((colors[i].R + colors[i].G + colors[i].B) / 3);
            return levels;
        }
    }
}
