using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace VgmStream.NET;

/// <summary>
/// P/Invoke surface for the libvgmstream shared library. Entry points are
/// source-generated via <c>[LibraryImport]</c>; the type initializer registers
/// a <see cref="NativeLibrary.SetDllImportResolver"/> that probes
/// <c>runtimes/{RID}/native/</c> before falling back to the OS search path.
/// </summary>
/// <remarks>
/// All vgmstream functions use <c>cdecl</c> on every platform. Optional symbols
/// (<c>libvgmstream_create</c>, <c>libvgmstream_set_log</c>, the tag APIs) are
/// gated through <see cref="NativeLibrary.TryGetExport"/> feature flags rather
/// than being declared as <c>[LibraryImport]</c> — calling a missing entry
/// point would surface as <see cref="EntryPointNotFoundException"/> at call
/// site, which is less friendly than the <see cref="NotSupportedException"/>
/// the feature-flagged wrappers throw.
/// </remarks>
internal static unsafe partial class VgmStreamNative
{
    private const string Lib = "libvgmstream";

    private static readonly List<string> _probedPaths = [];
    private static string? _loadError;

    private static readonly Lazy<nint> _libHandle = new(LoadLibrary);

    static VgmStreamNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(VgmStreamNative).Assembly, Resolver);
    }

    private static nint Resolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) =>
        libraryName == Lib ? _libHandle.Value : 0;

    /// <summary>
    /// Whether the native library was successfully loaded. False when the
    /// shared library couldn't be found in any probed location, when loading
    /// failed (missing dependency, arch mismatch), or when required exports
    /// are absent (native build predates the public libvgmstream API).
    /// </summary>
    public static bool IsAvailable => _libHandle.Value != 0 && _allRequiredExportsResolved.Value;

    /// <summary>
    /// Paths probed during the load attempt, in order. Populated after the
    /// first access of <see cref="IsAvailable"/>. Useful for diagnosing
    /// missing-native failures — <see cref="EnsureLoaded"/> already includes
    /// these in the thrown exception but callers that only inspect
    /// <see cref="IsAvailable"/> need a way to see what was tried.
    /// </summary>
    public static IReadOnlyList<string> ProbedPaths => _probedPaths;

    /// <summary>
    /// Describes the last failure encountered during load — either a missing
    /// native file, a load failure (missing dependency, arch mismatch), or a
    /// missing required export. <see langword="null"/> when the native loaded
    /// successfully or before any probe has run.
    /// </summary>
    public static string? LoadError => _loadError;

    internal static void EnsureLoaded()
    {
        if (IsAvailable) return;

        var msg = new StringBuilder("libvgmstream library is not available. ");
        if (_loadError is not null)
            msg.Append("Last error: ").Append(_loadError).Append(". ");
        if (_probedPaths.Count > 0)
        {
            msg.AppendLine().AppendLine("Probed paths:");
            foreach (var p in _probedPaths)
                msg.Append("  ").AppendLine(p);
        }
        throw new DllNotFoundException(msg.ToString());
    }

    private static nint LoadLibrary()
    {
        string file = GetLibraryFileName();
        string rid = GetRuntimeIdentifier();

        // Probe both the assembly-containing directory and AppContext.BaseDirectory.
        // They usually coincide, but single-file publish, AssemblyLoadContext
        // isolation, or NuGet cache assemblies can make them differ — probing
        // both covers every real-world layout without guessing.
        string? asmLoc = typeof(VgmStreamNative).Assembly.Location;
        string? asmDir = string.IsNullOrEmpty(asmLoc) ? null : Path.GetDirectoryName(asmLoc);

        var roots = new List<string>(2);
        if (!string.IsNullOrEmpty(asmDir))
            roots.Add(asmDir);
        if (!string.IsNullOrEmpty(AppContext.BaseDirectory)
            && !roots.Contains(AppContext.BaseDirectory, StringComparer.OrdinalIgnoreCase))
            roots.Add(AppContext.BaseDirectory);

        foreach (string root in roots)
        {
            foreach (string candidate in new[]
            {
                Path.Combine(root, "runtimes", rid, "native", file),
                Path.Combine(root, file),
            })
            {
                _probedPaths.Add(candidate);
                if (!File.Exists(candidate))
                    continue;
                try
                {
                    if (NativeLibrary.TryLoad(candidate, out nint handle))
                        return handle;
                }
                catch (Exception ex)
                {
                    _loadError = $"{candidate}: {ex.GetType().Name}: {ex.Message}";
                }
            }
        }

        // Name-only overload — bypasses the resolver, so no re-entry. Falls
        // back to the OS default search path.
        _probedPaths.Add($"(OS default) {file}");
        if (NativeLibrary.TryLoad(file, out nint osHandle))
            return osHandle;

        _loadError ??= $"{file} not found in any probed location";
        return 0;
    }

    private static string GetLibraryFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "libvgmstream.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "libvgmstream.dylib";
        return "libvgmstream.so";
    }

    private static string GetRuntimeIdentifier()
    {
        string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
                  : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
                  : "linux";

        string arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "x64",
        };

        return $"{os}-{arch}";
    }

    // ── Required-exports self-check ──
    //
    // P/Invoke via [LibraryImport] binds lazily — the symbol is resolved on
    // first call, and a missing export surfaces as EntryPointNotFoundException
    // at that call site. Eagerly probing every required symbol at IsAvailable
    // evaluation time mirrors the pre-refactor semantics (IsAvailable=false
    // when any required export is absent) and gives callers a single
    // predictable signal instead of failures scattered across call sites.

    private static readonly string[] _requiredExports =
    [
        "libvgmstream_get_version",
        "libvgmstream_init",
        "libvgmstream_free",
        "libvgmstream_setup",
        "libvgmstream_open_stream",
        "libvgmstream_close_stream",
        "libvgmstream_render",
        "libvgmstream_fill",
        "libvgmstream_get_play_position",
        "libvgmstream_seek",
        "libvgmstream_reset",
        "libvgmstream_get_extensions",
        "libvgmstream_get_common_extensions",
        "libvgmstream_is_valid",
        "libvgmstream_get_title",
        "libvgmstream_format_describe",
        "libvgmstream_is_virtual_filename",
        "libstreamfile_open_from_stdio",
        "libstreamfile_open_buffered",
        "libstreamfile_close",
    ];

    private static readonly Lazy<bool> _allRequiredExportsResolved = new(() =>
    {
        nint handle = _libHandle.Value;
        if (handle == 0) return false;

        foreach (var name in _requiredExports)
        {
            if (!NativeLibrary.TryGetExport(handle, name, out _))
            {
                _loadError = $"loaded {GetLibraryFileName()} is missing required export '{name}' "
                    + "(native build predates the public libvgmstream API — rebuild from a newer vgmstream)";
                return false;
            }
        }
        return true;
    });

    // ── Optional-feature flags ──
    //
    // The libvgmstream_create / libvgmstream_set_log / libvgmstream_tags_* APIs
    // were added after the initial public API release. Probe them individually
    // so callers can feature-detect without catching exceptions.

    private static readonly Lazy<bool> _hasCreate = new(() => HasExport("libvgmstream_create"));
    private static readonly Lazy<bool> _hasSetLog = new(() => HasExport("libvgmstream_set_log"));
    private static readonly Lazy<bool> _hasTags = new(() =>
        HasExport("libvgmstream_tags_init")
        && HasExport("libvgmstream_tags_find")
        && HasExport("libvgmstream_tags_next_tag")
        && HasExport("libvgmstream_tags_free"));

    /// <summary>Whether <see cref="LibvgmstreamCreate"/> is available in the loaded native build.</summary>
    public static bool IsCreateSupported => _hasCreate.Value;

    /// <summary>Whether <see cref="LibvgmstreamSetLog"/> is available in the loaded native build.</summary>
    public static bool IsSetLogSupported => _hasSetLog.Value;

    /// <summary>Whether the <c>libvgmstream_tags_*</c> APIs are available in the loaded native build.</summary>
    public static bool IsTagsSupported => _hasTags.Value;

    private static bool HasExport(string name)
    {
        nint handle = _libHandle.Value;
        return handle != 0 && NativeLibrary.TryGetExport(handle, name, out _);
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

    // ── Required entry points ──

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

    // ── Optional entry points ──
    //
    // These may not exist in all libvgmstream builds. The wrappers check the
    // feature flag up front and throw NotSupportedException instead of letting
    // EntryPointNotFoundException bubble from the P/Invoke call site.

    [LibraryImport(Lib, EntryPoint = "libvgmstream_create")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial nint LibvgmstreamCreateRaw(nint libsf, int subsong, LibVgmstreamConfig* cfg);

    internal static nint LibvgmstreamCreate(nint libsf, int subsong, LibVgmstreamConfig* cfg)
    {
        if (!_hasCreate.Value)
            throw new NotSupportedException("libvgmstream_create is not available in this build.");
        return LibvgmstreamCreateRaw(libsf, subsong, cfg);
    }

    [LibraryImport(Lib, EntryPoint = "libvgmstream_set_log")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial void LibvgmstreamSetLogRaw(int level, nint callback);

    internal static void LibvgmstreamSetLog(int level, nint callback)
    {
        if (!_hasSetLog.Value)
            throw new NotSupportedException("libvgmstream_set_log is not available in this build.");
        LibvgmstreamSetLogRaw(level, callback);
    }

    [LibraryImport(Lib, EntryPoint = "libvgmstream_tags_init")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial nint LibvgmstreamTagsInitRaw(nint libsf);

    internal static nint LibvgmstreamTagsInit(nint libsf)
    {
        if (!_hasTags.Value)
            throw new NotSupportedException("libvgmstream_tags_init is not available in this build.");
        return LibvgmstreamTagsInitRaw(libsf);
    }

    [LibraryImport(Lib, EntryPoint = "libvgmstream_tags_find")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial void LibvgmstreamTagsFindRaw(nint tags, byte* targetFilename);

    internal static void LibvgmstreamTagsFind(nint tags, byte* targetFilename)
    {
        if (!_hasTags.Value)
            throw new NotSupportedException("libvgmstream_tags_find is not available in this build.");
        LibvgmstreamTagsFindRaw(tags, targetFilename);
    }

    [LibraryImport(Lib, EntryPoint = "libvgmstream_tags_next_tag")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial byte LibvgmstreamTagsNextTagRaw(nint tags);

    internal static byte LibvgmstreamTagsNextTag(nint tags)
    {
        if (!_hasTags.Value)
            throw new NotSupportedException("libvgmstream_tags_next_tag is not available in this build.");
        return LibvgmstreamTagsNextTagRaw(tags);
    }

    [LibraryImport(Lib, EntryPoint = "libvgmstream_tags_free")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial void LibvgmstreamTagsFreeRaw(nint tags);

    internal static void LibvgmstreamTagsFree(nint tags)
    {
        if (!_hasTags.Value)
            throw new NotSupportedException("libvgmstream_tags_free is not available in this build.");
        LibvgmstreamTagsFreeRaw(tags);
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
