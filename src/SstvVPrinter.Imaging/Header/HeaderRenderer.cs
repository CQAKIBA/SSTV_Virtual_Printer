using System.Globalization;
using SstvVPrinter.Imaging.Composition;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Header;

internal static class HeaderRenderer
{
    private const int HeaderHeight = 16;
    private const int LogicalGlyphAdvance = 6;

    public static HeaderLayout MeasureAndSelect(CompositionRequest request, int canvasWidth)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(canvasWidth);

        var scale = request.HorizontalSampleMultiplier;
        var patchWidth = 16 * scale;
        var metadataX = (3 * patchWidth) + (2 * scale);
        var advance = LogicalGlyphAdvance * scale;
        var rightMargin = 2 * scale;
        var separator = LogicalGlyphAdvance * scale;
        var callSignWidth = request.CallSign.Length * advance;
        var callSignX = canvasWidth - rightMargin - callSignWidth;
        var jst = request.AcceptedAt.ToOffset(TimeSpan.FromHours(9));
        var mode = ModeLabel(request.Mode);
        string[] candidates =
        [
            $"{jst.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} JST {mode}",
            $"{jst.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture)} JST {mode}",
            $"{jst.ToString("HH:mm", CultureInfo.InvariantCulture)} JST {mode}",
            $"JST {mode}",
        ];

        foreach (var candidate in candidates)
        {
            if (metadataX + (candidate.Length * advance) + separator <= callSignX)
            {
                return new(candidate, request.CallSign, scale, patchWidth, metadataX, callSignX, advance);
            }
        }

        throw new ArgumentException("The complete call sign and shortest metadata header cannot fit the canvas.", nameof(request));
    }

    public static void Draw(Span<byte> rgb24, int width, int height, HeaderLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (height < HeaderHeight || rgb24.Length != checked(width * height * 3))
        {
            throw new ArgumentException("Header target dimensions do not match its RGB buffer.", nameof(rgb24));
        }

        FillRect(rgb24, width, 0, 0, layout.PatchWidth, HeaderHeight, 255, 0, 0);
        FillRect(rgb24, width, layout.PatchWidth, 0, layout.PatchWidth, HeaderHeight, 0, 255, 0);
        FillRect(rgb24, width, layout.PatchWidth * 2, 0, layout.PatchWidth, HeaderHeight, 0, 0, 255);
        DrawText(rgb24, width, layout.MetadataX, layout.MetadataText, layout.HorizontalScale);
        DrawText(rgb24, width, layout.CallSignX, layout.CallSign, layout.HorizontalScale);
    }

    private static void DrawText(Span<byte> pixels, int width, int startX, string text, int scale)
    {
        var x = startX;
        foreach (var character in text)
        {
            if (!BitmapFont5x7.TryGetRows(character, out var rows))
            {
                throw new InvalidOperationException($"Header glyph '{character}' is unavailable.");
            }

            for (var row = 0; row < 7; row++)
            for (var column = 0; column < 5; column++)
            {
                if ((rows[row] & (1 << (4 - column))) == 0) continue;
                FillRect(pixels, width, x + (column * scale), 1 + (row * 2), scale, 2, 0, 0, 0);
            }

            x += LogicalGlyphAdvance * scale;
        }
    }

    private static void FillRect(
        Span<byte> pixels, int canvasWidth, int x, int y, int width, int height,
        byte red, byte green, byte blue)
    {
        for (var row = y; row < y + height; row++)
        for (var column = x; column < x + width; column++)
        {
            var offset = ((row * canvasWidth) + column) * 3;
            pixels[offset] = red;
            pixels[offset + 1] = green;
            pixels[offset + 2] = blue;
        }
    }

    private static string ModeLabel(SstvMode mode) => mode switch
    {
        SstvMode.Martin1 => "M1",
        SstvMode.Scottie1 => "S1",
        SstvMode.Scottie2 => "S2",
        SstvMode.Robot36 => "R36",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
}
