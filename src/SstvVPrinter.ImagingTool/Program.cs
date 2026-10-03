using System.Globalization;
using SstvVPrinter.Imaging.Composition;
using SstvVPrinter.Imaging.Header;
using SstvVPrinter.Imaging.Model;
using SstvVPrinter.Imaging.Pwg;
using SstvVPrinter.ImagingTool;
using SstvVPrinter.Sstv;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    try
    {
        var options = ParseArguments(args);
        await using var source = new FileStream(options.Input, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var page = await new PwgRasterDecoder().DecodeFirstPageAsync(source);
        var request = new CompositionRequest(options.Mode, options.Horizontal, options.CallSign, options.AcceptedAt);
        var frame = new SstvFrameComposer().Compose(page, request);
        await PpmWriter.WriteAsync(options.Output, frame);
        var contentTargetHeight = 240;
        var contentBounds = FitBounds(page.Width, page.Height, frame.Width, contentTargetHeight);
        if (options.Mode != SstvMode.Robot36)
            contentBounds = new(contentBounds.X, contentBounds.Y + 16, contentBounds.Width, contentBounds.Height);
        var metadataText = HeaderRenderer.MeasureAndSelect(request, frame.Width).MetadataText;
        var treatment = options.Mode != SstvMode.Robot36
            ? "dedicated"
            : contentBounds.Y >= 16 ? "natural-padding" : "50%-white-overlay";

        Console.WriteLine($"Physical: {page.Metadata.PhysicalWidth}x{page.Metadata.PhysicalHeight}");
        Console.WriteLine($"Logical: {page.Width}x{page.Height}");
        Console.WriteLine($"Orientation: {page.Metadata.Orientation}");
        Console.WriteLine($"Mode: {options.Mode}; horizontal: {options.Horizontal}x");
        Console.WriteLine($"Frame: {frame.Width}x{frame.Height}");
        Console.WriteLine($"Content bounds: {contentBounds.X},{contentBounds.Y} {contentBounds.Width}x{contentBounds.Height}");
        Console.WriteLine($"Header: {treatment}; metadata: {metadataText}");
        Console.WriteLine($"Output: {options.Output}");
        return 0;
    }
    catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or FormatException)
    {
        Console.Error.WriteLine($"Error: {exception.Message}");
        return 1;
    }
}

static PixelRect FitBounds(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
{
    var sourceIsWider = (long)sourceWidth * targetHeight >= (long)sourceHeight * targetWidth;
    var width = sourceIsWider ? targetWidth : Math.Max(1, (int)Math.Round((double)targetHeight * sourceWidth / sourceHeight, MidpointRounding.AwayFromZero));
    var height = sourceIsWider ? Math.Max(1, (int)Math.Round((double)targetWidth * sourceHeight / sourceWidth, MidpointRounding.AwayFromZero)) : targetHeight;
    width = Math.Min(width, targetWidth);
    height = Math.Min(height, targetHeight);
    return new((targetWidth - width) / 2, (targetHeight - height) / 2, width, height);
}

static ToolOptions ParseArguments(string[] args)
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
    if (!DateTimeOffset.TryParseExact(Required("--accepted-at"), "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var acceptedAt) &&
        !DateTimeOffset.TryParse(Required("--accepted-at"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out acceptedAt))
        throw new ArgumentException("--accepted-at must be an ISO-8601 timestamp with an offset.");

    return new(input, mode, horizontal, Required("--callsign"), acceptedAt, output);
}

internal sealed record ToolOptions(
    string Input, SstvMode Mode, int Horizontal, string CallSign, DateTimeOffset AcceptedAt, string Output);
