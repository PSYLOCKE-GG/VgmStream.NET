using System.Buffers;
using System.Buffers.Binary;

namespace VgmStream.NET;

/// <summary>
/// Extension methods for writing VgmStreamReader output to common formats.
/// </summary>
public static class VgmStreamReaderExtensions
{
    private const uint MaxWavDataSize = uint.MaxValue - 36;

    /// <summary>
    /// Writes the decoded audio as a RIFF/WAV stream to <paramref name="output"/>.
    /// </summary>
    public static void WriteWavTo(this VgmStreamReader reader, Stream output)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(output);

        var vgm = reader.VgmStream;
        var (formatTag, channels, sampleRate, bitsPerSample) = GetWavParams(vgm);

        long headerStart = output.Position;
        WriteWavHeader(output, formatTag, channels, sampleRate, bitsPerSample, 0);

        long totalDataBytes = 0;
        while (!vgm.Done)
        {
            var samples = vgm.Render();
            if (samples.Length > 0)
            {
                output.Write(samples);
                totalDataBytes += samples.Length;
            }
        }

        if (output.CanSeek)
        {
            long endPos = output.Position;
            output.Position = headerStart;
            WriteWavHeader(output, formatTag, channels, sampleRate, bitsPerSample, ClampDataSize(totalDataBytes));
            output.Position = endPos;
        }
    }

    /// <summary>
    /// Asynchronously writes the decoded audio as a RIFF/WAV stream to <paramref name="output"/>.
    /// Decoding is CPU-bound; only the stream writes are async.
    /// </summary>
    public static async Task WriteWavToAsync(this VgmStreamReader reader, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(output);

        var vgm = reader.VgmStream;
        var (formatTag, channels, sampleRate, bitsPerSample) = GetWavParams(vgm);

        long headerStart = output.Position;
        WriteWavHeader(output, formatTag, channels, sampleRate, bitsPerSample, 0);

        long totalDataBytes = 0;
        while (!vgm.Done)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var samples = vgm.Render();
            int len = samples.Length;
            if (len > 0)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(len);
                try
                {
                    samples.CopyTo(buffer);
                    await output.WriteAsync(buffer.AsMemory(0, len), cancellationToken);
                    totalDataBytes += len;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        if (output.CanSeek)
        {
            long endPos = output.Position;
            output.Position = headerStart;
            WriteWavHeader(output, formatTag, channels, sampleRate, bitsPerSample, ClampDataSize(totalDataBytes));
            output.Position = endPos;
        }
    }

    private static (ushort formatTag, ushort channels, uint sampleRate, ushort bitsPerSample) GetWavParams(VgmStream vgm)
    {
        ushort formatTag = vgm.SampleFormat == SampleFormat.Float ? (ushort)3 : (ushort)1;
        return (formatTag, (ushort)vgm.Channels, (uint)vgm.SampleRate, (ushort)(vgm.SampleSize * 8));
    }

    private static uint ClampDataSize(long totalDataBytes)
    {
        return totalDataBytes > MaxWavDataSize ? MaxWavDataSize : (uint)totalDataBytes;
    }

    private static void WriteWavHeader(Stream output, ushort formatTag, ushort channels, uint sampleRate, ushort bitsPerSample, uint dataSize)
    {
        Span<byte> header = stackalloc byte[44];

        ushort blockAlign = (ushort)(channels * (bitsPerSample / 8));
        uint byteRate = sampleRate * blockAlign;
        uint riffSize = dataSize + 36;

        header[0] = (byte)'R'; header[1] = (byte)'I'; header[2] = (byte)'F'; header[3] = (byte)'F';
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], riffSize);
        header[8] = (byte)'W'; header[9] = (byte)'A'; header[10] = (byte)'V'; header[11] = (byte)'E';

        header[12] = (byte)'f'; header[13] = (byte)'m'; header[14] = (byte)'t'; header[15] = (byte)' ';
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(header[20..], formatTag);
        BinaryPrimitives.WriteUInt16LittleEndian(header[22..], channels);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(header[28..], byteRate);
        BinaryPrimitives.WriteUInt16LittleEndian(header[32..], blockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(header[34..], bitsPerSample);

        header[36] = (byte)'d'; header[37] = (byte)'a'; header[38] = (byte)'t'; header[39] = (byte)'a';
        BinaryPrimitives.WriteUInt32LittleEndian(header[40..], dataSize);

        output.Write(header);
    }
}
