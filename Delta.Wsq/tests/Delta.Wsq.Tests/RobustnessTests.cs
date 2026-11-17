using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace Delta.Wsq.Tests
{
    public class RobustnessTests
    {
        private static byte[] LoadTestWsq() =>
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fp.wsq"));

        private static RawImageData MakeImage(int width, int height, int seed = 0)
        {
            var data = new byte[width * height];
            for (var i = 0; i < data.Length; i++)
                data[i] = (byte)((i * 7 + i / width * 3 + seed * 31) % 256);
            return new RawImageData { Width = width, Height = height, PixelDepth = 8, Resolution = 500, Data = data };
        }

        // ── Input validation (#15) ────────────────────────────────────────────

        [Fact]
        public void Encode_DataShorterThanDimensions_Throws()
        {
            var image = MakeImage(128, 128);
            image.Height = 256; // claims twice the data it has

            Assert.Throws<ArgumentException>(() => WsqCodec.Encode(image, 0.75f, null));
        }

        [Theory]
        [InlineData(0, 128)]
        [InlineData(128, 0)]
        [InlineData(-128, 128)]
        public void Encode_NonPositiveDimensions_Throws(int width, int height)
        {
            var image = MakeImage(128, 128);
            image.Width = width;
            image.Height = height;

            Assert.Throws<ArgumentException>(() => WsqCodec.Encode(image, 0.75f, null));
        }

        [Fact]
        public void Encode_UnsupportedPixelDepth_Throws()
        {
            var image = MakeImage(128, 128);
            image.PixelDepth = 16;

            Assert.Throws<ArgumentException>(() => WsqCodec.Encode(image, 0.75f, null));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void Encode_InvalidBitrate_Throws(float bitrate)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WsqCodec.Encode(MakeImage(128, 128), bitrate, null));
        }

        // ── Determinism, content and thread safety (#16, #26) ─────────────────

        [Fact]
        public void Encode_SameInputTwice_ProducesIdenticalBytes()
        {
            var image = MakeImage(128, 128);

            Assert.Equal(WsqCodec.Encode(image, 0.75f, null), WsqCodec.Encode(image, 0.75f, null));
        }

        [Fact]
        public void RoundTrip_PreservesImageContent()
        {
            var original = WsqCodec.Decode(LoadTestWsq());

            var decoded = WsqCodec.Decode(WsqCodec.Encode(original, 2.25f, null));

            Assert.Equal(original.Data.Length, decoded.Data.Length);
            var meanError = original.Data.Zip(decoded.Data, (a, b) => Math.Abs(a - b)).Average();
            Assert.True(meanError < 10, $"mean absolute pixel error {meanError:F2}");
        }

        [Fact]
        public void EncodeDecode_FromManyThreads_MatchesSequentialResults()
        {
            const int workers = 8;
            const int iterations = 40;
            var wsqFile = LoadTestWsq();
            var images = Enumerable.Range(0, workers).Select(i => MakeImage(128 + 16 * i, 160, i)).ToArray();
            var expectedEncoded = images.Select(i => WsqCodec.Encode(i, 0.75f, "t")).ToArray();
            var expectedDecoded = WsqCodec.Decode(wsqFile).Data;

            Parallel.For(0, workers, new ParallelOptions { MaxDegreeOfParallelism = workers }, worker =>
            {
                for (var n = 0; n < iterations; n++)
                {
                    Assert.Equal(expectedEncoded[worker], WsqCodec.Encode(images[worker], 0.75f, "t"));
                    Assert.Equal(expectedDecoded, WsqCodec.Decode(wsqFile).Data);
                    Assert.Single(WsqCodec.GetComments(expectedEncoded[worker]), c => c == "t");
                }
            });
        }

        // ── Resolution rounding (#18) ─────────────────────────────────────────

        [Fact]
        public void GdiImageToImageInfo_RoundsResolution()
        {
            using var bitmap = new Bitmap(16, 16, PixelFormat.Format8bppIndexed);
            bitmap.SetResolution(499.9998f, 499.9998f); // 500 dpi stored as pixels per metre (PNG)

            Assert.Equal(500, Conversions.GdiImageToImageInfo(bitmap).Resolution);
        }

        [Fact]
        public void WpfImageToImageInfo_RoundsResolution()
        {
            var source = BitmapSource.Create(16, 16, 499.9998, 499.9998, PixelFormats.Gray8, null, new byte[16 * 16], 16);

            Assert.Equal(500, Conversions.WpfImageToImageInfo(source).Resolution);
        }

        // ── Palettes (#19) ────────────────────────────────────────────────────

        [Fact]
        public void To8bppBitmap_ColorInput_ResultHasGrayscalePalette()
        {
            using var input = new Bitmap(16, 16, PixelFormat.Format24bppRgb);

            using var gray = Conversions.To8bppBitmap(input);

            var entries = gray.Palette.Entries;
            Assert.Equal(256, entries.Length);
            for (var i = 0; i < 256; i++)
                Assert.Equal(Color.FromArgb(i, i, i).ToArgb(), entries[i].ToArgb());
        }

        [Fact]
        public void To8bppBitmap_IndexedInputWithNonGrayPalette_UsesPaletteColors()
        {
            const int size = 16;
            using var input = new Bitmap(size, size, PixelFormat.Format8bppIndexed);
            var palette = input.Palette;
            for (var i = 0; i < 256; i++)
                palette.Entries[i] = Color.FromArgb(255 - i, 255 - i, 255 - i); // inverted
            input.Palette = palette;

            // Every pixel uses palette index 10, i.e. gray level 245.
            var bits = input.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            var row = Enumerable.Repeat((byte)10, size).ToArray();
            for (var y = 0; y < size; y++)
                Marshal.Copy(row, 0, bits.Scan0 + y * bits.Stride, size);
            input.UnlockBits(bits);

            using var gray = Conversions.To8bppBitmap(input);
            var raw = Conversions.GdiImageToImageInfo(gray);

            Assert.All(raw.Data, value => Assert.Equal(245, value));
        }
    }
}
