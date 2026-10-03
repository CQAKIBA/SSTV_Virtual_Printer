using SstvVPrinter.Imaging.Header;

namespace SstvVPrinter.Imaging.Tests.Header;

public sealed class BitmapFontTests
{
    [Fact]
    public void Font_contains_every_accepted_header_character()
    {
        const string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 /-:.";
        foreach (var character in characters)
        {
            Assert.True(BitmapFont5x7.TryGetRows(character, out var rows), character.ToString());
            Assert.Equal(7, rows.Length);
        }

        Assert.False(BitmapFont5x7.TryGetRows('a', out _));
        Assert.False(BitmapFont5x7.TryGetRows('_', out _));
    }

    [Theory]
    [InlineData('A', new byte[] { 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 })]
    [InlineData('1', new byte[] { 0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 })]
    [InlineData(':', new byte[] { 0, 0b00100, 0b00100, 0, 0b00100, 0b00100, 0 })]
    [InlineData('-', new byte[] { 0, 0, 0, 0b11111, 0, 0, 0 })]
    public void Font_known_glyph_masks_are_exact(char character, byte[] expected)
    {
        Assert.True(BitmapFont5x7.TryGetRows(character, out var rows));
        Assert.Equal(expected, rows.ToArray());
    }
}
