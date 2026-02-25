namespace VgmStream.NET;

/// <summary>
/// Configuration for vgmstream playback behavior.
/// Maps to libvgmstream_config_t.
/// </summary>
public record VgmStreamConfig
{
    public bool DisableConfigOverride { get; init; }
    public bool AllowPlayForever { get; init; }
    public bool PlayForever { get; init; }
    public bool IgnoreLoop { get; init; }
    public bool ForceLoop { get; init; }
    public bool ReallyForceLoop { get; init; }
    public bool IgnoreFade { get; init; }
    public double LoopCount { get; init; } = 2.0;
    public double FadeTime { get; init; } = 10.0;
    public double FadeDelay { get; init; }
    public int StereoTrack { get; init; }
    public int AutoDownmixChannels { get; init; }
    public SampleFormat? ForceSampleFormat { get; init; }
}
