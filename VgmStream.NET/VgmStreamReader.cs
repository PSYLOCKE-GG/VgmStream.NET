namespace VgmStream.NET;

/// <summary>
/// Read-only Stream that yields decoded PCM samples from a VgmStream.
/// The caller owns the underlying VgmStream and must dispose it separately.
/// </summary>
public sealed class VgmStreamReader : Stream
{
    private readonly VgmStream _vgm;
    private readonly bool _ownsVgm;
    private readonly long _totalBytes;
    private byte[] _leftover = [];
    private int _leftoverOffset;
    private long _bytesRead;
    private bool _disposed;

    /// <summary>
    /// Creates a reader over an existing VgmStream instance.
    /// </summary>
    /// <param name="vgm">The VgmStream to read from.</param>
    /// <param name="ownsVgm">If true, the VgmStream is disposed when this reader is disposed.</param>
    public VgmStreamReader(VgmStream vgm, bool ownsVgm = false)
    {
        ArgumentNullException.ThrowIfNull(vgm);
        _vgm = vgm;
        _ownsVgm = ownsVgm;
        _totalBytes = vgm.PlaySamples * vgm.Channels * vgm.SampleSize;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _totalBytes;

    public override long Position
    {
        get => _bytesRead;
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <summary>
    /// The underlying VgmStream instance.
    /// </summary>
    public VgmStream VgmStream => _vgm;

    public override int Read(byte[] buffer, int offset, int count)
        => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int totalCopied = 0;

        while (totalCopied < buffer.Length)
        {
            if (_leftoverOffset < _leftover.Length)
            {
                int available = _leftover.Length - _leftoverOffset;
                int toCopy = Math.Min(available, buffer.Length - totalCopied);
                _leftover.AsSpan(_leftoverOffset, toCopy).CopyTo(buffer[totalCopied..]);
                _leftoverOffset += toCopy;
                totalCopied += toCopy;
                continue;
            }

            if (_vgm.Done)
                break;

            var rendered = _vgm.Render();
            if (rendered.Length == 0)
                break;

            _leftover = rendered.ToArray();
            _leftoverOffset = 0;
        }

        _bytesRead += totalCopied;
        return totalCopied;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _bytesRead + offset,
            SeekOrigin.End => _totalBytes + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (target < 0) throw new ArgumentOutOfRangeException(nameof(offset));

        int frameSize = _vgm.Channels * _vgm.SampleSize;
        if (frameSize > 0)
        {
            long samplePos = target / frameSize;
            _vgm.Seek(samplePos);
        }

        _leftover = [];
        _leftoverOffset = 0;
        _bytesRead = target;
        return target;
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() { }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
            if (disposing && _ownsVgm)
                _vgm.Dispose();
        }
        base.Dispose(disposing);
    }
}
