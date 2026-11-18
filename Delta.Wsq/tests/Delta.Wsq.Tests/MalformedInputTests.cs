using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Delta.Wsq.Tests
{
    /// <summary>
    /// Malformed or unsupported input must be rejected without damaging the
    /// native codec: a valid file still decodes afterwards (#14, #20).
    /// </summary>
    public class MalformedInputTests
    {
        private const int DttMarker = 0xFFA4;   // transform table
        private const int DqtMarker = 0xFFA5;   // quantization table
        private const int DhtMarker = 0xFFA6;   // Huffman table
        private const int SofMarker = 0xFFA2;   // frame header
        private const int SobMarker = 0xFFA3;   // start of block

        private static byte[] LoadSample(string name) =>
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, name));

        private static void AssertSampleStillDecodes()
        {
            var image = WsqCodec.Decode(LoadSample("fp.wsq"));
            Assert.Equal(320, image.Width);
            Assert.Equal(480, image.Height);
            Assert.Equal(2, WsqCodec.GetComments(LoadSample("fp.wsq")).Length);
        }

        // Offset of the first segment with the given marker: the marker's own
        // two bytes are at the returned offset, the 2-byte length follows.
        private static int FindSegment(byte[] wsq, int marker)
        {
            var pos = 2; // after SOI
            while (pos + 4 <= wsq.Length)
            {
                var current = (wsq[pos] << 8) | wsq[pos + 1];
                if (current == marker) return pos;
                if (current == SobMarker) break;
                pos += 2 + ((wsq[pos + 2] << 8) | wsq[pos + 3]);
            }

            throw new InvalidOperationException($"Marker {marker:X4} not found.");
        }

        [Theory]
        [InlineData("fp.wsq", 320, 480)]
        [InlineData("finger.wsq", 320, 480)]
        [InlineData("nores.wsq", 500, 500)]
        public void Decode_SampleFiles_ReturnsImage(string name, int width, int height)
        {
            var image = WsqCodec.Decode(LoadSample(name));

            Assert.Equal(width, image.Width);
            Assert.Equal(height, image.Height);
            Assert.Equal(width * height, image.Data.Length);
        }

        [Theory]
        [InlineData(32, 128)]
        [InlineData(128, 32)]
        [InlineData(1, 1)]
        public void Encode_ImageSmallerThanMinimum_Throws(int width, int height)
        {
            var image = new RawImageData
            {
                Width = width, Height = height, PixelDepth = 8, Resolution = 500, Data = new byte[width * height]
            };

            Assert.Throws<ArgumentException>(() => WsqCodec.Encode(image, 0.75f, null));
        }

        private static void AssertFails(int expectedErrorCode, Action action)
        {
            var exception = Assert.Throws<WsqException>(action);
            Assert.Equal(expectedErrorCode, exception.ErrorCode);
            Assert.Contains(expectedErrorCode.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message);
        }

        [Theory]
        [InlineData(WsqException.InvalidArgument, "invalid argument")]
        [InlineData(WsqException.ImageTooSmall, "minimum WSQ image size")]
        [InlineData(WsqException.MalformedStream, "truncated or damaged")]
        [InlineData(WsqException.OutOfMemory, "out of memory")]
        [InlineData(-57, "NBIS codec reported an error")]
        public void WsqException_Message_NamesTheOperationReasonAndCode(int errorCode, string reason)
        {
            var exception = WsqException.For("decoding", errorCode);

            Assert.Equal(errorCode, exception.ErrorCode);
            Assert.StartsWith("WSQ decoding failed: ", exception.Message);
            Assert.Contains(reason, exception.Message);
            Assert.Contains(errorCode.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message);
        }

        [Fact]
        public void Decode_StreamOfTooSmallImage_Throws()
        {
            // Rewrite the frame header of a valid file to claim a 16 x 16 image.
            var wsq = LoadSample("fp.wsq");
            var frame = FindSegment(wsq, SofMarker) + 4;        // black, white, height, width
            wsq[frame + 2] = 0; wsq[frame + 3] = 16;            // height
            wsq[frame + 4] = 0; wsq[frame + 5] = 16;            // width

            AssertFails(WsqException.ImageTooSmall, () => WsqCodec.Decode(wsq));
            AssertSampleStillDecodes();
        }

        [Fact]
        public void Decode_TruncatedAnywhereInTheHeaders_Throws()
        {
            var wsq = LoadSample("fp.wsq");
            // Up to and including the header of the first block (marker,
            // length, Huffman table id), which then has no data.
            var headersEnd = FindSegment(wsq, SobMarker) + 5;

            for (var length = 1; length <= headersEnd; length++)
            {
                var truncated = wsq.Take(length).ToArray();
                var exception = Assert.Throws<WsqException>(() => WsqCodec.Decode(truncated));
                Assert.True(exception.ErrorCode != 0, $"truncated to {length} bytes");
            }

            AssertSampleStillDecodes();
        }

        [Fact]
        public void Decode_TransformTableWithZeroFilterLength_Throws()
        {
            var wsq = LoadSample("fp.wsq");
            wsq[FindSegment(wsq, DttMarker) + 5] = 0; // low-pass filter length

            AssertFails(WsqException.MalformedStream, () => WsqCodec.Decode(wsq));
            AssertSampleStillDecodes();
        }

        [Fact]
        public void Decode_HuffmanTableIdOutOfRange_Throws()
        {
            var wsq = LoadSample("fp.wsq");
            wsq[FindSegment(wsq, DhtMarker) + 4] = 0xFF; // table id

            AssertFails(WsqException.MalformedStream, () => WsqCodec.Decode(wsq));
            AssertSampleStillDecodes();
        }

        [Fact]
        public void Decode_BlockHuffmanTableIdOutOfRange_Throws()
        {
            var wsq = LoadSample("fp.wsq");
            wsq[FindSegment(wsq, SobMarker) + 4] = 0xFF; // table id of the first block

            AssertFails(WsqException.MalformedStream, () => WsqCodec.Decode(wsq));
            AssertSampleStillDecodes();
        }

        [Fact]
        public void Decode_StreamWithoutQuantizationTable_Throws()
        {
            // NBIS would silently reuse the table of the previously decoded image.
            AssertSampleStillDecodes();
            var wsq = LoadSample("fp.wsq");
            var start = FindSegment(wsq, DqtMarker);
            var segmentLength = 2 + ((wsq[start + 2] << 8) | wsq[start + 3]);
            var withoutTable = wsq.Take(start).Concat(wsq.Skip(start + segmentLength)).ToArray();

            AssertFails(WsqException.MalformedStream, () => WsqCodec.Decode(withoutTable));
            Assert.Equal(2, WsqCodec.GetComments(withoutTable).Length); // comments are still readable
            AssertSampleStillDecodes();
        }

        // ── Damage that only shows while decoding (#32) ──────────────────────
        // Found by the fuzzer in Delta.Wsq/nbis/fuzz: each of these made the
        // unpatched NBIS decoder access memory outside its buffers.

        // Decodes damaged data: it may decode (to a wrong image) or be
        // rejected, but must not take the process down.
        private static void DecodeDamaged(byte[] wsq)
        {
            try
            {
                var image = WsqCodec.Decode(wsq);
                Assert.Equal(image.Width * image.Height, image.Data.Length);
            }
            catch (WsqException exception)
            {
                Assert.NotEqual(0, exception.ErrorCode);
            }
        }

        [Theory]
        [InlineData(6)]     // high byte of the height
        [InlineData(8)]     // high byte of the width
        public void Decode_FrameDimensionsFarBeyondTheData_Throws(int offsetInFrameHeader)
        {
            var wsq = LoadSample("fp.wsq");
            wsq[FindSegment(wsq, SofMarker) + offsetInFrameHeader] = 0xFF;

            Assert.Throws<WsqException>(() => WsqCodec.Decode(wsq));
            AssertSampleStillDecodes();
        }

        [Theory]
        [InlineData(SobMarker)]     // block header: the next byte becomes a Huffman table id
        [InlineData(DttMarker)]     // transform table read from compressed data
        [InlineData(DqtMarker)]
        [InlineData(DhtMarker)]
        public void Decode_MarkerInsideTheCompressedData_DoesNotCrash(int marker)
        {
            var original = LoadSample("fp.wsq");
            var dataStart = FindSegment(original, SobMarker) + 5;

            for (var offset = dataStart; offset < original.Length - 1; offset += 97)
            {
                var wsq = (byte[])original.Clone();
                wsq[offset] = (byte)(marker >> 8);
                wsq[offset + 1] = (byte)marker;
                DecodeDamaged(wsq);
            }

            AssertSampleStillDecodes();
        }

        [Fact]
        public void Decode_DamagedCompressedData_DoesNotCrash()
        {
            var original = LoadSample("fp.wsq");
            var dataStart = FindSegment(original, SobMarker) + 5;
            var random = new Random(32);

            for (var i = 0; i < 300; i++)
            {
                var wsq = (byte[])original.Clone();
                var offset = random.Next(dataStart, wsq.Length);
                var length = random.Next(1, 16);
                for (var k = offset; k < offset + length && k < wsq.Length; k++)
                    wsq[k] = (byte)random.Next(256);
                DecodeDamaged(wsq);
            }

            AssertSampleStillDecodes();
        }

        [Fact]
        public void GetComments_TruncatedAnywhereInTheHeaders_Throws()
        {
            var wsq = LoadSample("fp.wsq");
            var headersEnd = FindSegment(wsq, SobMarker);

            // Up to the last header byte the stream is incomplete, also when
            // the comments themselves (at the start of the file) are intact.
            for (var length = 1; length < headersEnd; length++)
            {
                var truncated = wsq.Take(length).ToArray();
                AssertFails(WsqException.MalformedStream, () => WsqCodec.GetComments(truncated));
            }

            AssertSampleStillDecodes();
        }
    }
}
