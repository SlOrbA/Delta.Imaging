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
