using System.Runtime.InteropServices;
using System.Text;

namespace VgmStream.NET;

/// <summary>
/// High-level wrapper for vgmstream audio decoding.
/// Provides an IDisposable API for opening, querying, and decoding game audio files.
/// </summary>
public sealed unsafe class VgmStream : IDisposable
{
    private readonly VgmStreamHandle _handle;
    private bool _disposed;

    /// <summary>
    /// Opens a game audio file for decoding.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="subsong">Subsong index (0 = default/first, 1..N = specific subsong).</param>
    /// <param name="config">Optional playback configuration.</param>
    public VgmStream(string filePath, int subsong = 0, VgmStreamConfig? config = null)
    {
        VgmStreamNative.EnsureLoadedPublic();

        nint lib = VgmStreamNative.LibvgmstreamInit();
        if (lib == 0)
            throw new InvalidOperationException("Failed to initialize vgmstream context.");

        _handle = new VgmStreamHandle(lib);

        try
        {
            // Apply config if provided
            if (config != null)
            {
                var nativeCfg = ConfigToNative(config);
                VgmStreamNative.LibvgmstreamSetup(lib, &nativeCfg);
            }

            // Open streamfile from path
            byte[] pathBytes = Encoding.UTF8.GetBytes(filePath + '\0');
            nint sf;
            fixed (byte* pPath = pathBytes)
            {
                sf = VgmStreamNative.LibstreamfileOpenFromStdio(pPath);
            }

            if (sf == 0)
                throw new FileNotFoundException("Failed to open streamfile.", filePath);

            try
            {
                int result = VgmStreamNative.LibvgmstreamOpenStream(lib, sf, subsong);
                if (result < 0)
                    throw new InvalidOperationException($"Failed to open stream: error {result}. File may not be a supported format.");
            }
            finally
            {
                VgmStreamNative.LibstreamfileClose(sf);
            }

            // Cache format info
            ReadFormatInfo();
        }
        catch
        {
            _handle.Dispose();
            throw;
        }
    }

    // --- Format properties (cached after open) ---

    public int Channels { get; private set; }
    public int SampleRate { get; private set; }
    public SampleFormat SampleFormat { get; private set; }
    public int SampleSize { get; private set; }
    public long StreamSamples { get; private set; }
    public long PlaySamples { get; private set; }
    public bool LoopFlag { get; private set; }
    public long LoopStart { get; private set; }
    public long LoopEnd { get; private set; }
    public int SubsongCount { get; private set; }
    public int SubsongIndex { get; private set; }
    public string CodecName { get; private set; } = string.Empty;
    public string LayoutName { get; private set; } = string.Empty;
    public string MetaName { get; private set; } = string.Empty;
    public string StreamName { get; private set; } = string.Empty;
    public int StreamBitrate { get; private set; }

