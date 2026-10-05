namespace SstvVPrinter.Imaging.Header;

internal static class BitmapFont5x7
{
    private static readonly IReadOnlyDictionary<char, byte[]> Glyphs = CreateGlyphs();

    public static bool TryGetRows(char character, out ReadOnlySpan<byte> rows)
    {
        if (Glyphs.TryGetValue(character, out var stored))
        {
            rows = stored;
            return true;
        }

        rows = default;
        return false;
    }

    private static IReadOnlyDictionary<char, byte[]> CreateGlyphs()
    {
        var glyphs = new Dictionary<char, byte[]>();
        Add(glyphs, 'A', "01110 10001 10001 11111 10001 10001 10001");
        Add(glyphs, 'B', "11110 10001 10001 11110 10001 10001 11110");
        Add(glyphs, 'C', "01110 10001 10000 10000 10000 10001 01110");
        Add(glyphs, 'D', "11110 10001 10001 10001 10001 10001 11110");
        Add(glyphs, 'E', "11111 10000 10000 11110 10000 10000 11111");
        Add(glyphs, 'F', "11111 10000 10000 11110 10000 10000 10000");
        Add(glyphs, 'G', "01110 10001 10000 10111 10001 10001 01110");
        Add(glyphs, 'H', "10001 10001 10001 11111 10001 10001 10001");
        Add(glyphs, 'I', "01110 00100 00100 00100 00100 00100 01110");
        Add(glyphs, 'J', "00111 00010 00010 00010 10010 10010 01100");
        Add(glyphs, 'K', "10001 10010 10100 11000 10100 10010 10001");
        Add(glyphs, 'L', "10000 10000 10000 10000 10000 10000 11111");
        Add(glyphs, 'M', "10001 11011 10101 10101 10001 10001 10001");
        Add(glyphs, 'N', "10001 11001 10101 10011 10001 10001 10001");
        Add(glyphs, 'O', "01110 10001 10001 10001 10001 10001 01110");
        Add(glyphs, 'P', "11110 10001 10001 11110 10000 10000 10000");
        Add(glyphs, 'Q', "01110 10001 10001 10001 10101 10010 01101");
        Add(glyphs, 'R', "11110 10001 10001 11110 10100 10010 10001");
        Add(glyphs, 'S', "01111 10000 10000 01110 00001 00001 11110");
        Add(glyphs, 'T', "11111 00100 00100 00100 00100 00100 00100");
        Add(glyphs, 'U', "10001 10001 10001 10001 10001 10001 01110");
        Add(glyphs, 'V', "10001 10001 10001 10001 10001 01010 00100");
        Add(glyphs, 'W', "10001 10001 10001 10101 10101 10101 01010");
        Add(glyphs, 'X', "10001 10001 01010 00100 01010 10001 10001");
        Add(glyphs, 'Y', "10001 10001 01010 00100 00100 00100 00100");
        Add(glyphs, 'Z', "11111 00001 00010 00100 01000 10000 11111");
        Add(glyphs, '0', "01110 10001 10011 10101 11001 10001 01110");
        Add(glyphs, '1', "00100 01100 00100 00100 00100 00100 01110");
        Add(glyphs, '2', "01110 10001 00001 00010 00100 01000 11111");
        Add(glyphs, '3', "11110 00001 00001 01110 00001 00001 11110");
        Add(glyphs, '4', "00010 00110 01010 10010 11111 00010 00010");
        Add(glyphs, '5', "11111 10000 10000 11110 00001 00001 11110");
        Add(glyphs, '6', "01110 10000 10000 11110 10001 10001 01110");
        Add(glyphs, '7', "11111 00001 00010 00100 01000 01000 01000");
        Add(glyphs, '8', "01110 10001 10001 01110 10001 10001 01110");
        Add(glyphs, '9', "01110 10001 10001 01111 00001 00001 01110");
        Add(glyphs, ' ', "00000 00000 00000 00000 00000 00000 00000");
        Add(glyphs, '/', "00001 00010 00010 00100 01000 01000 10000");
        Add(glyphs, '-', "00000 00000 00000 11111 00000 00000 00000");
        Add(glyphs, ':', "00000 00100 00100 00000 00100 00100 00000");
        Add(glyphs, '.', "00000 00000 00000 00000 00000 00110 00110");
        return glyphs;
    }

    private static void Add(Dictionary<char, byte[]> glyphs, char character, string pattern)
    {
        glyphs.Add(character, pattern.Split(' ').Select(row => Convert.ToByte(row, 2)).ToArray());
    }
}
