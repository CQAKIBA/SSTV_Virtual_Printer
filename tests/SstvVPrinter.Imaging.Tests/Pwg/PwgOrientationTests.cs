using SstvVPrinter.Imaging.Pwg;

namespace SstvVPrinter.Imaging.Tests.Pwg;

public sealed class PwgOrientationTests
{
    public static TheoryData<int, int, int, byte[]> Orientations => new()
    {
        { 0, 2, 3, [1, 2, 3, 4, 5, 6] },
        { 1, 2, 3, [1, 2, 3, 4, 5, 6] },
        { 2, 2, 3, [1, 2, 3, 4, 5, 6] },
        { 3, 2, 3, [1, 2, 3, 4, 5, 6] },
    };

    [Theory]
    [MemberData(nameof(Orientations))]
    public async Task Decode_preserves_raster_pixels_for_all_printer_orientations(
        int orientation,
        int expectedWidth,
        int expectedHeight,
        byte[] expectedRedValues)
    {
        var rows = new[]
        {
            PwgFixtureBuilder.LiteralRow(1, (1, 0, 0), (2, 0, 0)),
            PwgFixtureBuilder.LiteralRow(1, (3, 0, 0), (4, 0, 0)),
            PwgFixtureBuilder.LiteralRow(1, (5, 0, 0), (6, 0, 0)),
        };
        using var stream = new MemoryStream(PwgFixtureBuilder.Create(2, 3, rows, orientation));

        var page = await new PwgRasterDecoder().DecodeFirstPageAsync(stream);

        Assert.Equal(expectedWidth, page.Width);
        Assert.Equal(expectedHeight, page.Height);
        Assert.Equal(2, page.Metadata.PhysicalWidth);
        Assert.Equal(3, page.Metadata.PhysicalHeight);
        Assert.Equal(expectedWidth, page.Metadata.LogicalWidth);
        Assert.Equal(expectedHeight, page.Metadata.LogicalHeight);
        Assert.Equal(orientation, page.Metadata.Orientation);

        for (var index = 0; index < expectedRedValues.Length; index++)
        {
            var x = index % expectedWidth;
            var y = index / expectedWidth;
            Assert.Equal(expectedRedValues[index], page.GetPixel(x, y).R);
        }
    }

}
