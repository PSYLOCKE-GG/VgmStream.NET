using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace VgmStream.NET;

/// <summary>
/// Creates a native libstreamfile_t backed by a .NET Stream.
/// The stream must be readable and seekable.
/// </summary>
/// <remarks>
/// vgmstream is single-threaded and our read callback always seeks before reading,
/// so multiple streamfile instances can safely share the same underlying Stream.
/// </remarks>
internal sealed unsafe class ManagedStreamfile : IDisposable
{
    private readonly Stream _stream;
    private readonly string _name;
    private GCHandle _nameBytesHandle;
    private GCHandle _gcHandle;
    private VgmStreamNative.LibStreamfile* _native;
    private bool _disposed;

    public nint NativeHandle => (nint)_native;

    public ManagedStreamfile(Stream stream, string name)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(name);

        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        if (!stream.CanSeek)
            throw new ArgumentException("Stream must be seekable. vgmstream requires random-access reads.", nameof(stream));

        _stream = stream;
        _name = name;

        byte[] nameBytes = Encoding.UTF8.GetBytes(name + '\0');
        _nameBytesHandle = GCHandle.Alloc(nameBytes, GCHandleType.Pinned);

        _gcHandle = GCHandle.Alloc(this);

        _native = (VgmStreamNative.LibStreamfile*)NativeMemory.AllocZeroed(
            (nuint)sizeof(VgmStreamNative.LibStreamfile));

        _native->UserData = GCHandle.ToIntPtr(_gcHandle);
        _native->Read = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, long, int, int>)&OnRead;
        _native->GetSize = (nint)(delegate* unmanaged[Cdecl]<nint, long>)&OnGetSize;
        _native->GetName = (nint)(delegate* unmanaged[Cdecl]<nint, nint>)&OnGetName;
        _native->Open = (nint)(delegate* unmanaged[Cdecl]<nint, byte*, nint>)&OnOpen;
        _native->Close = (nint)(delegate* unmanaged[Cdecl]<nint, void>)&OnClose;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        FreeNative();
    }

    private void FreeNative()
    {
        if (_gcHandle.IsAllocated)
            _gcHandle.Free();
        if (_nameBytesHandle.IsAllocated)
            _nameBytesHandle.Free();
        if (_native != null)
        {
            NativeMemory.Free(_native);
            _native = null;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OnRead(nint userData, byte* dst, long offset, int length)
    {
        try
        {
            var self = (ManagedStreamfile)GCHandle.FromIntPtr(userData).Target!;
            self._stream.Seek(offset, SeekOrigin.Begin);
            return self._stream.Read(new Span<byte>(dst, length));
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long OnGetSize(nint userData)
    {
        try
        {
            var self = (ManagedStreamfile)GCHandle.FromIntPtr(userData).Target!;
            return self._stream.Length;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nint OnGetName(nint userData)
    {
        try
        {
            var self = (ManagedStreamfile)GCHandle.FromIntPtr(userData).Target!;
            return self._nameBytesHandle.AddrOfPinnedObject();
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Called by vgmstream to open another streamfile (or reopen the same one).
    /// We support reopens by creating a new ManagedStreamfile over the same Stream.
    /// vgmstream is single-threaded and OnRead always seeks, so sharing is safe.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nint OnOpen(nint userData, byte* filename)
    {
        try
        {
            var self = (ManagedStreamfile)GCHandle.FromIntPtr(userData).Target!;

            string requested = Marshal.PtrToStringUTF8((nint)filename) ?? string.Empty;
            if (requested != self._name)
                return 0;

            var clone = new ManagedStreamfile(self._stream, self._name);
            return clone.NativeHandle;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnClose(nint libsfPtr)
    {
        if (libsfPtr == 0) return;
        var libsf = (VgmStreamNative.LibStreamfile*)libsfPtr;
        if (libsf->UserData == 0) return;

        var self = (ManagedStreamfile)GCHandle.FromIntPtr(libsf->UserData).Target!;
        self.FreeNative();
    }
}
