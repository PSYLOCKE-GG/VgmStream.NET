using System.Buffers.Binary;

namespace VgmStream.NET;

/// <summary>
/// Static convenience methods for converting game audio to WAV format.
/// Writes a standard RIFF/WAV header then streams decoded PCM samples.
/// </summary>
public static class VgmStreamConverter
{
    /// <summary>
    /// Converts a game audio file to WAV and writes to the specified output path.
    /// </summary>
    public static void ConvertToWav(string inputPath, string outputPath, VgmStreamConfig? config = null)
    {
        using var output = File.Create(outputPath);
        ConvertToWav(inputPath, output, config);
    }

    /// <summary>
    /// Converts a game audio file to WAV and writes to the specified stream.
    /// </summary>
    public static void ConvertToWav(string inputPath, Stream output, VgmStreamConfig? config = null)
    {
        config ??= new VgmStreamConfig { IgnoreLoop = true };

        using var vgm = new VgmStream(inputPath, config: config);

        int channels = vgm.Channels;
        int sampleRate = vgm.SampleRate;
        int sampleSize = vgm.SampleSize;
        int bitsPerSample = sampleSize * 8;
        int blockAlign = channels * sampleSize;

        // Determine WAV format tag
        ushort formatTag = vgm.SampleFormat == SampleFormat.Float ? (ushort)3 : (ushort)1; // 3 = IEEE_FLOAT, 1 = PCM

        // Write placeholder WAV header (will update data size at the end)
        long headerStart = output.Position;
        WriteWavHeader(output, formatTag, (ushort)channels, (uint)sampleRate, (ushort)bitsPerSample, 0);

        // Decode and write samples
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

        // Update header with actual data size if stream supports seeking
        if (output.CanSeek)
        {
            long endPos = output.Position;
            output.Position = headerStart;
            WriteWavHeader(output, formatTag, (ushort)channels, (uint)sampleRate, (ushort)bitsPerSample, (uint)totalDataBytes);
            output.Position = endPos;
        }
    }

    /// <summary>
    /// Asynchronously converts a game audio file to WAV and writes to the specified stream.
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
            if (samples.Length > 0)
            {
                byte[] buffer = samples.ToArray();
                await output.WriteAsync(buffer, cancellationToken);
                totalDataBytes += buffer.Length;
            }
        }

        if (output.CanSeek)
        {
            long endPos = output.Position;
            output.Position = headerStart;
            WriteWavHeader(output, formatTag, (ushort)channels, (uint)sampleRate, (ushort)bitsPerSample, (uint)totalDataBytes);
            output.Position = endPos;
        }
    }

    private static void WriteWavHeader(Stream output, ushort formatTag, ushort channels, uint sampleRate, ushort bitsPerSample, uint dataSize)
    {
        Span<byte> header = stackalloc byte[44];

        ushort blockAlign = (ushort)(channels * (bitsPerSample / 8));
        uint byteRate = sampleRate * blockAlign;
        uint riffSize = dataSize + 36; // 44 - 8

        // RIFF header
        header[0] = (byte)'R'; header[1] = (byte)'I'; header[2] = (byte)'F'; header[3] = (byte)'F';
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], riffSize);
        header[8] = (byte)'W'; header[9] = (byte)'A'; header[10] = (byte)'V'; header[11] = (byte)'E';

        // fmt sub-chunk
        header[12] = (byte)'f'; header[13] = (byte)'m'; header[14] = (byte)'t'; header[15] = (byte)' ';
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], 16); // sub-chunk size
        BinaryPrimitives.WriteUInt16LittleEndian(header[20..], formatTag);
        BinaryPrimitives.WriteUInt16LittleEndian(header[22..], channels);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(header[28..], byteRate);
        BinaryPrimitives.WriteUInt16LittleEndian(header[32..], blockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(header[34..], bitsPerSample);

        // data sub-chunk
        header[36] = (byte)'d'; header[37] = (byte)'a'; header[38] = (byte)'t'; header[39] = (byte)'a';
        BinaryPrimitives.WriteUInt32LittleEndian(header[40..], dataSize);

        output.Write(header);
    }
}
