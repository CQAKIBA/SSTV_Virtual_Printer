using System.Buffers.Binary;

namespace SstvVPrinter.Imaging.Pwg;

internal sealed record PwgRasterHeader(
    int Width,
    int Height,
    int DpiX,
    int DpiY,
    int Orientation,
    int PageWidthPoints,
    int PageHeightPoints)
{
    internal const int Length = 1796;
    internal const int MaximumDimension = 20_000;
    internal const int MaximumPixels = 50_000_000;

    public static PwgRasterHeader Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Length)
        {
            throw new PwgRasterException($"PWG page header must contain exactly {Length} bytes.");
        }

        var dpiX = Read(bytes, 276);
        var dpiY = Read(bytes, 280);
        var orientation = Read(bytes, 344);
        var pageWidthPoints = Read(bytes, 352);
        var pageHeightPoints = Read(bytes, 356);
        var width = Read(bytes, 372);
        var height = Read(bytes, 376);
        var bitsPerColor = Read(bytes, 384);
        var bitsPerPixel = Read(bytes, 388);
        var bytesPerLine = Read(bytes, 392);
        var colorOrder = Read(bytes, 396);
        var colorSpace = Read(bytes, 400);
        var numberOfColors = Read(bytes, 420);

        Require(width is > 0 and <= MaximumDimension, "width", width);
        Require(height is > 0 and <= MaximumDimension, "height", height);
        Require(dpiX > 0 && dpiY > 0, "resolution", $"{dpiX}x{dpiY}");
        Require(orientation is >= 0 and <= 3, "orientation", orientation);
        Require(pageWidthPoints > 0 && pageHeightPoints > 0, "page dimensions", $"{pageWidthPoints}x{pageHeightPoints}");
        Require(bitsPerColor == 8, "bits per color", bitsPerColor);
        Require(bitsPerPixel == 24, "bits per pixel", bitsPerPixel);
        Require(colorOrder == 0, "color order", colorOrder);
        Require(colorSpace == 19, "color space", colorSpace);
        Require(numberOfColors == 3, "number of colors", numberOfColors);

        int expectedBytesPerLine;
        long pixelCount;
        try
        {
            expectedBytesPerLine = checked(width * 3);
            pixelCount = checked((long)width * height);
            _ = checked(width * height * 3);
        }
        catch (OverflowException exception)
        {
            throw new PwgRasterException("PWG dimensions overflow the decoded allocation size.", exception);
        }

        Require(bytesPerLine == expectedBytesPerLine, "bytes per line", bytesPerLine);
        Require(pixelCount <= MaximumPixels, "pixel count", pixelCount);

        return new(width, height, dpiX, dpiY, orientation, pageWidthPoints, pageHeightPoints);
    }

    private static int Read(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(offset, 4));

    private static void Require(bool condition, string field, object value)
    {
        if (!condition)
        {
            throw new PwgRasterException($"Unsupported or invalid PWG {field}: {value}.");
        }
    }
}
