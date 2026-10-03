using SstvVPrinter.Imaging.Pwg;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Tests.Pwg;

public sealed class PwgRasterDecoderTests
{
    [Fact]
    public async Task Decode_accepts_supported_srgb24_header()
    {
        using var stream = new MemoryStream(PwgFixtureBuilder.CreateSolid(2, 1, 10, 20, 30));
        var page = await new PwgRasterDecoder().DecodeFirstPageAsync(stream);

        Assert.Equal(2, page.Width);
        Assert.Equal(1, page.Height);
        Assert.Equal(new Rgb24(10, 20, 30), page.GetPixel(1, 0));
    }

    [Fact]
    public async Task Decode_reads_big_endian_metadata()
    {
        var document = PwgFixtureBuilder.CreateSolid(2, 1);
        PwgFixtureBuilder.Write(document, 276, 600);
        PwgFixtureBuilder.Write(document, 280, 720);
        PwgFixtureBuilder.Write(document, 352, 1008);
        PwgFixtureBuilder.Write(document, 356, 756);
        using var stream = new MemoryStream(document);

        var metadata = (await new PwgRasterDecoder().DecodeFirstPageAsync(stream)).Metadata;

        Assert.Equal(600, metadata.DpiX);
        Assert.Equal(720, metadata.DpiY);
        Assert.Equal(1008, metadata.PageWidthPoints);
        Assert.Equal(756, metadata.PageHeightPoints);
    }

    public static TheoryData<int, int, string> InvalidFields => new()
    {
        { 372, 0, "width" }, { 372, 20001, "width" }, { 376, 0, "height" },
        { 376, 20001, "height" }, { 392, 99, "bytes per line" }, { 384, 16, "bits per color" },
        { 388, 32, "bits per pixel" }, { 396, 1, "color order" }, { 400, 18, "color space" },
        { 420, 4, "number of colors" }, { 344, 4, "orientation" }, { 276, 0, "resolution" },
    };

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public async Task Decode_rejects_unsupported_or_invalid_header_fields(int offset, int value, string field)
    {
        var document = PwgFixtureBuilder.CreateSolid(2, 1);
        PwgFixtureBuilder.Write(document, offset, value);
        using var stream = new MemoryStream(document);

        var exception = await Assert.ThrowsAsync<PwgRasterException>(async () =>
            await new PwgRasterDecoder().DecodeFirstPageAsync(stream));

        Assert.Contains(field, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Decode_rejects_wrong_sync()
    {
        var document = PwgFixtureBuilder.CreateSolid(1, 1);
        document[0] = (byte)'X';
        using var stream = new MemoryStream(document);
        var exception = await Assert.ThrowsAsync<PwgRasterException>(async () =>
            await new PwgRasterDecoder().DecodeFirstPageAsync(stream));
        Assert.Contains("sync", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Decode_rejects_incomplete_header()
    {
        var bytes = new byte[100];
        "RaS2"u8.CopyTo(bytes);
        using var stream = new MemoryStream(bytes);
        var exception = await Assert.ThrowsAsync<PwgRasterException>(async () =>
            await new PwgRasterDecoder().DecodeFirstPageAsync(stream));
        Assert.Contains("header", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Decode_rejects_excessive_pixel_count()
    {
        var document = PwgFixtureBuilder.CreateSolid(1, 1);
        PwgFixtureBuilder.Write(document, 372, 10000);
        PwgFixtureBuilder.Write(document, 376, 10000);
        PwgFixtureBuilder.Write(document, 392, 30000);
        using var stream = new MemoryStream(document);
        var exception = await Assert.ThrowsAsync<PwgRasterException>(async () =>
            await new PwgRasterDecoder().DecodeFirstPageAsync(stream));
        Assert.Contains("pixel count", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Decode_handles_repeated_literal_and_mixed_runs()
    {
        var literal = Enumerable.Range(0, 128).Select(index => ((byte)index, (byte)(index + 1), (byte)(index + 2))).ToArray();
        var firstRow = PwgFixtureBuilder.LiteralRow(1, literal);
        var secondRow = PwgFixtureBuilder.RepeatedRow(1, (1, 250, 10, 20), (127, 30, 40, 50));
        using var stream = new MemoryStream(PwgFixtureBuilder.Create(128, 2, [firstRow, secondRow]));

        var page = await new PwgRasterDecoder().DecodeFirstPageAsync(stream);

        Assert.Equal(new Rgb24(0, 1, 2), page.GetPixel(0, 0));
        Assert.Equal(new Rgb24(127, 128, 129), page.GetPixel(127, 0));
        Assert.Equal(new Rgb24(250, 10, 20), page.GetPixel(0, 1));
        Assert.Equal(new Rgb24(30, 40, 50), page.GetPixel(127, 1));
    }

    [Fact]
    public async Task Decode_handles_256_repeated_rows_and_byte_at_a_time_reads()
    {
        var stream = new FragmentedReadStream(PwgFixtureBuilder.CreateSolid(1, 256, 7, 8, 9));
        var page = await new PwgRasterDecoder().DecodeFirstPageAsync(stream);

        Assert.Equal(new Rgb24(7, 8, 9), page.GetPixel(0, 255));
        Assert.False(stream.WasDisposed);
    }

    [Theory]
    [InlineData(new byte[] { 0, 128 })]
    [InlineData(new byte[] { 0, 1, 1, 2, 3 })]
    [InlineData(new byte[] { 1, 0, 1, 2, 3 })]
    public async Task Decode_rejects_reserved_or_overlong_runs(byte[] row)
    {
        using var stream = new MemoryStream(PwgFixtureBuilder.Create(1, 1, [row]));
        await Assert.ThrowsAsync<PwgRasterException>(async () =>
            await new PwgRasterDecoder().DecodeFirstPageAsync(stream));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Decode_rejects_truncated_raster_tokens(int bytesToKeep)
    {
        var document = PwgFixtureBuilder.CreateSolid(2, 1, 1, 2, 3);
        Array.Resize(ref document, 4 + PwgFixtureBuilder.HeaderLength + bytesToKeep);
        using var stream = new MemoryStream(document);
        var exception = await Assert.ThrowsAsync<PwgRasterException>(async () =>
            await new PwgRasterDecoder().DecodeFirstPageAsync(stream));
        Assert.Contains("raster", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Decode_returns_first_page_without_reading_malformed_second_page()
    {
        var first = PwgFixtureBuilder.CreateSolid(1, 1, 11, 22, 33);
        var bytes = first.Concat("BAD!"u8.ToArray()).ToArray();
        using var stream = new MemoryStream(bytes);

        var page = await new PwgRasterDecoder().DecodeFirstPageAsync(stream);

        Assert.Equal(new Rgb24(11, 22, 33), page.GetPixel(0, 0));
        Assert.True(stream.Position < stream.Length);
    }

    [Fact]
    public async Task Decode_leaves_stream_open_on_failure_and_cancellation()
    {
        var failed = new FragmentedReadStream([1, 2, 3, 4]);
        await Assert.ThrowsAsync<PwgRasterException>(async () =>
            await new PwgRasterDecoder().DecodeFirstPageAsync(failed));
        Assert.False(failed.WasDisposed);

        var cancelled = new FragmentedReadStream(PwgFixtureBuilder.CreateSolid(1, 1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new PwgRasterDecoder().DecodeFirstPageAsync(cancelled, cancellation.Token));
        Assert.False(cancelled.WasDisposed);
    }
}
