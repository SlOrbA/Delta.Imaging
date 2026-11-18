using System;
using System.Globalization;

namespace Delta.Wsq
{
    /// <summary>
    /// The native WSQ codec could not encode, decode or read the data it was given.
    /// </summary>
    public class WsqException : Exception
    {
        /// <summary>A required argument was missing (reported by the nbis DLL).</summary>
        public const int InvalidArgument = -9001;

        /// <summary>The image is smaller than <see cref="WsqCodec.Constants.MinImageSize"/> in either dimension.</summary>
        public const int ImageTooSmall = -9002;

        /// <summary>The WSQ data is truncated or its headers are damaged.</summary>
        public const int MalformedStream = -9003;

        /// <summary>The nbis DLL could not allocate memory.</summary>
        public const int OutOfMemory = -9004;

        public WsqException(string message, int errorCode)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// The error code returned by the nbis DLL: one of the constants of this class, or
        /// the code of the NBIS function that failed.
        /// </summary>
        public int ErrorCode { get; }

        internal static WsqException For(string operation, int errorCode)
        {
            return new WsqException(string.Format(CultureInfo.InvariantCulture,
                "WSQ {0} failed: {1} (error code {2}).", operation, Describe(errorCode), errorCode), errorCode);
        }

        private static string Describe(int errorCode)
        {
            switch (errorCode)
            {
                case InvalidArgument: return "invalid argument";
                case ImageTooSmall: return "the image is smaller than the minimum WSQ image size";
                case MalformedStream: return "the WSQ data is truncated or damaged";
                case OutOfMemory: return "out of memory";
                default: return "the NBIS codec reported an error";
            }
        }
    }
}
