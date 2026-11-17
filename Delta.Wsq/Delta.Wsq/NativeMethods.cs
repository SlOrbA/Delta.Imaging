using System;
using System.Runtime.InteropServices;

namespace Delta.Wsq
{
    internal static class NativeMethods
    {
        //private const string prefix = "nistbio";
        // The same DLL names in Debug and Release builds of this assembly, and in
        // all configurations of nbis.vcxproj: releases ship nbis32.dll / nbis64.dll
        // only. To debug the native code, build a Debug configuration of nbis.
        private const string x86Dll = "nbis32.dll";
        private const string x64Dll = "nbis64.dll";

        /// <summary>
        /// The native library for a process architecture. There are builds for
        /// x86 and x64 only; e.g. an ARM64 process cannot load either of them.
        /// </summary>
        internal static string GetLibraryName(Architecture architecture)
        {
            switch (architecture)
            {
                case Architecture.X86: return x86Dll;
                case Architecture.X64: return x64Dll;
                default:
                    throw new PlatformNotSupportedException(string.Format(
                        "Delta.Wsq has native codec libraries for x86 and x64 processes only; this process is {0}.",
                        architecture));
            }
        }

        private static bool Use64BitLibrary
        {
            get { return GetLibraryName(RuntimeInformation.ProcessArchitecture) == x64Dll; }
        }

        // NBIS keeps its codec state in global variables (see nbis/include/wsq.h),
        // so calls into the DLL must never run concurrently.
        private static readonly object nativeLock = new object();

        // The nbis DLL exports are __cdecl (see nbis/nistwsq.h). DllImport defaults
        // to StdCall, which on x86 leaves the arguments on the stack after every
        // call (PInvokeStackImbalance). On x64 there is only one convention.

        #region x86

        [DllImport(x86Dll, EntryPoint = "wsq_get_comments", CallingConvention = CallingConvention.Cdecl)]
        private static extern int wsq_get_comments32(
            out IntPtr commentData,
            out int commentsCount,
            IntPtr inputData,
            int inputLength);

        [DllImport(x86Dll, EntryPoint = "wsq_decode", CallingConvention = CallingConvention.Cdecl)]
        private static extern int wsq_decode32(
            out IntPtr outputData,
            out int width,
            out int height,
            out int depth,
            out int dpi,
            out int lossyflag,
            IntPtr inputData,
            int inputLength);

        [DllImport(x86Dll, EntryPoint = "wsq_encode", CallingConvention = CallingConvention.Cdecl)]
        private static extern int wsq_encode32(
            out IntPtr outputData,
            out int outputLength,
            float bitrate,
            IntPtr inputData,
            int width,
            int height,
            int depth,
            int dpi,
            string comment_text);

        [DllImport(x86Dll, EntryPoint = "free_mem", CallingConvention = CallingConvention.Cdecl)]
        private static extern void free_mem32(IntPtr data);

        #endregion

        #region x64

        [DllImport(x64Dll, EntryPoint = "wsq_get_comments", CallingConvention = CallingConvention.Cdecl)]
        private static extern int wsq_get_comments64(
            out IntPtr commentData,
            out int commentsCount,
            IntPtr inputData,
            int inputLength);

        [DllImport(x64Dll, EntryPoint = "wsq_decode", CallingConvention = CallingConvention.Cdecl)]
        private static extern int wsq_decode64(
            out IntPtr outputData,
            out int width,
            out int height,
            out int depth,
            out int dpi,
            out int lossyflag,
            IntPtr inputData,
            int inputLength);

        [DllImport(x64Dll, EntryPoint = "wsq_encode", CallingConvention = CallingConvention.Cdecl)]
        private static extern int wsq_encode64(
            out IntPtr outputData,
            out int outputLength,
            float bitrate,
            IntPtr inputData,
            int width,
            int height,
            int depth,
            int dpi,
            string comment_text);

        [DllImport(x64Dll, EntryPoint = "free_mem", CallingConvention = CallingConvention.Cdecl)]
        private static extern void free_mem64(IntPtr data);

        #endregion

        public static int wsq_decode(
            out IntPtr outputData,
            out int width,
            out int height,
            out int depth,
            out int dpi,
            out int lossyflag,
            IntPtr inputData,
            int inputLength)
        {
            lock (nativeLock)
            {
                return Use64BitLibrary ?
                    wsq_decode64(out outputData, out width, out height, out depth, out dpi, out lossyflag, inputData, inputLength) :
                    wsq_decode32(out outputData, out width, out height, out depth, out dpi, out lossyflag, inputData, inputLength);
            }
        }

        public static int wsq_get_comments(
            out IntPtr commentData,
            out int commentsCount,
            IntPtr inputData,
            int inputLength)
        {
            lock (nativeLock)
            {
                return Use64BitLibrary ?
                    wsq_get_comments64(out commentData, out commentsCount, inputData, inputLength) :
                    wsq_get_comments32(out commentData, out commentsCount, inputData, inputLength);
            }
        }

        public static int wsq_encode(
            out IntPtr outputData,
            out int outputLength,
            float bitrate,
            IntPtr inputData,
            int width,
            int height,
            int depth,
            int dpi,
            string comment_text)
        {
            lock (nativeLock)
            {
                return Use64BitLibrary ?
                    wsq_encode64(out outputData, out outputLength, bitrate, inputData, width, height, depth, dpi, comment_text) :
                    wsq_encode32(out outputData, out outputLength, bitrate, inputData, width, height, depth, dpi, comment_text);
            }
        }

        public static void free_mem(IntPtr data)
        {
            lock (nativeLock)
            {
                if (Use64BitLibrary)
                    free_mem64(data);
                else free_mem32(data);
            }
        }
    }
}
