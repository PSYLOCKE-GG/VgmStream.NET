using System.Buffers;
using System.Buffers.Binary;

namespace VgmStream.NET;

/// <summary>
/// Static convenience methods for converting game audio to WAV format.
/// Writes a standard RIFF/WAV header then streams decoded PCM samples.
/// </summary>
public static class VgmStreamConverter
{
    private const uint MaxWavDataSize = uint.MaxValue - 36;

    /// <summary>
    /// Converts a game audio file to WAV and writes to the specified output path.
    /// Creates or overwrites the file at <paramref name="outputPath"/>.
    /// </summary>
    public static void ConvertToWav(string inputPath, string outputPath, VgmStreamConfig? config = null)
    {
        using var output = File.Create(outputPath);
        ConvertToWav(inputPath, output, config);
    }

    /// <summary>
    /// Converts a game audio file to WAV and writes to the specified stream.
    /// When <paramref name="config"/> is null, defaults to ignoring loops so the
    /// stream has a finite length suitable for WAV output.
    /// </summary>
    public static void ConvertToWav(string inputPath, Stream output, VgmStreamConfig? config = null)
    {
        config ??= new VgmStreamConfig { IgnoreLoop = true };

        using var vgm = new VgmStream(inputPath, config: config);

        int channels = vgm.Channels;
        int sampleRate = vgm.SampleRate;
        int sampleSize = vgm.SampleSize;
        int bitsPerSample = sampleSize * 8;
        ushort formatTag = vgm.SampleFormat == SampleFormat.Float ? (ushort)3 : (ushort)1;

        long headerStart = output.Position;
        WriteWavHeader(output, formatTag, (ushort)channels, (uint)sampleRate, (ushort)bitsPerSample, 0);

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
            WriteWavHeader(output, formatTag, (ushort)channels, (uint)sampleRate, (ushort)bitsPerSample, ClampDataSize(totalDataBytes));
            output.Position = endPos;
        }
    }

    /// <summary>
    /// Asynchronously converts a game audio file to WAV and writes to the specified stream.
    /// The decoding itself is CPU-bound and runs synchronously; only the stream writes are async.
    /// When <paramref name="config"/> is null, defaults to ignoring loops.
    /// </summary>
    public static async Task ConvertToWavAsync(string inputPath, Stream output, VgmStreamConfig? config = null, CancellationToken cancellationToken = default)
    {
        config ??= new VgmStreamConfig { IgnoreLoop = true };

        using var vgm = new VgmStream(inputPath, config: config);

        int channels = vgm.Channels;
        int sampleRate = vgm.SampleRate;
        int sampleSize = vgm.SampleSize;
        int bitsPerSample = sampleSize * 8;
        ushort formatTag = vgm.SampleFormat == SampleFormat.Float ? (ushort)3 : (ushort)1;

        long headerStart = output.Position;
        WriteWavHeader(output, formatTag, (ushort)channels, (uint)sampleRate, (ushort)bitsPerSample, 0);

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
            WriteWavHeader(output, formatTag, (ushort)channels, (uint)sampleRate, (ushort)bitsPerSample, ClampDataSize(totalDataBytes));
            output.Position = endPos;
        }
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
