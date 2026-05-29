using System;
using System.IO;
using Xunit;

namespace Delta.Wsq.Tests
{
    public class WsqCodecTests
    {
        // WSQ SOI (Start Of Image) marker bytes: 0xFF 0xA0
        private const byte SoiByte0 = 0xFF;
        private const byte SoiByte1 = 0xA0;

        // Properties of the bundled test file (fp.wsq)
        private const int TestImageWidth = 320;
        private const int TestImageHeight = 480;
        private const int TestImageDepth = 8;

        private static byte[] LoadTestWsq() =>
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fp.wsq"));

        // ── Decode ────────────────────────────────────────────────────────────

        [Fact]
        public void Decode_KnownFile_ReturnsCorrectDimensions()
        {
            var result = WsqCodec.Decode(LoadTestWsq());

            Assert.False(result.IsEmpty);
            Assert.Equal(TestImageWidth, result.Width);
            Assert.Equal(TestImageHeight, result.Height);
            Assert.Equal(TestImageDepth, result.PixelDepth);
            Assert.Equal(TestImageWidth * TestImageHeight, result.Data.Length);
        }

        [Fact]
        public void Decode_NullInput_ReturnsEmpty()
        {
            var result = WsqCodec.Decode(null);
            Assert.True(result.IsEmpty);
        }

        [Fact]
        public void Decode_EmptyInput_ReturnsEmpty()
        {
            var result = WsqCodec.Decode(Array.Empty<byte>());
            Assert.True(result.IsEmpty);
        }

        // ── Encode ────────────────────────────────────────────────────────────

        [Fact]
        public void Encode_ValidData_StartsWithWsqSoiMarker()
        {
            var raw = MakeSyntheticImage(64, 64);

            var wsq = WsqCodec.Encode(raw, 0.75f, null);

            Assert.NotNull(wsq);
            Assert.True(wsq.Length > 4);
            Assert.Equal(SoiByte0, wsq[0]);
            Assert.Equal(SoiByte1, wsq[1]);
        }

        [Fact]
        public void Encode_NullInput_ReturnsEmptyArray()
        {
            var result = WsqCodec.Encode(null, 0.75f, null);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void Encode_EmptyInput_ReturnsEmptyArray()
        {
            var result = WsqCodec.Encode(RawImageData.Empty, 0.75f, null);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        // ── Round-trip ────────────────────────────────────────────────────────

        [Fact]
        public void RoundTrip_EncodeDecodePreservesDimensions()
        {
            var original = WsqCodec.Decode(LoadTestWsq());
            Assert.False(original.IsEmpty);

            var encoded = WsqCodec.Encode(original, 0.75f, null);
            Assert.NotNull(encoded);
            Assert.NotEmpty(encoded);

            var roundtripped = WsqCodec.Decode(encoded);
            Assert.Equal(original.Width, roundtripped.Width);
            Assert.Equal(original.Height, roundtripped.Height);
            Assert.Equal(original.PixelDepth, roundtripped.PixelDepth);
            Assert.Equal(original.Data.Length, roundtripped.Data.Length);
        }

        // ── GetComments ───────────────────────────────────────────────────────

        [Fact]
        public void GetComments_KnownFile_ReturnsNistComment()
        {
            var comments = WsqCodec.GetComments(LoadTestWsq());

            Assert.NotEmpty(comments);
            Assert.Contains(comments, c => c.Contains("PIX_WIDTH 320"));
        }

        [Fact]
        public void GetComments_EncodedWithComment_ReturnsComment()
        {
            const string expected = "hello wsq test";
            var raw = MakeSyntheticImage(64, 64);
            var encoded = WsqCodec.Encode(raw, 0.75f, expected);

            var comments = WsqCodec.GetComments(encoded);

            Assert.Contains(comments, c => c.Contains(expected));
        }

        [Fact]
        public void GetComments_NullInput_ReturnsEmptyArray()
        {
            var result = WsqCodec.GetComments(null);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        // ── Edge cases & parametrized tests ───────────────────────────────────

        [Theory]
        [InlineData(0.75f)]
        [InlineData(1.5f)]
        [InlineData(3.0f)]
        [InlineData(5.0f)]
        public void Encode_VariousBitRates_ProducesValidWSQ(float bitrate)
        {
            var raw = MakeSyntheticImage(64, 64);

            var wsq = WsqCodec.Encode(raw, bitrate, null);

            Assert.NotNull(wsq);
            Assert.NotEmpty(wsq);
            Assert.Equal(SoiByte0, wsq[0]);
            Assert.Equal(SoiByte1, wsq[1]);
        }

        [Fact]
        public void Encode_VerySmallImage_Succeeds()
        {
            // 8x8 is minimal valid fingerprint
            var raw = MakeSyntheticImage(8, 8);

            var wsq = WsqCodec.Encode(raw, 0.75f, null);

            Assert.NotNull(wsq);
            Assert.NotEmpty(wsq);
        }

        [Fact]
        public void Encode_LargeImage_Succeeds()
        {
            // 1024x768 fingerprint
            var raw = MakeSyntheticImage(1024, 768);

            var wsq = WsqCodec.Encode(raw, 0.75f, null);

            Assert.NotNull(wsq);
            Assert.NotEmpty(wsq);
        }

        [Fact]
        public void Decode_CorruptedWsqHeader_ReturnsEmpty()
        {
            // Create fake WSQ with bad header
            var corrupted = new byte[] { SoiByte0, SoiByte1, 0xFF, 0xFF, 0x00 };

            var result = WsqCodec.Decode(corrupted);

            // Should return empty instead of crashing
            Assert.True(result.IsEmpty);
        }

        [Fact]
        public void RoundTrip_WithComment_PreservesComment()
        {
            const string testComment = "Test fingerprint comment";
            var original = MakeSyntheticImage(128, 128);

            var encoded = WsqCodec.Encode(original, 0.75f, testComment);
            var comments = WsqCodec.GetComments(encoded);

            Assert.NotEmpty(comments);
            Assert.Contains(comments, c => c.Contains(testComment));
        }

        [Fact]
        public void Encode_MultipleTimesWithSameData_ConsistentResults()
        {
            var raw = MakeSyntheticImage(64, 64);

            var wsq1 = WsqCodec.Encode(raw, 0.75f, null);
            var wsq2 = WsqCodec.Encode(raw, 0.75f, null);

            // Both should produce valid WSQ
            Assert.NotNull(wsq1);
            Assert.NotNull(wsq2);
            Assert.NotEmpty(wsq1);
            Assert.NotEmpty(wsq2);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static RawImageData MakeSyntheticImage(int width, int height)
        {
            var data = new byte[width * height];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(i % 256);

            return new RawImageData
            {
                Width = width,
                Height = height,
                PixelDepth = 8,
                Resolution = 500,
                Data = data
            };
        }
    }
}
