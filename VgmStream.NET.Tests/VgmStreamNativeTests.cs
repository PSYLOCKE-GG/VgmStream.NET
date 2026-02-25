using VgmStream.NET;
using Xunit;

namespace VgmStream.NET.Tests;

/// <summary>
/// Tests that verify the native libvgmstream library loads correctly and all symbols resolve.
/// These tests require the native binaries (libvgmstream.dll/.so/.dylib) to be present
/// in the runtimes directory — they are expected to run in CI after the native build step.
/// </summary>
public class VgmStreamNativeTests
{
    [Fact]
    public void IsAvailable_WithNativeBinary_ReturnsTrue()
    {
        Assert.True(
            VgmStreamNative.IsAvailable,
            "libvgmstream should be loadable. Ensure the native binary is built and " +
            "placed in runtimes/{rid}/native/. Check for undefined symbols with: " +
            "nm -D --undefined-only libvgmstream.so");
    }

    [Fact]
    public void GetVersion_ReturnsNonZero()
    {
        Assert.True(VgmStreamNative.IsAvailable, "Native library must be available for this test");

        uint version = VgmStream.GetVersion();
        Assert.NotEqual(0u, version);
    }

    [Fact]
    public void GetExtensions_ReturnsNonEmpty()
    {
        Assert.True(VgmStreamNative.IsAvailable, "Native library must be available for this test");

        string[] exts = VgmStream.GetExtensions();
        Assert.NotEmpty(exts);
        Assert.All(exts, ext => Assert.False(string.IsNullOrEmpty(ext)));
    }

    [Fact]
    public void GetCommonExtensions_ReturnsNonEmpty()
    {
        Assert.True(VgmStreamNative.IsAvailable, "Native library must be available for this test");

        string[] exts = VgmStream.GetCommonExtensions();
        Assert.NotEmpty(exts);
        Assert.Contains(exts, e => e == "ogg" || e == "wav" || e == "mp3");
    }
}
