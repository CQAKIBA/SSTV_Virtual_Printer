using SstvVPrinter.Sstv;

namespace SstvVPrinter.SstvTool;

public static class ColorBarFrame
{
    private static readonly Rgb24[] Bars =
    [
        new(0, 0, 0),
        new(255, 255, 255),
        new(255, 0, 0),
        new(0, 255, 0),
        new(0, 0, 255),
        new(128, 128, 128),
        new(255, 255, 0),
        new(0, 255, 255),
    ];

    public static RgbFrame Create(SstvEncodeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var mode = SstvModes.Get(options.Mode);
        var width = mode.Width * options.HorizontalSampleMultiplier;
        var data = new byte[width * mode.Height * 3];
        var detailStart = mode.Height - 16;
        for (var y = 0; y < mode.Height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = y >= detailStart
                    ? (x % 2 == 0 ? Bars[0] : Bars[1])
                    : Bars[Math.Min(Bars.Length - 1, x * Bars.Length / width)];
                var offset = ((y * width) + x) * 3;
                data[offset] = pixel.R;
                data[offset + 1] = pixel.G;
                data[offset + 2] = pixel.B;
            }
        }
        return RgbFrame.Create(width, mode.Height, data);
    }
}
