using SstvVPrinter.ImagingTool;

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

    private static string[] Arguments(string timestamp) =>
    [
        "--input", @"C:\input.pwg",
        "--mode", "martin1",
        "--horizontal", "1",
        "--callsign", "JA1UMW",
        "--accepted-at", timestamp,
        "--output", @"C:\output.ppm",
    ];
}
