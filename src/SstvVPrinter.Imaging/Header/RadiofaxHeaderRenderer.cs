using System.Globalization;
using SstvVPrinter.Imaging.Composition;
using SstvVPrinter.Sstv;
using SstvVPrinter.Sstv.Radiofax;

namespace SstvVPrinter.Imaging.Header;

public static class RadiofaxHeaderRenderer
{
    private static readonly byte[] ReferenceLevels = [0, 36, 73, 109, 146, 182, 219, 255];

    internal static string MetadataText(CompositionRequest request)
    {
        var jst = request.AcceptedAt.ToOffset(TimeSpan.FromHours(9));
        var label = request.Mode switch
        {
            SstvMode.Radiofax120Ioc288 => "FAX288",
            SstvMode.Radiofax120Ioc576 => "FAX576",
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };
        return $"{jst.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} JST {label}";
    }

    public static void Draw(Span<byte> rgb24, int width, int height, CompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var definition = RadiofaxModes.Get(request.Mode);
        if (width != definition.ActiveWidth || height < definition.HeaderHeight ||
            rgb24.Length != checked(width * height * 3))
        {
            throw new ArgumentException("Header target dimensions do not match its RGB buffer.", nameof(rgb24));
        }

        var headerHeight = definition.HeaderHeight;
        var unit = headerHeight / 16;
        for (var cell = 0; cell < ReferenceLevels.Length; cell++)
        {
            FillRect(rgb24, width, cell * headerHeight, 0, headerHeight, headerHeight, ReferenceLevels[cell]);
        }

        DrawOutline(rgb24, width, 0, 0, 8 * headerHeight, headerHeight, unit);
        var metadataX = (8 * headerHeight) + (2 * unit);
        var callSignWidth = request.CallSign.Length * 12 * unit;
        var callSignX = width - (2 * unit) - callSignWidth;
        DrawText(rgb24, width, metadataX, MetadataText(request), unit, 1, 1);
        DrawText(rgb24, width, callSignX, request.CallSign, unit, 2, 2);
    }

    private static void DrawText(
        Span<byte> pixels,
        int width,
        int startX,
        string text,
        int unit,
        int horizontalStretch,
        int logicalGap)
    {
        var x = startX;
        foreach (var character in text)
        {
            if (!BitmapFont5x7.TryGetRows(character, out var rows))
            {
                throw new InvalidOperationException($"Header glyph '{character}' is unavailable.");
            }

            for (var row = 0; row < 7; row++)
            {
                for (var column = 0; column < 5; column++)
                {
                    if ((rows[row] & (1 << (4 - column))) != 0)
                    {
                        FillRect(
                            pixels,
                            width,
                            x + (column * unit * horizontalStretch),
                            unit + (row * 2 * unit),
                            unit * horizontalStretch,
                            2 * unit,
                            0);
                    }
                }
            }

            x += ((5 * horizontalStretch) + logicalGap) * unit;
        }
    }

    private static void DrawOutline(
        Span<byte> pixels,
        int canvasWidth,
        int x,
        int y,
        int width,
        int height,
        int thickness)
    {
        FillRect(pixels, canvasWidth, x, y, width, thickness, 0);
        FillRect(pixels, canvasWidth, x, y + height - thickness, width, thickness, 0);
        FillRect(pixels, canvasWidth, x, y, thickness, height, 0);
        FillRect(pixels, canvasWidth, x + width - thickness, y, thickness, height, 0);
    }

    private static void FillRect(
        Span<byte> pixels,
        int canvasWidth,
        int x,
        int y,
        int width,
        int height,
        byte value)
    {
        for (var row = y; row < y + height; row++)
        {
            for (var column = x; column < x + width; column++)
            {
                var offset = ((row * canvasWidth) + column) * 3;
                pixels[offset] = value;
                pixels[offset + 1] = value;
                pixels[offset + 2] = value;
            }
        }
    }
}