    /// <summary>
    /// Whether the decoder has finished producing samples.
    /// </summary>
    public bool Done
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var ctx = (VgmStreamNative.LibVgmstreamContext*)_handle.DangerousGetHandle();
            if (ctx->Decoder == 0) return true;
            var decoder = (VgmStreamNative.LibVgmstreamDecoder*)ctx->Decoder;
            return decoder->Done != 0;
        }
    }

    /// <summary>
    /// Decodes the next batch of samples. Returns a span over the library's internal buffer.
    /// The span is valid until the next Render/Fill call.
    /// </summary>
    public ReadOnlySpan<byte> Render()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int result = VgmStreamNative.LibvgmstreamRender(_handle.DangerousGetHandle());
        if (result < 0)
            throw new InvalidOperationException($"Render failed: error {result}");

        var ctx = (VgmStreamNative.LibVgmstreamContext*)_handle.DangerousGetHandle();
        var decoder = (VgmStreamNative.LibVgmstreamDecoder*)ctx->Decoder;

        if (decoder->Buf == 0 || decoder->BufBytes <= 0)
            return ReadOnlySpan<byte>.Empty;

        return new ReadOnlySpan<byte>((void*)decoder->Buf, decoder->BufBytes);
    }

    /// <summary>
    /// Decodes samples into the provided buffer.
    /// </summary>
    /// <param name="buffer">Target buffer. Must be at least channels * sampleSize * bufSamples bytes.</param>
    /// <returns>Number of bytes written.</returns>
    public int Fill(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (SampleSize == 0 || Channels == 0)
            return 0;

        int bufSamples = buffer.Length / (Channels * SampleSize);
        if (bufSamples <= 0) return 0;

        fixed (byte* pBuf = buffer)
        {
            int result = VgmStreamNative.LibvgmstreamFill(_handle.DangerousGetHandle(), pBuf, bufSamples);
            if (result < 0)
                throw new InvalidOperationException($"Fill failed: error {result}");
        }

        var ctx = (VgmStreamNative.LibVgmstreamContext*)_handle.DangerousGetHandle();
        var decoder = (VgmStreamNative.LibVgmstreamDecoder*)ctx->Decoder;
        return decoder->BufBytes;
    }

    /// <summary>
    /// Gets the current play position in samples.
    /// </summary>
    public long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return VgmStreamNative.LibvgmstreamGetPlayPosition(_handle.DangerousGetHandle());
        }
    }

    /// <summary>
    /// Seeks to an absolute sample position.
    /// </summary>
    public void Seek(long sample)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        VgmStreamNative.LibvgmstreamSeek(_handle.DangerousGetHandle(), sample);
    }

    /// <summary>
    /// Resets the stream to the beginning.
    /// </summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        VgmStreamNative.LibvgmstreamReset(_handle.DangerousGetHandle());
    }

    /// <summary>
    /// Gets a formatted description of the current stream.
    /// </summary>
    public string GetDescription()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        byte* buf = stackalloc byte[4096];
        VgmStreamNative.LibvgmstreamFormatDescribe(_handle.DangerousGetHandle(), buf, 4096);
        return VgmStreamNative.FixedStringToManaged(buf, 4096);
    }

    // --- Static helpers ---

    /// <summary>
    /// Whether the native vgmstream library is available.
    /// </summary>
    public static bool IsAvailable => VgmStreamNative.IsAvailable;

    /// <summary>
    /// Gets the library version as a packed uint (0xMMmmpppp).
    /// </summary>
    public static uint GetVersion() => VgmStreamNative.LibvgmstreamGetVersion();

    /// <summary>
    /// Gets all supported file extensions.
    /// </summary>
    public static string[] GetExtensions()
    {
        int size;
        byte** exts = VgmStreamNative.LibvgmstreamGetExtensions(&size);
        return ReadExtensionArray(exts, size);
    }

    /// <summary>
    /// Gets commonly-used file extensions (e.g. wav, ogg) that may conflict with other players.
    /// </summary>
    public static string[] GetCommonExtensions()
    {
        int size;
        byte** exts = VgmStreamNative.LibvgmstreamGetCommonExtensions(&size);
        return ReadExtensionArray(exts, size);
    }

    /// <summary>
    /// Checks if a filename's extension is supported by vgmstream.
    /// </summary>
    public static bool IsValid(string filename)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(filename + '\0');
        fixed (byte* pName = nameBytes)
        {
            return VgmStreamNative.LibvgmstreamIsValid(pName, null) != 0;
        }
    }

    /// <summary>
    /// Returns true if vgmstream detects the filename can be used even if the file doesn't physically exist.
    /// </summary>
    public static bool IsVirtualFilename(string filename)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(filename + '\0');
        fixed (byte* pName = nameBytes)
        {
            return VgmStreamNative.LibvgmstreamIsVirtualFilename(pName) != 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _handle.Dispose();
    }

    // --- Private helpers ---

    private void ReadFormatInfo()
    {
        var ctx = (VgmStreamNative.LibVgmstreamContext*)_handle.DangerousGetHandle();
        if (ctx->Format == 0) return;

        var fmt = (VgmStreamNative.LibVgmstreamFormat*)ctx->Format;
        Channels = fmt->Channels;
        SampleRate = fmt->SampleRate;
        SampleFormat = (SampleFormat)fmt->SampleFormat;
        SampleSize = fmt->SampleSize;
        StreamSamples = fmt->StreamSamples;
        PlaySamples = fmt->PlaySamples;
        LoopFlag = fmt->LoopFlag != 0;
        LoopStart = fmt->LoopStart;
        LoopEnd = fmt->LoopEnd;
        SubsongCount = fmt->SubsongCount;
        SubsongIndex = fmt->SubsongIndex;
        StreamBitrate = fmt->StreamBitrate;
        CodecName = VgmStreamNative.FixedStringToManaged(fmt->CodecName, 128);
        LayoutName = VgmStreamNative.FixedStringToManaged(fmt->LayoutName, 128);
        MetaName = VgmStreamNative.FixedStringToManaged(fmt->MetaName, 128);
        StreamName = VgmStreamNative.FixedStringToManaged(fmt->StreamName, 256);
    }

    private static VgmStreamNative.LibVgmstreamConfig ConfigToNative(VgmStreamConfig config)
    {
        return new VgmStreamNative.LibVgmstreamConfig
        {
            DisableConfigOverride = config.DisableConfigOverride ? (byte)1 : (byte)0,
            AllowPlayForever = config.AllowPlayForever ? (byte)1 : (byte)0,
            PlayForever = config.PlayForever ? (byte)1 : (byte)0,
            IgnoreLoop = config.IgnoreLoop ? (byte)1 : (byte)0,
            ForceLoop = config.ForceLoop ? (byte)1 : (byte)0,
            ReallyForceLoop = config.ReallyForceLoop ? (byte)1 : (byte)0,
            IgnoreFade = config.IgnoreFade ? (byte)1 : (byte)0,
            LoopCount = config.LoopCount,
            FadeTime = config.FadeTime,
            FadeDelay = config.FadeDelay,
            StereoTrack = config.StereoTrack,
            AutoDownmixChannels = config.AutoDownmixChannels,
            ForceSfmt = config.ForceSampleFormat.HasValue ? (int)config.ForceSampleFormat.Value : 0,
        };
    }

    private static string[] ReadExtensionArray(byte** exts, int size)
    {
        if (exts == null || size <= 0)
            return [];

        var result = new string[size];
        for (int i = 0; i < size; i++)
        {
            result[i] = VgmStreamNative.PtrToStringUtf8((nint)exts[i]);
        }
        return result;
    }
}
