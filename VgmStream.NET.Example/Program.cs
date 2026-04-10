using VgmStream.NET;
using VgmStreamClass = VgmStream.NET.VgmStream;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: VgmStream.NET.Example <input-file> [output.wav]");
    return 1;
}

string inputPath = args[0];
string? outputPath = args.Length > 1 ? args[1] : null;

if (!VgmStreamClass.IsAvailable)
{
    Console.Error.WriteLine("ERROR: libvgmstream not found. Ensure libvgmstream.dll/.so/.dylib is in the runtimes directory.");
    return 2;
}

Console.WriteLine($"libvgmstream version: 0x{VgmStreamClass.GetVersion():X8}");
Console.WriteLine();

Console.WriteLine($"Input:    {inputPath}");
Console.WriteLine($"Valid:    {VgmStreamClass.IsValid(inputPath)}");
Console.WriteLine();

try
{
    using var inputStream = File.OpenRead(inputPath);
    using var vgm = new VgmStreamClass(inputStream, Path.GetFileName(inputPath),
        config: new VgmStreamConfig { IgnoreLoop = true });

    Console.WriteLine($"Channels:      {vgm.Channels}");
    Console.WriteLine($"Sample rate:   {vgm.SampleRate} Hz");
    Console.WriteLine($"Sample format: {vgm.SampleFormat}");
    Console.WriteLine($"Sample size:   {vgm.SampleSize} bytes");
    Console.WriteLine($"Samples:       {vgm.StreamSamples}");
    Console.WriteLine($"Play samples:  {vgm.PlaySamples}");
    Console.WriteLine($"Loop:          {vgm.LoopFlag} ({vgm.LoopStart}..{vgm.LoopEnd})");
    Console.WriteLine($"Subsongs:      {vgm.SubsongCount} (index {vgm.SubsongIndex})");
    Console.WriteLine($"Codec:         {vgm.CodecName}");
    Console.WriteLine($"Layout:        {vgm.LayoutName}");
    Console.WriteLine($"Meta:          {vgm.MetaName}");
    Console.WriteLine($"Stream name:   {vgm.StreamName}");
    Console.WriteLine($"Bitrate:       {vgm.StreamBitrate} bps");
    Console.WriteLine();

    using var reader = new VgmStreamReader(vgm);
    byte[] buf = new byte[64 * 1024];
    long totalBytes = 0;
    int batches = 0;
    while (batches < 5)
    {
        int read = reader.Read(buf);
        if (read == 0) break;
        totalBytes += read;
        batches++;
    }
    Console.WriteLine($"Test decode:   {batches} batches, {totalBytes} bytes via VgmStreamReader");
    Console.WriteLine();

    if (outputPath != null)
    {
        Console.WriteLine("Converting to WAV...");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        vgm.Reset();
        using var outputStream = new BufferedStream(File.Create(outputPath), 256 * 1024);
        reader.WriteWavTo(outputStream);

        sw.Stop();
        Console.WriteLine($"Done in {sw.Elapsed.TotalSeconds:F1}s -> {outputPath}");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    return 3;
}

return 0;
