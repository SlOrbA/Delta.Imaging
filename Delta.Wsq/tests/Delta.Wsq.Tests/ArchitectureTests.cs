using System;
using System.Runtime.InteropServices;
using Xunit;

namespace Delta.Wsq.Tests
{
    public class ArchitectureTests
    {
        [Theory]
        [InlineData(Architecture.X86, "nbis32.dll")]
        [InlineData(Architecture.X64, "nbis64.dll")]
        public void GetLibraryName_SupportedArchitecture_ReturnsItsLibrary(Architecture architecture, string library)
        {
            Assert.Equal(library, NativeMethods.GetLibraryName(architecture));
        }

        [Theory]
        [InlineData(Architecture.Arm64)]
        [InlineData(Architecture.Arm)]
        public void GetLibraryName_UnsupportedArchitecture_Throws(Architecture architecture)
        {
            var ex = Assert.Throws<PlatformNotSupportedException>(() => NativeMethods.GetLibraryName(architecture));
            Assert.Contains(architecture.ToString(), ex.Message);
        }
    }
}
