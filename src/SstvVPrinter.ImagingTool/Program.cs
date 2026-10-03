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
        var options = ToolOptionsParser.Parse(args);
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

// End of the command-line entry point and its local helpers.
