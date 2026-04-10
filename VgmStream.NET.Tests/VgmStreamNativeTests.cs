using VgmStream.NET;
using Xunit;

namespace VgmStream.NET.Tests;

public class VgmStreamNativeTests
{
    [SkippableFact]
    public void IsAvailable_WithNativeBinary_ReturnsTrue()
    {
        Skip.IfNot(VgmStreamNative.IsAvailable, "Native library not available");

        Assert.True(VgmStreamNative.IsAvailable);
    }

    [SkippableFact]
    public void GetVersion_ReturnsNonZero()
    {
        Skip.IfNot(VgmStreamNative.IsAvailable, "Native library not available");

        uint version = VgmStream.GetVersion();
        Assert.NotEqual(0u, version);
    }

    [SkippableFact]
    public void GetExtensions_ReturnsNonEmpty()
    {
        Skip.IfNot(VgmStreamNative.IsAvailable, "Native library not available");

        string[] exts = VgmStream.GetExtensions();
        Assert.NotEmpty(exts);
        Assert.All(exts, ext => Assert.False(string.IsNullOrEmpty(ext)));
    }

    [SkippableFact]
    public void GetCommonExtensions_ReturnsNonEmpty()
    {
        Skip.IfNot(VgmStreamNative.IsAvailable, "Native library not available");

        string[] exts = VgmStream.GetCommonExtensions();
        Assert.NotEmpty(exts);
        Assert.Contains(exts, e => e == "ogg" || e == "wav" || e == "mp3");
    }
}
