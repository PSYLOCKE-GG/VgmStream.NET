using System.Runtime.InteropServices;
using System.Text;

namespace VgmStream.NET;

/// <summary>
/// P/Invoke surface for the libvgmstream shared library. Entry points are
/// source-generated via <c>[LibraryImport]</c>; resolution uses the runtime's
/// default algorithm, which walks <c>NATIVE_DLL_SEARCH_DIRECTORIES</c>
/// (populated from deps.json for NuGet <c>runtimes/{rid}/native/</c> payloads)
/// and the assembly directory via <c>LoadLibraryEx</c> with altered search path.
/// </summary>
internal static unsafe partial class VgmStreamNative
{
    private const string Lib = "libvgmstream";

    // PackageReference consumers get the native path via deps.json and resolve
    // through NATIVE_DLL_SEARCH_DIRECTORIES automatically. ProjectReference
    // consumers don't — MSBuild doesn't propagate runtimes/{rid}/native/ into
    // the consumer's deps.json. This resolver is the one-line fallback.
    static VgmStreamNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(VgmStreamNative).Assembly, (name, _, _) =>
        {
            if (name != Lib) return 0;
            var dir = Path.GetDirectoryName(typeof(VgmStreamNative).Assembly.Location);
            if (string.IsNullOrEmpty(dir)) return 0;
            var file = Path.Combine(dir, "runtimes", Rid, "native", NativeFileName);
            return File.Exists(file) && NativeLibrary.TryLoad(file, out var h) ? h : 0;
        });
    }

    private static string Rid =>
        (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
         : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
         : "linux") + "-" + RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

    private static string NativeFileName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "libvgmstream.dll"
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libvgmstream.dylib"
        : "libvgmstream.so";

    /// <summary>
    /// Whether the native library can be loaded. Probes with a cheap parameterless
    /// P/Invoke — a missing or unloadable native surfaces as
    /// <see cref="DllNotFoundException"/> from the runtime (with OS error text).
    /// </summary>
    public static bool IsAvailable
    {
        get
        {
            try { _ = LibvgmstreamGetVersion(); return true; }
            catch (DllNotFoundException) { return false; }
        }
    }

    /// <summary>
    /// Forces the native library to load. Lets the runtime's
    /// <see cref="DllNotFoundException"/> (with full Win32 error detail) propagate
    /// when the native can't be resolved. Safe to call from multiple threads.
    /// </summary>
    internal static void EnsureLoaded() => _ = LibvgmstreamGetVersion();

    [LibraryImport(Lib, EntryPoint = "libvgmstream_get_version")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial uint LibvgmstreamGetVersion();

    [LibraryImport(Lib, EntryPoint = "libvgmstream_init")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial nint LibvgmstreamInit();

    [LibraryImport(Lib, EntryPoint = "libvgmstream_free")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void LibvgmstreamFree(nint lib);

    [LibraryImport(Lib, EntryPoint = "libvgmstream_setup")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void LibvgmstreamSetup(nint lib, LibVgmstreamConfig* cfg);

    [LibraryImport(Lib, EntryPoint = "libvgmstream_open_stream")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int LibvgmstreamOpenStream(nint lib, nint libsf, int subsong);

    [LibraryImport(Lib, EntryPoint = "libvgmstream_close_stream")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void LibvgmstreamCloseStream(nint lib);

    [LibraryImport(Lib, EntryPoint = "libvgmstream_render")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int LibvgmstreamRender(nint lib);

    [LibraryImport(Lib, EntryPoint = "libvgmstream_fill")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int LibvgmstreamFill(nint lib, void* buf, int bufSamples);

    [LibraryImport(Lib, EntryPoint = "libvgmstream_get_play_position")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial long LibvgmstreamGetPlayPosition(nint lib);

    [LibraryImport(Lib, EntryPoint = "libvgmstream_seek")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void LibvgmstreamSeek(nint lib, long sample);

    [LibraryImport(Lib, EntryPoint = "libvgmstream_reset")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void LibvgmstreamReset(nint lib);

    [LibraryImport(Lib, EntryPoint = "libvgmstream_get_extensions")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial byte** LibvgmstreamGetExtensions(int* size);

    [LibraryImport(Lib, EntryPoint = "libvgmstream_get_common_extensions")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial byte** LibvgmstreamGetCommonExtensions(int* size);

    [LibraryImport(Lib, EntryPoint = "libvgmstream_is_valid")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial byte LibvgmstreamIsValid(byte* filename, LibVgmstreamValid* cfg);

    [LibraryImport(Lib, EntryPoint = "libvgmstream_get_title")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int LibvgmstreamGetTitle(nint lib, LibVgmstreamTitle* cfg, byte* buf, int bufLen);

    [LibraryImport(Lib, EntryPoint = "libvgmstream_format_describe")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int LibvgmstreamFormatDescribe(nint lib, byte* dst, int dstSize);

    [LibraryImport(Lib, EntryPoint = "libvgmstream_is_virtual_filename")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial byte LibvgmstreamIsVirtualFilename(byte* filename);

    [LibraryImport(Lib, EntryPoint = "libstreamfile_open_from_stdio")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial nint LibstreamfileOpenFromStdio(byte* filename);

    /// <summary>
    /// Wraps a libstreamfile_t with vgmstream's internal read cache.
    /// Recommended for custom streamfiles since vgmstream seeks heavily.
    /// </summary>
    [LibraryImport(Lib, EntryPoint = "libstreamfile_open_buffered")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial nint LibstreamfileOpenBuffered(nint extLibsf);

    [LibraryImport(Lib, EntryPoint = "libstreamfile_close")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial void LibstreamfileCloseRaw(nint libsf);

    /// <summary>Closes a libstreamfile_t. Null handles are tolerated as a convenience.</summary>
    internal static void LibstreamfileClose(nint libsf)
    {
        if (libsf == 0) return;
        LibstreamfileCloseRaw(libsf);
    }

    // ── Struct types mirroring libvgmstream headers ──

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

    // ── Managed helpers ──

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
