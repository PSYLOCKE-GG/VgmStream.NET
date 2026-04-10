using System.Runtime.InteropServices;
using System.Text;

namespace VgmStream.NET;

/// <summary>
/// P/Invoke bindings for the libvgmstream shared library.
/// Uses NativeLibrary.TryLoad + function pointers for runtime binding.
/// All vgmstream functions use __cdecl calling convention on all platforms.
/// </summary>
public static unsafe class VgmStreamNative
{
    private static nint _lib;
    private static volatile bool _loaded;
    private static volatile bool _attempted;
    private static readonly object _loadLock = new();

    private static nint _pGetVersion;
    private static nint _pInit;
    private static nint _pFree;
    private static nint _pSetup;
    private static nint _pOpenStream;
    private static nint _pCloseStream;
    private static nint _pRender;
    private static nint _pFill;
    private static nint _pGetPlayPosition;
    private static nint _pSeek;
    private static nint _pReset;
    private static nint _pCreate;
    private static nint _pSetLog;
    private static nint _pGetExtensions;
    private static nint _pGetCommonExtensions;
    private static nint _pIsValid;
    private static nint _pGetTitle;
    private static nint _pFormatDescribe;
    private static nint _pIsVirtualFilename;
    private static nint _pTagsInit;
    private static nint _pTagsFind;
    private static nint _pTagsNextTag;
    private static nint _pTagsFree;
    private static nint _pOpenFromStdio;
    private static nint _pStreamfileClose;

    /// <summary>
    /// Whether the native library was successfully loaded and all functions resolved.
    /// </summary>
    public static bool IsAvailable
    {
        get
        {
            if (!_attempted) TryLoad();
            return _loaded;
        }
    }

    private static void TryLoad()
    {
        lock (_loadLock)
        {
            if (_attempted) return;
            _attempted = true;

            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool isOsx = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            string libName = isWindows ? "libvgmstream.dll" : isOsx ? "libvgmstream.dylib" : "libvgmstream.so";
            string rid = GetRuntimeIdentifier();
            string? assemblyDir = Path.GetDirectoryName(typeof(VgmStreamNative).Assembly.Location);

            string[] searchPaths = assemblyDir != null
                ? [
                    Path.Combine(assemblyDir, "runtimes", rid, "native", libName),
                    Path.Combine(assemblyDir, libName),
                ]
                : [libName];

            foreach (string path in searchPaths)
            {
                if (File.Exists(path) && NativeLibrary.TryLoad(path, out _lib))
                    break;
            }

            if (_lib == 0)
                NativeLibrary.TryLoad(libName, typeof(VgmStreamNative).Assembly, null, out _lib);

            if (_lib == 0)
                return;

            if (!TryResolveAll())
            {
                NativeLibrary.Free(_lib);
                _lib = 0;
                return;
            }

            _loaded = true;
        }
    }

