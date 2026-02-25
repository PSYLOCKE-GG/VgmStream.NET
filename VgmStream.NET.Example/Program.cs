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

// Check extension validity
Console.WriteLine($"Input:    {inputPath}");
Console.WriteLine($"Valid:    {VgmStreamClass.IsValid(inputPath)}");
Console.WriteLine();

// Open and display format info
try
{
    using var vgm = new VgmStreamClass(inputPath, config: new VgmStreamConfig { IgnoreLoop = true });

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

    // Render a few batches to verify decoding works
    int batchCount = 0;
    long totalBytes = 0;
    while (!vgm.Done && batchCount < 5)
    {
        var samples = vgm.Render();
        totalBytes += samples.Length;
        batchCount++;
    }
    Console.WriteLine($"Test decode:   {batchCount} batches, {totalBytes} bytes");
    Console.WriteLine();

    // Convert to WAV if output path given
    if (outputPath != null)
    {
        Console.WriteLine("Converting to WAV...");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        VgmStreamConverter.ConvertToWav(inputPath, outputPath);
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
