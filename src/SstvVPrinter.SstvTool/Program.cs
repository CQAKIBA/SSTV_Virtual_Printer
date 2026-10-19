using System.Runtime.CompilerServices;
using SstvVPrinter.Sstv;
using SstvVPrinter.Sstv.Radiofax;
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
            var options = new TransmissionEncodeOptions(parsed.Mode, parsed.Horizontal);
            var frame = TransmissionModes.IsRadiofax(parsed.Mode)
                ? CreateRadiofaxTestFrame(parsed.Mode)
                : ColorBarFrame.Create(new SstvEncodeOptions(parsed.Mode, parsed.Horizontal));
            long sampleCount = 0;
            var audio = CountSamples(TransmissionEncoder.EncodeAsync(frame, options, cancellation.Token), value => sampleCount += value, cancellation.Token);
            await WaveFileWriter.WriteAsync(parsed.Output, audio, cancellation.Token);
            Console.WriteLine($"Mode: {TransmissionModes.DisplayName(parsed.Mode)}");
            Console.WriteLine($"Dimensions: {frame.Width}x{frame.Height}");
            Console.WriteLine($"Estimated duration: {TransmissionDurationEstimator.Estimate(frame, options):g}");
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
            "fax288" => SstvMode.Radiofax120Ioc288,
            "fax576" => SstvMode.Radiofax120Ioc576,
            _ => throw Usage(),
        };
        if (!int.TryParse(horizontalText, out var horizontal) || horizontal is not (1 or 2)) throw Usage();
        try
        {
            _ = new TransmissionEncodeOptions(mode, horizontal);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw Usage();
        }
        if (output is null || !Path.IsPathFullyQualified(output) || !string.Equals(Path.GetExtension(output), ".wav", StringComparison.OrdinalIgnoreCase)) throw Usage();
        return (mode, horizontal, Path.GetFullPath(output));
    }

    private static ArgumentException Usage() => new("Usage: --mode <martin1|scottie1|scottie2|robot36|fax288|fax576> --horizontal <1|2; Radiofax requires 1> --output <absolute.wav>");

    private static RgbFrame CreateRadiofaxTestFrame(SstvMode mode)
    {
        var definition = RadiofaxModes.Get(mode);
        var pixels = new byte[checked(definition.ActiveWidth * definition.HeaderHeight * 3)];
        for (var y = 0; y < definition.HeaderHeight; y++)
        {
            for (var x = 0; x < definition.ActiveWidth; x++)
            {
                var luminance = (byte)(x * 255L / Math.Max(1, definition.ActiveWidth - 1));
                var offset = ((y * definition.ActiveWidth) + x) * 3;
                pixels[offset] = luminance;
                pixels[offset + 1] = luminance;
                pixels[offset + 2] = luminance;
            }
        }

        return RgbFrame.Create(definition.ActiveWidth, definition.HeaderHeight, pixels);
    }

    private static async IAsyncEnumerable<PcmBlock> CountSamples(IAsyncEnumerable<PcmBlock> blocks, Action<int> count, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var block in blocks.WithCancellation(cancellationToken))
        {
            count(block.Samples.Length);
            yield return block;
        }
    }
}