    private static string GetRuntimeIdentifier()
    {
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        bool isOsx = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        string arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "x64",
        };
        string os = isWindows ? "win" : isOsx ? "osx" : "linux";
        return $"{os}-{arch}";
    }

    private static bool TryResolveAll()
    {
        bool ok = NativeLibrary.TryGetExport(_lib, "libvgmstream_get_version", out _pGetVersion)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_init", out _pInit)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_free", out _pFree)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_setup", out _pSetup)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_open_stream", out _pOpenStream)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_close_stream", out _pCloseStream)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_render", out _pRender)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_fill", out _pFill)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_get_play_position", out _pGetPlayPosition)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_seek", out _pSeek)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_reset", out _pReset)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_get_extensions", out _pGetExtensions)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_get_common_extensions", out _pGetCommonExtensions)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_is_valid", out _pIsValid)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_get_title", out _pGetTitle)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_format_describe", out _pFormatDescribe)
            && NativeLibrary.TryGetExport(_lib, "libvgmstream_is_virtual_filename", out _pIsVirtualFilename)
            && NativeLibrary.TryGetExport(_lib, "libstreamfile_open_from_stdio", out _pOpenFromStdio)
            && NativeLibrary.TryGetExport(_lib, "libstreamfile_close", out _pStreamfileClose);

        // Optional symbols — resolve if available but don't fail the load
        NativeLibrary.TryGetExport(_lib, "libvgmstream_create", out _pCreate);
        NativeLibrary.TryGetExport(_lib, "libvgmstream_set_log", out _pSetLog);
        NativeLibrary.TryGetExport(_lib, "libvgmstream_tags_init", out _pTagsInit);
        NativeLibrary.TryGetExport(_lib, "libvgmstream_tags_find", out _pTagsFind);
        NativeLibrary.TryGetExport(_lib, "libvgmstream_tags_next_tag", out _pTagsNextTag);
        NativeLibrary.TryGetExport(_lib, "libvgmstream_tags_free", out _pTagsFree);

        return ok;
    }

    internal static void EnsureLoaded()
    {
        if (!IsAvailable)
            throw new DllNotFoundException(
                "libvgmstream library is not available. Ensure libvgmstream.dll/.so/.dylib is present in the runtimes folder.");
    }

    /// <summary>Mirrors libvgmstream_format_t.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct LibVgmstreamFormat
    {
        public int Channels;
        public int SampleRate;
        public int SampleFormat;
        public int SampleSize;
        public uint ChannelLayout;
        public int SubsongIndex;
        public int SubsongCount;
        public int InputChannels;
        public long StreamSamples;
        public long LoopStart;
        public long LoopEnd;
        public byte LoopFlag;
        public byte PlayForever;
        public long PlaySamples;
        public int StreamBitrate;
        public fixed byte CodecName[128];
        public fixed byte LayoutName[128];
        public fixed byte MetaName[128];
        public fixed byte StreamName[256];
        public int FormatId;
    }

    /// <summary>Mirrors libvgmstream_decoder_t.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LibVgmstreamDecoder
    {
        public nint Buf;
        public int BufSamples;
        public int BufBytes;
        public byte Done;
    }

    /// <summary>Mirrors libvgmstream_t (context handle).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LibVgmstreamContext
    {
        public nint Priv;
        public nint Format;
        public nint Decoder;
    }

    /// <summary>Mirrors libvgmstream_config_t.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct LibVgmstreamConfig
    {
        public byte DisableConfigOverride;
        public byte AllowPlayForever;
        public byte PlayForever;
        public byte IgnoreLoop;
        public byte ForceLoop;
        public byte ReallyForceLoop;
        public byte IgnoreFade;
        public double LoopCount;
        public double FadeTime;
        public double FadeDelay;
        public int StereoTrack;
        public int AutoDownmixChannels;
        public int ForceSfmt;
    }

    /// <summary>Mirrors libvgmstream_valid_t.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LibVgmstreamValid
    {
        public byte IsExtension;
        public byte SkipStandard;
        public byte RejectExtensionless;
        public byte AcceptUnknown;
        public byte AcceptCommon;
    }

    /// <summary>Mirrors libvgmstream_title_t.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LibVgmstreamTitle
    {
        public byte ForceTitle;
        public byte SubsongRange;
        public byte RemoveExtension;
        public byte RemoveArchive;
        public nint Filename;
    }

    /// <summary>Mirrors libvgmstream_tags_t.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LibVgmstreamTags
    {
        public nint Priv;
        public nint Key;
        public nint Val;
    }

    /// <summary>Mirrors libstreamfile_t (custom IO).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LibStreamfile
    {
        public nint UserData;
        public nint Read;
        public nint GetSize;
        public nint GetName;
        public nint Open;
        public nint Close;
    }

    public static uint LibvgmstreamGetVersion()
    {
        EnsureLoaded();
        return ((delegate* unmanaged[Cdecl]<uint>)_pGetVersion)();
    }

    public static nint LibvgmstreamInit()
    {
        EnsureLoaded();
        return ((delegate* unmanaged[Cdecl]<nint>)_pInit)();
    }

    public static void LibvgmstreamFree(nint lib)
    {
        EnsureLoaded();
        ((delegate* unmanaged[Cdecl]<nint, void>)_pFree)(lib);
    }

    public static void LibvgmstreamSetup(nint lib, LibVgmstreamConfig* cfg)
    {
        EnsureLoaded();
        ((delegate* unmanaged[Cdecl]<nint, LibVgmstreamConfig*, void>)_pSetup)(lib, cfg);
    }

    public static int LibvgmstreamOpenStream(nint lib, nint libsf, int subsong)
    {
        EnsureLoaded();
        return ((delegate* unmanaged[Cdecl]<nint, nint, int, int>)_pOpenStream)(lib, libsf, subsong);
    }

    public static void LibvgmstreamCloseStream(nint lib)
    {
        EnsureLoaded();
        ((delegate* unmanaged[Cdecl]<nint, void>)_pCloseStream)(lib);
    }

    public static int LibvgmstreamRender(nint lib)
    {
        EnsureLoaded();
        return ((delegate* unmanaged[Cdecl]<nint, int>)_pRender)(lib);
    }

    public static int LibvgmstreamFill(nint lib, void* buf, int bufSamples)
    {
        EnsureLoaded();
        return ((delegate* unmanaged[Cdecl]<nint, void*, int, int>)_pFill)(lib, buf, bufSamples);
    }

    public static long LibvgmstreamGetPlayPosition(nint lib)
    {
        EnsureLoaded();
        return ((delegate* unmanaged[Cdecl]<nint, long>)_pGetPlayPosition)(lib);
    }

    public static void LibvgmstreamSeek(nint lib, long sample)
    {
        EnsureLoaded();
        ((delegate* unmanaged[Cdecl]<nint, long, void>)_pSeek)(lib, sample);
    }

    public static void LibvgmstreamReset(nint lib)
    {
        EnsureLoaded();
        ((delegate* unmanaged[Cdecl]<nint, void>)_pReset)(lib);
    }

    public static nint LibvgmstreamCreate(nint libsf, int subsong, LibVgmstreamConfig* cfg)
    {
        EnsureLoaded();
        if (_pCreate == 0) throw new NotSupportedException("libvgmstream_create is not available in this build.");
        return ((delegate* unmanaged[Cdecl]<nint, int, LibVgmstreamConfig*, nint>)_pCreate)(libsf, subsong, cfg);
    }

    public static void LibvgmstreamSetLog(int level, nint callback)
    {
        EnsureLoaded();
        if (_pSetLog == 0) throw new NotSupportedException("libvgmstream_set_log is not available in this build.");
        ((delegate* unmanaged[Cdecl]<int, nint, void>)_pSetLog)(level, callback);
    }

    public static byte** LibvgmstreamGetExtensions(int* size)
    {
        EnsureLoaded();
        return ((delegate* unmanaged[Cdecl]<int*, byte**>)_pGetExtensions)(size);
    }

    public static byte** LibvgmstreamGetCommonExtensions(int* size)
    {
        EnsureLoaded();
        return ((delegate* unmanaged[Cdecl]<int*, byte**>)_pGetCommonExtensions)(size);
    }

    public static byte LibvgmstreamIsValid(byte* filename, LibVgmstreamValid* cfg)
    {
        EnsureLoaded();
        return ((delegate* unmanaged[Cdecl]<byte*, LibVgmstreamValid*, byte>)_pIsValid)(filename, cfg);
    }

    public static int LibvgmstreamGetTitle(nint lib, LibVgmstreamTitle* cfg, byte* buf, int bufLen)
    {
        EnsureLoaded();
        return ((delegate* unmanaged[Cdecl]<nint, LibVgmstreamTitle*, byte*, int, int>)_pGetTitle)(lib, cfg, buf, bufLen);
    }

    public static int LibvgmstreamFormatDescribe(nint lib, byte* dst, int dstSize)
    {
        EnsureLoaded();
        return ((delegate* unmanaged[Cdecl]<nint, byte*, int, int>)_pFormatDescribe)(lib, dst, dstSize);
    }

    public static byte LibvgmstreamIsVirtualFilename(byte* filename)
    {
        EnsureLoaded();
        return ((delegate* unmanaged[Cdecl]<byte*, byte>)_pIsVirtualFilename)(filename);
    }

    public static nint LibvgmstreamTagsInit(nint libsf)
    {
        EnsureLoaded();
        if (_pTagsInit == 0) throw new NotSupportedException("libvgmstream_tags_init is not available in this build.");
        return ((delegate* unmanaged[Cdecl]<nint, nint>)_pTagsInit)(libsf);
    }

    public static void LibvgmstreamTagsFind(nint tags, byte* targetFilename)
    {
        EnsureLoaded();
        if (_pTagsFind == 0) throw new NotSupportedException("libvgmstream_tags_find is not available in this build.");
        ((delegate* unmanaged[Cdecl]<nint, byte*, void>)_pTagsFind)(tags, targetFilename);
    }

    public static byte LibvgmstreamTagsNextTag(nint tags)
    {
        EnsureLoaded();
        if (_pTagsNextTag == 0) throw new NotSupportedException("libvgmstream_tags_next_tag is not available in this build.");
        return ((delegate* unmanaged[Cdecl]<nint, byte>)_pTagsNextTag)(tags);
    }

    public static void LibvgmstreamTagsFree(nint tags)
    {
        EnsureLoaded();
        if (_pTagsFree == 0) throw new NotSupportedException("libvgmstream_tags_free is not available in this build.");
        ((delegate* unmanaged[Cdecl]<nint, void>)_pTagsFree)(tags);
    }

    public static nint LibstreamfileOpenFromStdio(byte* filename)
    {
        EnsureLoaded();
        return ((delegate* unmanaged[Cdecl]<byte*, nint>)_pOpenFromStdio)(filename);
    }

    /// <summary>
    /// Closes a libstreamfile_t. Exported as a proper API function.
    /// </summary>
    public static void LibstreamfileClose(nint libsf)
    {
        if (libsf == 0) return;
        EnsureLoaded();
        ((delegate* unmanaged[Cdecl]<nint, void>)_pStreamfileClose)(libsf);
    }

    /// <summary>
    /// Helper to read a null-terminated UTF-8 string from a native pointer.
    /// Returns empty string if pointer is null.
    /// </summary>
    internal static string PtrToStringUtf8(nint ptr)
    {
        if (ptr == 0) return string.Empty;
        return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
    }

    /// <summary>
    /// Helper to read a fixed-size UTF-8 string from a native buffer.
    /// </summary>
    internal static string FixedStringToManaged(byte* buf, int maxLen)
    {
        int len = 0;
        while (len < maxLen && buf[len] != 0) len++;
        if (len == 0) return string.Empty;
        return Encoding.UTF8.GetString(buf, len);
    }
}
