using System;
using System.Runtime.InteropServices;

namespace Delta.Wsq
{
    internal static class NativeMethods
    {
        //private const string prefix = "nistbio";
        // The same DLL names in Debug and Release builds of this assembly: only
        // nbis32.dll / nbis64.dll are built by CI and shipped in releases. To
        // debug the native code, copy a Debug build of nbis over them.
        private const string x86Dll = "nbis32.dll";
        private const string x64Dll = "nbis64.dll";

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
                return IntPtr.Size == 8 ?
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
                return IntPtr.Size == 8 ?
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
                return IntPtr.Size == 8 ?
                    wsq_encode64(out outputData, out outputLength, bitrate, inputData, width, height, depth, dpi, comment_text) :
                    wsq_encode32(out outputData, out outputLength, bitrate, inputData, width, height, depth, dpi, comment_text);
            }
        }

        public static void free_mem(IntPtr data)
        {
            lock (nativeLock)
            {
                if (IntPtr.Size == 8)
                    free_mem64(data);
                else free_mem32(data);
            }
        }
    }
}
