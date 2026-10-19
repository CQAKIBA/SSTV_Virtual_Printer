using SstvVPrinter.ImagingTool;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Tests.Tool;

public sealed class ToolOptionsParserTests
{
    [Theory]
    [InlineData("2026-10-03T09:00:00")]
    [InlineData("2026-10-03 09:00:00")]
    public void Parse_rejects_timestamp_without_explicit_offset(string timestamp)
    {
        var exception = Assert.Throws<ArgumentException>(() => ToolOptionsParser.Parse(Arguments(timestamp)));
        Assert.Contains("offset", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("2026-10-03T09:00:00Z")]
    [InlineData("2026-10-03T09:00:00+09:00")]
    [InlineData("2026-10-03T09:00:00.1234567+09:00")]
    public void Parse_accepts_ISO_timestamp_with_explicit_offset(string timestamp)
    {
        var options = ToolOptionsParser.Parse(Arguments(timestamp));
        Assert.Equal(2026, options.AcceptedAt.Year);
    }

    [Theory]
    [InlineData("fax288", SstvMode.Radiofax120Ioc288)]
    [InlineData("fax576", SstvMode.Radiofax120Ioc576)]
    public void Parse_accepts_radiofax_aliases_at_horizontal_one(string alias, SstvMode expected)
    {
        var options = ToolOptionsParser.Parse(Arguments("2026-10-03T09:00:00Z", alias, "1"));

        Assert.Equal(expected, options.Mode);
        Assert.Equal(1, options.Horizontal);
    }

    [Theory]
    [InlineData("fax288")]
    [InlineData("fax576")]
    public void Parse_rejects_horizontal_two_for_radiofax(string alias)
    {
        Assert.Throws<ArgumentException>(() =>
            ToolOptionsParser.Parse(Arguments("2026-10-03T09:00:00Z", alias, "2")));
    }

    private static string[] Arguments(string timestamp, string mode = "martin1", string horizontal = "1") =>
    [
        "--input", @"C:\input.pwg",
        "--mode", mode,
        "--horizontal", horizontal,
        "--callsign", "JA1UMW",
        "--accepted-at", timestamp,
        "--output", @"C:\output.ppm",
    ];
}
