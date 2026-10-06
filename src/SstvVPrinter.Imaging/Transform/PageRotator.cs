using System.Diagnostics;
using SstvVPrinter.Imaging.Model;

namespace SstvVPrinter.Imaging.Transform;

public static class PageRotator
{
    public static DecodedPage Rotate(
        DecodedPage source,
        int clockwiseDegrees,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (clockwiseDegrees == 0) return source;
        if (clockwiseDegrees is not (90 or 180 or 270))
        {
            throw new ArgumentOutOfRangeException(nameof(clockwiseDegrees));
        }

        var swapsAxes = clockwiseDegrees is 90 or 270;
        var width = swapsAxes ? source.Height : source.Width;
        var height = swapsAxes ? source.Width : source.Height;
        var pixels = new byte[checked(width * height * 3)];

        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var (sourceX, sourceY) = clockwiseDegrees switch
                {
                    90 => (y, source.Height - 1 - x),
                    180 => (source.Width - 1 - x, source.Height - 1 - y),
                    270 => (source.Width - 1 - y, x),
                    _ => throw new UnreachableException(),
                };
                var color = source.GetPixel(sourceX, sourceY);
                var offset = ((y * width) + x) * 3;
                pixels[offset] = color.R;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.B;
            }
        }

        var metadata = source.Metadata;
        var rotatedMetadata = swapsAxes
            ? new PwgPageMetadata(
                metadata.PhysicalHeight, metadata.PhysicalWidth,
                metadata.LogicalHeight, metadata.LogicalWidth,
                metadata.DpiY, metadata.DpiX, 0,
                metadata.PageHeightPoints, metadata.PageWidthPoints)
            : metadata with { Orientation = 0 };
        return DecodedPage.CreateOwned(width, height, pixels, rotatedMetadata);
    }
}
