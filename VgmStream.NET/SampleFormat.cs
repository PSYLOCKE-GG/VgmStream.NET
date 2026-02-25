namespace VgmStream.NET;

/// <summary>
/// Audio sample formats supported by vgmstream.
/// Maps to libvgmstream_sfmt_t.
/// </summary>
public enum SampleFormat
{
    Pcm16 = 1,
    Pcm24 = 2,
    Pcm32 = 3,
    Float = 4,
}
