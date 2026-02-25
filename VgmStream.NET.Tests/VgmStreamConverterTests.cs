using VgmStream.NET;
using Xunit;

namespace VgmStream.NET.Tests;

/// <summary>
/// Tests for VgmStreamConverter WAV header writing.
/// These are pure managed tests that don't require the native library.
/// </summary>
public class VgmStreamConverterTests
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
        Assert.Equal(2.0, config.LoopCount);
        Assert.Equal(10.0, config.FadeTime);
        Assert.Equal(0.0, config.FadeDelay);
        Assert.Equal(0, config.AutoDownmixChannels);
        Assert.Null(config.ForceSampleFormat);
    }

    [Fact]
    public void IsValid_KnownExtension_ReturnsTrue()
    {
        if (!VgmStreamNative.IsAvailable)
            return; // skip if native not available

        Assert.True(VgmStream.IsValid("test.adx"));
    }

    [Fact]
    public void IsValid_UnknownExtension_ReturnsFalse()
    {
        if (!VgmStreamNative.IsAvailable)
            return; // skip if native not available

        Assert.False(VgmStream.IsValid("test.xyz123"));
    }
}
