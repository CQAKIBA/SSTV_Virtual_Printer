using SstvVPrinter.IppProbe.Ipp;

namespace SstvVPrinter.IppProbe.Tests;

public sealed class IppMessageWriterTests
{
    [Fact]
    public async Task WriteAsync_emits_network_byte_order_and_end_tag()
    {
        var message = new IppMessage(new IppVersion(2, 0), IppStatusCodes.SuccessfulOk, 42,
        [
            new IppAttributeGroup(IppDelimiterTags.OperationAttributes,
            [
                new IppAttribute("attributes-charset", [IppValue.FromString(IppValueTags.Charset, "utf-8")])
            ])
        ]);
        await using var output = new MemoryStream();

        await IppMessageWriter.WriteAsync(output, message, CancellationToken.None);

        byte[] bytes = output.ToArray();
        Assert.Equal([0x02, 0x00, 0x00, 0x00, 0, 0, 0, 42], bytes[..8]);
        Assert.Equal(IppDelimiterTags.EndOfAttributes, bytes[^1]);
    }

    [Fact]
    public async Task WriteAsync_uses_empty_name_for_repeated_values()
    {
        var message = new IppMessage(new IppVersion(2, 0), IppStatusCodes.SuccessfulOk, 42,
        [
            new IppAttributeGroup(IppDelimiterTags.PrinterAttributes,
            [
                new IppAttribute("ipp-versions-supported",
                [
                    IppValue.FromString(IppValueTags.Keyword, "2.0"),
                    IppValue.FromString(IppValueTags.Keyword, "2.1")
                ])
            ])
        ]);
        await using var output = new MemoryStream();

        await IppMessageWriter.WriteAsync(output, message, CancellationToken.None);

        byte[] bytes = output.ToArray();
        byte[] repeatedValuePrefix = [IppValueTags.Keyword, 0, 0, 0, 3, (byte)'2', (byte)'.', (byte)'1'];
        Assert.True(bytes.AsSpan().IndexOf(repeatedValuePrefix) >= 0);
    }

    [Fact]
    public async Task WriteAsync_round_trips_supported_value_types()
    {
        var original = new IppMessage(new IppVersion(2, 1), IppStatusCodes.SuccessfulOk, 42,
        [
            new IppAttributeGroup(IppDelimiterTags.OperationAttributes,
            [
                new IppAttribute("attributes-charset", [IppValue.FromString(IppValueTags.Charset, "utf-8")]),
                new IppAttribute("status-message", [IppValue.FromString(IppValueTags.TextWithoutLanguage, "ready")])
            ])
        ]);
        await using var output = new MemoryStream();
        await IppMessageWriter.WriteAsync(output, original, CancellationToken.None);
        output.Position = 0;

        IppMessage parsed = await IppMessageReader.ReadAsync(output, CancellationToken.None);

        Assert.Equal(original, parsed);
    }
}
