using System.Runtime.CompilerServices;
using SstvVPrinter.Sstv;
using SstvVPrinter.Sstv.Signal;
using SstvVPrinter.Sstv.Wave;

namespace SstvVPrinter.SstvTool;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var parsed = Parse(args);
            using var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
            var options = new SstvEncodeOptions(parsed.Mode, parsed.Horizontal);
            var frame = ColorBarFrame.Create(options);
            long sampleCount = 0;
            var audio = CountSamples(SstvEncoder.EncodeAsync(frame, options, cancellation.Token), value => sampleCount += value, cancellation.Token);
            await WaveFileWriter.WriteAsync(parsed.Output, audio, cancellation.Token);
            Console.WriteLine($"Mode: {SstvModes.Get(parsed.Mode).DisplayName}");
            Console.WriteLine($"Dimensions: {frame.Width}x{frame.Height}");
            Console.WriteLine($"Samples: {sampleCount}");
            Console.WriteLine($"Duration: {sampleCount / (double)SstvEncodeOptions.SampleRate:F6} s");
            Console.WriteLine($"Output: {parsed.Output}");
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or OperationCanceledException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static (SstvMode Mode, int Horizontal, string Output) Parse(string[] args)
    {
        if (args.Length != 6) throw Usage();
        string? modeText = null, horizontalText = null, output = null;
        for (var index = 0; index < args.Length; index += 2)
        {
            switch (args[index])
            {
                case "--mode" when modeText is null: modeText = args[index + 1]; break;
                case "--horizontal" when horizontalText is null: horizontalText = args[index + 1]; break;
                case "--output" when output is null: output = args[index + 1]; break;
                default: throw Usage();
            }
        }
        var mode = modeText switch
        {
            "martin1" => SstvMode.Martin1,
            "scottie1" => SstvMode.Scottie1,
            "scottie2" => SstvMode.Scottie2,
            "robot36" => SstvMode.Robot36,
            _ => throw Usage(),
        };
        if (!int.TryParse(horizontalText, out var horizontal) || horizontal is not (1 or 2)) throw Usage();
        if (output is null || !Path.IsPathFullyQualified(output) || !string.Equals(Path.GetExtension(output), ".wav", StringComparison.OrdinalIgnoreCase)) throw Usage();
        return (mode, horizontal, Path.GetFullPath(output));
    }

    private static ArgumentException Usage() => new("Usage: --mode <martin1|scottie1|scottie2|robot36> --horizontal <1|2> --output <absolute.wav>");

    private static async IAsyncEnumerable<PcmBlock> CountSamples(IAsyncEnumerable<PcmBlock> blocks, Action<int> count, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var block in blocks.WithCancellation(cancellationToken))
        {
            count(block.Samples.Length);
            yield return block;
        }
    }
}
