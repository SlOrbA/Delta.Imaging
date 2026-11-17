using System;

namespace Delta.Wsq
{
    partial class WsqCodec
    {
        public static class Constants
        {
            /// <summary>
            /// Default Bit rate recommended by FBI specs
            /// </summary>
            public const float DefaultBitrate = 0.75f;

            public const float MinCompressionRatio = 3f;
            public const float MaxCompressionRatio = 100f;

            /// <summary>
            /// Smallest image width and height (in pixels) the codec supports.
            /// The wavelet decomposition of the underlying NBIS implementation
            /// does not work on smaller images.
            /// </summary>
            public const int MinImageSize = 33;

            public const int MinQuality = 0;
            public const int MaxQuality = 100;

            public static readonly float DefaultCompressionRatio;
            public static readonly int DefaultQuality;

            /// <summary>
            /// Initializes the <see cref="Constants"/> class.
            /// </summary>
            static Constants()
            {
                DefaultCompressionRatio = WsqCodec.BitrateToCompressionRatio(DefaultBitrate);
                DefaultQuality = WsqCodec.CompressionRatioToQuality(DefaultCompressionRatio);
            }
        }

        // Helper conversion methods:
        // 
        // Bitrate = 0.75 --> Cr = 15
        // Bitrate = 2.25 --> Cr = 5
        //
        // Note that the relation between bitrate and compression ratio is only a hint.
        // Given a bit rate, the real compression ratio may differ.

        public static float CompressionRatioToBitrate(float compressionRatio)
        {
            var cr = SafeCompressionRatio(compressionRatio);
            return 11.25f / cr;
        }

        public static float BitrateToCompressionRatio(float bitrate)
        {
            return SafeCompressionRatio(11.25f / bitrate);
        }

        // We define a quality in % based on the following considerations:
        // Cr between 10:1 and 20:1 produces very good quality images (read here: 
        // http://books.google.fr/books?id=D0OauYNF3bAC&lpg=PA404&ots=W-DXIxvasW&dq=wsq%20compression%20ratio%20quality&pg=PA404#v=onepage&q=wsq%20compression%20ratio%20quality&f=false
        // High compression rates such as 60:1 or 120:1 are possible but degrade the image. We'll fix the upper compression rate to 100.
        // On the other hand, Cr of 1:1 make the encoder raise an error. Cr of 2:1 produces strange artifacts. So the minimum Cr should be 3.
        // Therefore, we map (reversed) a quality in percents [0..100] to the Compression ratio range [3..100]:
        // Qmax = 100 --> Crmin = 3
        // Qmin = 0 --> Crmax = 100
        // From this, we deduce the coefficient of the linear relation giving Cr from Q (Cr = a * Q + b):
        // a = (Crmin - Crmax) / (Qmax - Qmin)
        // b = Crmin - Qmax * a
        //
        // The reversed relation (Q = a * Cr + b) gives:
        // a = (Qmin - Qmax) / (Crmax - Crmin)
        // b = Qmin - Crmax * a
        //
        // By fixing Qmin and Qmax to 0 and 100, we get, for the Cr = f(Q) relation:
        // a = (Crmin - Crmax) / 100
        // b = Crmax
        // 
        // And for Q = f(Cr):
        // a = 100 / (Crmin - Crmax)
        // b = -Crmax * a

        // q = a * cr + b
        private static readonly float qa = 100f / (Constants.MinCompressionRatio - Constants.MaxCompressionRatio);
        private static readonly float qb = -Constants.MaxCompressionRatio * qa;

        // cr = a * q + b
        private static readonly float cra = (Constants.MinCompressionRatio - Constants.MaxCompressionRatio) / 100f;
        private static readonly float crb = Constants.MaxCompressionRatio;


        // The reversed relation gives:
        // Q = a * Cr + b with a = -99/97 and b = 1 - 100 * a
                
        public static float QualityToCompressionRatio(int quality)
        {
            var q = (float)SafeQuality(quality);
            return cra * q + crb;
        }

        public static int CompressionRatioToQuality(float compressionRatio)
        {
            var cr = SafeCompressionRatio(compressionRatio);
            return (int)Math.Round(qa * cr + qb);
        }

        // Quality is clamped to [0;100] and the compression ratio to [3;100]:
        // a ratio of 1:1 makes the codec fail (memory allocation error) and
        // 2:1 produces strange artifacts, so quality 100 maps to 3:1.

        private static int SafeQuality(int quality)
        {
            return Clamp(quality, Constants.MinQuality, Constants.MaxQuality);
        }

        private static float SafeCompressionRatio(float compressionRatio)
        {
            return Clamp(compressionRatio, Constants.MinCompressionRatio, Constants.MaxCompressionRatio);
        }

        private static int Clamp(int i, int min, int max)
        {
            return i < min ? min : (i > max ? max : i);
        }
        
        private static float Clamp(float i, float min, float max)
        {
            return i < min ? min : (i > max ? max : i);
        }
    }
}
