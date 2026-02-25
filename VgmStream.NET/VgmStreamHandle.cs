using System.Runtime.InteropServices;

namespace VgmStream.NET;

/// <summary>
/// SafeHandle wrapper for libvgmstream_t* (vgmstream context).
/// Calls libvgmstream_free on dispose.
/// </summary>
public sealed class VgmStreamHandle : SafeHandle
{
    public VgmStreamHandle() : base(0, true) { }

    public VgmStreamHandle(nint handle) : base(0, true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == 0;

    protected override bool ReleaseHandle()
    {
        if (handle != 0)
        {
            VgmStreamNative.LibvgmstreamFree(handle);
            handle = 0;
        }
        return true;
    }
}
