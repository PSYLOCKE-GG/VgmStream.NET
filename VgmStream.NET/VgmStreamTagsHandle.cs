using System.Runtime.InteropServices;

namespace VgmStream.NET;

/// <summary>
/// SafeHandle wrapper for libvgmstream_tags_t* (tag reader).
/// Calls libvgmstream_tags_free on dispose.
/// </summary>
public sealed class VgmStreamTagsHandle : SafeHandle
{
    public VgmStreamTagsHandle() : base(0, true) { }

    public VgmStreamTagsHandle(nint handle) : base(0, true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == 0;

    protected override bool ReleaseHandle()
    {
        if (handle != 0)
        {
            VgmStreamNative.LibvgmstreamTagsFree(handle);
            handle = 0;
        }
        return true;
    }
}
