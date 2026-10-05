using System.Globalization;
using System.Text.RegularExpressions;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.ImagingTool;

internal sealed record ToolOptions(
    string Input, SstvMode Mode, int Horizontal, string CallSign, DateTimeOffset AcceptedAt, string Output);

internal static class ToolOptionsParser
{
    public static ToolOptions Parse(string[] args)
    {
        if (args.Length != 12)
        {
            throw new ArgumentException("Expected --input, --mode, --horizontal, --callsign, --accepted-at, and --output.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || !values.TryAdd(args[index], args[index + 1]))
                throw new ArgumentException($"Invalid or duplicate argument: {args[index]}");
        }

        string Required(string name) => values.TryGetValue(name, out var value)
            ? value
            : throw new ArgumentException($"Missing argument: {name}");

        var input = Required("--input");
        var output = Required("--output");
        if (!Path.IsPathFullyQualified(input) || !input.EndsWith(".pwg", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("--input must be an absolute .pwg path.");
        if (!Path.IsPathFullyQualified(output) || !output.EndsWith(".ppm", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("--output must be an absolute .ppm path.");

        var mode = Required("--mode").ToLowerInvariant() switch
        {
            "martin1" => SstvMode.Martin1,
            "scottie1" => SstvMode.Scottie1,
            "scottie2" => SstvMode.Scottie2,
            "robot36" => SstvMode.Robot36,
            var value => throw new ArgumentException($"Unsupported mode: {value}"),
        };
        if (!int.TryParse(Required("--horizontal"), NumberStyles.None, CultureInfo.InvariantCulture, out var horizontal))
            throw new ArgumentException("--horizontal must be 1 or 2.");
        var timestamp = Required("--accepted-at");
        if (!Regex.IsMatch(timestamp, @"^\d{4}-\d{2}-\d{2}T.*(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant) ||
            !DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var acceptedAt))
            throw new ArgumentException("--accepted-at must be an ISO-8601 timestamp with an offset.");

        return new(input, mode, horizontal, Required("--callsign"), acceptedAt, output);
    }
}
