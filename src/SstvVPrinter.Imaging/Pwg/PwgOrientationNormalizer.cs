namespace SstvVPrinter.Imaging.Pwg;

internal static class PwgOrientationNormalizer
{
    public static (int Width, int Height, byte[] Rgb24) Normalize(
        int physicalWidth,
        int physicalHeight,
        int orientation,
        ReadOnlySpan<byte> physicalRgb,
        CancellationToken cancellationToken)
    {
        var logicalWidth = orientation is 1 or 3 ? physicalHeight : physicalWidth;
        var logicalHeight = orientation is 1 or 3 ? physicalWidth : physicalHeight;
        var logicalRgb = new byte[checked(logicalWidth * logicalHeight * 3)];

        for (var y = 0; y < logicalHeight; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < logicalWidth; x++)
            {
                if ((x & 4095) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var (sourceX, sourceY) = orientation switch
                {
                    0 => (x, y),
                    1 => (physicalWidth - 1 - y, x),
                    2 => (physicalWidth - 1 - x, physicalHeight - 1 - y),
                    3 => (y, physicalHeight - 1 - x),
                    _ => throw new ArgumentOutOfRangeException(nameof(orientation)),
                };

                var sourceOffset = checked(((sourceY * physicalWidth) + sourceX) * 3);
                var destinationOffset = checked(((y * logicalWidth) + x) * 3);
                physicalRgb.Slice(sourceOffset, 3).CopyTo(logicalRgb.AsSpan(destinationOffset, 3));
            }
        }

        return (logicalWidth, logicalHeight, logicalRgb);
    }
}
