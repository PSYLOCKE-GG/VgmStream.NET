using VgmStream.NET;
using Xunit;

namespace VgmStream.NET.Tests;

public class VgmStreamTests
{
    [Fact]
    public void SampleFormat_EnumValues_MatchNative()
    {
        Assert.Equal(1, (int)SampleFormat.Pcm16);
        Assert.Equal(2, (int)SampleFormat.Pcm24);
        Assert.Equal(3, (int)SampleFormat.Pcm32);
        Assert.Equal(4, (int)SampleFormat.Float);
    }

    [Fact]
    public void VgmStreamConfig_Defaults_AreCorrect()
    {
        var config = new VgmStreamConfig();

        Assert.False(config.PlayForever);
        Assert.False(config.IgnoreLoop);
        Assert.False(config.ForceLoop);
        Assert.Equal(1.0, config.LoopCount);
        Assert.Equal(10.0, config.FadeTime);
        Assert.Equal(0.0, config.FadeDelay);
        Assert.Equal(0, config.AutoDownmixChannels);
        Assert.Null(config.ForceSampleFormat);
    }

    [SkippableFact]
    public void IsValid_KnownExtension_ReturnsTrue()
    {
        Skip.IfNot(VgmStreamNative.IsAvailable, "Native library not available");

        Assert.True(VgmStream.IsValid("test.adx"));
    }

    [SkippableFact]
    public void IsValid_UnknownExtension_ReturnsFalse()
    {
        Skip.IfNot(VgmStreamNative.IsAvailable, "Native library not available");

        Assert.False(VgmStream.IsValid("test.xyz123"));
    }
}
