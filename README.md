# VgmStream.NET

C# wrapper for [vgmstream](https://github.com/vgmstream/vgmstream). Decodes hundreds of video game audio formats via P/Invoke bindings to the native library.

## Requirements

- .NET 9.0
- Native `libvgmstream` binary (built by CI or manually)

## Building

Native libraries are built in CI via the `Build Natives` workflow. For local dev, grab the artifacts from a CI run and drop them in `VgmStream.NET/runtimes/{rid}/native/`.

```
dotnet build VgmStream.NET.sln
```

## Usage

### Code

```csharp
using VgmStream.NET;

// Open and read format info
using var vgm = new VgmStream("audio.adx");
Console.WriteLine($"{vgm.Channels}ch {vgm.SampleRate}Hz {vgm.CodecName}");

// Decode
while (!vgm.Done)
{
    ReadOnlySpan<byte> pcm = vgm.Render();
    // ...
}

// Or just convert to WAV
VgmStreamConverter.ConvertToWav("audio.adx", "output.wav");
```

### CLI

```
VgmStream.NET.Example <input-file> [output.wav]
```

## License

ISC. See [LICENSE](LICENSE).

vgmstream itself is ISC licensed. See [vgmstream/COPYING](vgmstream/COPYING).
