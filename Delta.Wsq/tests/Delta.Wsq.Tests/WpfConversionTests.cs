using System;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace Delta.Wsq.Tests
{
    public class WpfConversionTests
    {
        // Odd width: every pixel format below has padded scanlines.
        private const int Width = 37;
        private const int Height = 21;

        private static int Stride(PixelFormat format) => (Width * format.BitsPerPixel / 8 + 3) / 4 * 4;

        // Left half (B, G, R) = (90, 60, 30) → gray 60, right half (250, 210, 200) → gray 220.
        private static BitmapSource MakeColorSource(PixelFormat format)
        {
            var bytesPerPixel = format.BitsPerPixel / 8;
            var stride = Stride(format);
            var pixels = new byte[stride * Height];
            for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                {
                    var offset = y * stride + x * bytesPerPixel;
                    var left = x < Width / 2;
                    pixels[offset] = (byte)(left ? 90 : 250);
                    pixels[offset + 1] = (byte)(left ? 60 : 210);
                    pixels[offset + 2] = (byte)(left ? 30 : 200);
                    if (bytesPerPixel == 4) pixels[offset + 3] = 255;
                }

            return BitmapSource.Create(Width, Height, 500, 500, format, null, pixels, stride);
        }

        // Every pixel has palette index 10; the palette is inverted gray, so
        // the image shows gray level 245.
        private static BitmapSource MakeIndexedSource()
        {
            var colors = Enumerable.Range(0, 256)
                .Select(i => Color.FromRgb((byte)(255 - i), (byte)(255 - i), (byte)(255 - i))).ToList();
            var stride = Stride(PixelFormats.Indexed8);
            var pixels = Enumerable.Repeat((byte)10, stride * Height).ToArray();
            return BitmapSource.Create(Width, Height, 500, 500, PixelFormats.Indexed8,
                new BitmapPalette(colors), pixels, stride);
        }

        public static TheoryData<PixelFormat> ColorFormats() => new TheoryData<PixelFormat>
        {
            PixelFormats.Bgr24,
            PixelFormats.Bgr32,
            PixelFormats.Bgra32,
        };

        private static void AssertTwoTone(byte[] data)
        {
            Assert.Equal(Width * Height, data.Length);
            for (var y = 0; y < Height; y++)
            {
                Assert.Equal(60, data[y * Width]);
                Assert.Equal(60, data[y * Width + Width / 2 - 1]);
                Assert.Equal(220, data[y * Width + Width / 2]);
                Assert.Equal(220, data[y * Width + Width - 1]);
            }
        }

        [Theory]
        [MemberData(nameof(ColorFormats))]
        public void WpfImageToImageInfo_ColorFormats_AveragesChannels(PixelFormat format)
        {
            var raw = Conversions.WpfImageToImageInfo(MakeColorSource(format));

            Assert.Equal(Width, raw.Width);
            Assert.Equal(Height, raw.Height);
            Assert.Equal(8, raw.PixelDepth);
            Assert.Equal(500, raw.Resolution);
            AssertTwoTone(raw.Data);
        }

        [Theory]
        [MemberData(nameof(ColorFormats))]
        public void ToGray8BitmapSource_ColorFormats_AveragesChannels(PixelFormat format)
        {
            var gray = Conversions.ToGray8BitmapSource(MakeColorSource(format));

            Assert.Equal(PixelFormats.Gray8, gray.Format);
            AssertTwoTone(Conversions.WpfImageToImageInfo(gray).Data);
        }

        [Fact]
        public void WpfImageToImageInfo_IndexedWithNonGrayPalette_UsesPaletteColors()
        {
            var raw = Conversions.WpfImageToImageInfo(MakeIndexedSource());

            Assert.All(raw.Data, value => Assert.Equal(245, value));
        }

        [Fact]
        public void ToGray8BitmapSource_IndexedWithNonGrayPalette_UsesPaletteColors()
        {
            var gray = Conversions.ToGray8BitmapSource(MakeIndexedSource());

            Assert.Equal(PixelFormats.Gray8, gray.Format);
            Assert.All(Conversions.WpfImageToImageInfo(gray).Data, value => Assert.Equal(245, value));
        }

        [Fact]
        public void ImageInfoToWpfImage_RoundTripsPixels()
        {
            var data = Enumerable.Range(0, Width * Height).Select(i => (byte)(i * 5)).ToArray();
            var info = new RawImageData { Width = Width, Height = Height, PixelDepth = 8, Resolution = 500, Data = data };

            var image = Conversions.ImageInfoToWpfImage(info);

            Assert.Equal(Width, image.PixelWidth);
            Assert.Equal(Height, image.PixelHeight);
            Assert.Equal(data, Conversions.WpfImageToImageInfo(image).Data);
        }

        // ── Invalid arguments ─────────────────────────────────────────────────

        private static BitmapSource MakeGray16Source() =>
            BitmapSource.Create(4, 4, 96, 96, PixelFormats.Gray16, null, new byte[4 * 4 * 2], 8);

        [Fact]
        public void WpfImageToImageInfo_Null_ReturnsEmpty()
        {
            Assert.True(Conversions.WpfImageToImageInfo(null).IsEmpty);
        }

        [Fact]
        public void WpfConversions_UnsupportedPixelFormat_Throw()
        {
            Assert.Throws<NotSupportedException>(() => Conversions.WpfImageToImageInfo(MakeGray16Source()));
            Assert.Throws<NotSupportedException>(() => Conversions.ToGray8BitmapSource(MakeGray16Source()));
        }

        [Fact]
        public void ToGray8BitmapSource_Gray8Input_IsReturnedUnchanged()
        {
            var source = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Gray8, null, new byte[16], 4);

            Assert.Same(source, Conversions.ToGray8BitmapSource(source));
        }

        [Fact]
        public void WpfConversions_NullOrInvalidImage_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => Conversions.ToGray8BitmapSource(null));
            Assert.Throws<ArgumentNullException>(() => Conversions.ImageInfoToWpfImage(null));
            Assert.Throws<ArgumentException>(() => Conversions.ImageInfoToWpfImage(new RawImageData()));
            Assert.Throws<NotSupportedException>(() => Conversions.ImageInfoToWpfImage(
                new RawImageData { Width = 4, Height = 4, PixelDepth = 16, Data = new byte[32] }));
            Assert.Throws<ArgumentNullException>(() => new WsqEncoder().EncodeWpf(null));
            Assert.Throws<ArgumentNullException>(() => new WsqDecoder().DecodeWpf(null));
        }

        [Fact]
        public void ImageInfoToWpfImage_WithoutResolution_Uses96Dpi()
        {
            var info = new RawImageData { Width = 4, Height = 4, PixelDepth = 8, Resolution = 0, Data = new byte[16] };

            Assert.Equal(96, Conversions.ImageInfoToWpfImage(info).DpiX, 3);
        }

        [Fact]
        public void EncodeWpf_ColorSource_RoundTripsDimensions()
        {
            const int size = 96;
            var pixels = Enumerable.Range(0, size * size * 3).Select(i => (byte)(i / 3 * 7)).ToArray();
            var source = BitmapSource.Create(size, size, 500, 500, PixelFormats.Bgr24, null, pixels, size * 3);

            var wsq = new WsqEncoder().EncodeWpf(source);
            var decoded = new WsqDecoder().DecodeWpf(wsq);

            Assert.Equal(size, decoded.PixelWidth);
            Assert.Equal(size, decoded.PixelHeight);
        }
    }
}
