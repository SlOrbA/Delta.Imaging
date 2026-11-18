using System;
using System.Drawing;
using System.Drawing.Imaging;
using Xunit;

namespace Delta.Wsq.Tests
{
    public class GdiConversionTests
    {
        // Odd width: the 24bpp (765 → 768) and 8bpp (255 → 256) scanlines both
        // carry padding bytes. Large enough for the NBIS WSQ codec.
        private const int Width = 255;
        private const int Height = 200;

        private static readonly Color Left = Color.FromArgb(30, 60, 90);     // gray 60
        private static readonly Color Right = Color.FromArgb(200, 210, 250); // gray 220

        // Left half one color, right half another.
        private static Bitmap MakeColorBitmap()
        {
            var bitmap = new Bitmap(Width, Height, PixelFormat.Format24bppRgb);
            for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                    bitmap.SetPixel(x, y, x < Width / 2 ? Left : Right);
            bitmap.SetResolution(500, 500);
            return bitmap;
        }

        [Fact]
        public void To8bppBitmap_ColorInput_AveragesChannels()
        {
            using var input = MakeColorBitmap();

            using var gray = Conversions.To8bppBitmap(input);
            var raw = Conversions.GdiImageToImageInfo(gray);

            Assert.Equal(PixelFormat.Format8bppIndexed, gray.PixelFormat);
            Assert.Equal(Width, raw.Width);
            Assert.Equal(Height, raw.Height);
            // Sample away from the color boundary (DrawImage may blend there).
            for (var y = 0; y < Height; y++)
            {
                Assert.Equal(60, raw.Data[y * Width + 2]);
                Assert.Equal(220, raw.Data[y * Width + Width - 3]);
            }
        }

        [Fact]
        public void GdiConversions_NullOrInvalidImage_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => Conversions.To8bppBitmap(null));
            Assert.Throws<ArgumentNullException>(() => Conversions.GdiImageToImageInfo(null));
            Assert.Throws<ArgumentNullException>(() => Conversions.ImageInfoToGdiImage(null));
            Assert.Throws<ArgumentException>(() => Conversions.ImageInfoToGdiImage(new RawImageData()));
            Assert.Throws<NotSupportedException>(() => Conversions.ImageInfoToGdiImage(
                new RawImageData { Width = 4, Height = 4, PixelDepth = 16, Data = new byte[32] }));
            Assert.Throws<ArgumentNullException>(() => new WsqEncoder().EncodeGdi(null));
            Assert.Throws<ArgumentNullException>(() => new WsqDecoder().DecodeGdi(null));
            Assert.Throws<ArgumentNullException>(() => new WsqDecoder().Decode(Array.Empty<byte>()));

            using var color = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
            Assert.Throws<ArgumentException>(() => Conversions.GdiImageToImageInfo(color)); // not 8 bpp
        }

        // Documented in Delta.Wsq/README.md.
        [Fact]
        public void WsqDecoder_UndecodableData_Throws()
        {
            var garbage = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            Assert.Throws<WsqException>(() => new WsqDecoder().Decode(garbage));
            Assert.Throws<WsqException>(() => new WsqDecoder().DecodeGdi(garbage));
            Assert.Throws<WsqException>(() => new WsqDecoder().DecodeWpf(garbage));
        }

        [Fact]
        public void EncodeGdi_ColorBitmapWithoutConversion_Throws()
        {
            using var color = MakeColorBitmap();

            Assert.Throws<ArgumentException>(() => new WsqEncoder().EncodeGdi(color, autoConvertToGrayscale: false));
        }

        [Fact]
        public void ImageInfoToGdiImage_SetsResolutionOrFallsBackTo96Dpi()
        {
            var info = new RawImageData { Width = 4, Height = 4, PixelDepth = 8, Resolution = 500, Data = new byte[16] };
            using var withResolution = Conversions.ImageInfoToGdiImage(info);
            Assert.Equal(500f, withResolution.HorizontalResolution);

            info.Resolution = 0;
            using var withoutResolution = Conversions.ImageInfoToGdiImage(info);
            Assert.Equal(96f, withoutResolution.HorizontalResolution);
        }

        [Fact]
        public void EncodeGdi_ColorBitmap_RoundTripsDimensions()
        {
            using var input = MakeColorBitmap();

            var wsq = new WsqEncoder().EncodeGdi(input, 0.75f, autoConvertToGrayscale: true);
            using var decoded = new WsqDecoder().DecodeGdi(wsq);

            Assert.NotEmpty(wsq);
            Assert.Equal(Width, decoded.Width);
            Assert.Equal(Height, decoded.Height);
        }
    }
}
