using SstvVPrinter.Sstv;

namespace SstvVPrinter.Desktop.Imaging;

public static class PreviewFrameFactory
{
    public static RgbFrame Create(RgbFrame source, int verticalScale)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (verticalScale is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(verticalScale));
        }

        if (verticalScale == 1) return source;

        var sourcePixels = new byte[checked(source.Width * source.Height * 3)];
        source.CopyPixelsTo(sourcePixels);
        var rowBytes = checked(source.Width * 3);
        var previewPixels = new byte[checked(sourcePixels.Length * verticalScale)];
        for (var y = 0; y < source.Height; y++)
        {
            var row = sourcePixels.AsSpan(y * rowBytes, rowBytes);
            for (var copy = 0; copy < verticalScale; copy++)
            {
                row.CopyTo(previewPixels.AsSpan(((y * verticalScale) + copy) * rowBytes, rowBytes));
            }
        }

        return RgbFrame.Create(source.Width, checked(source.Height * verticalScale), previewPixels);
    }
}
