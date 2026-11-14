using SstvVPrinter.Imaging.Header;
using SstvVPrinter.Imaging.Model;
using SstvVPrinter.Imaging.Resampling;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Composition;

public sealed class SstvFrameComposer
{
    private const int HeaderHeight = 16;
    private readonly DetailPreservingResampler _resampler;

    public SstvFrameComposer() : this(new DetailPreservingResampler())
    {
    }

    public SstvFrameComposer(DetailPreservingResampler resampler)
    {
        _resampler = resampler ?? throw new ArgumentNullException(nameof(resampler));
    }

    public RgbFrame Compose(
        DecodedPage page,
        CompositionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var definition = SstvModes.Get(request.Mode);
        var canvasWidth = checked(definition.Width * request.HorizontalSampleMultiplier);
        var canvasHeight = definition.Height;
        var layout = HeaderRenderer.MeasureAndSelect(request, canvasWidth);

        return request.Placement == HeaderPlacement.Overlay
            ? ComposeOverlay(page, canvasWidth, canvasHeight, request.HorizontalSampleMultiplier, layout, cancellationToken)
            : ComposeReserved(page, canvasWidth, canvasHeight, request.HorizontalSampleMultiplier, layout, cancellationToken);
    }

    private RgbFrame ComposeReserved(
        DecodedPage page,
        int width,
        int height,
        int horizontalSampleMultiplier,
        HeaderLayout layout,
        CancellationToken cancellationToken)
    {
        var contentHeight = height - HeaderHeight;
        var fitted = _resampler.Fit(page, width, contentHeight, horizontalSampleMultiplier, cancellationToken);
        var canvas = new byte[checked(width * height * 3)];
        fitted.Pixels.CopyTo(canvas.AsSpan(width * HeaderHeight * 3));
        canvas.AsSpan(0, width * HeaderHeight * 3).Fill(255);
        HeaderRenderer.Draw(canvas, width, height, layout);
        return RgbFrame.Create(width, height, canvas);
    }

    private RgbFrame ComposeOverlay(
        DecodedPage page,
        int width,
        int height,
        int horizontalSampleMultiplier,
        HeaderLayout layout,
        CancellationToken cancellationToken)
    {
        var fitted = _resampler.Fit(page, width, height, horizontalSampleMultiplier, cancellationToken);
        var canvas = fitted.Pixels.ToArray();
        var headerByteCount = width * HeaderHeight * 3;
        for (var index = 0; index < headerByteCount; index++)
        {
            canvas[index] = (byte)((canvas[index] + 255) / 2);
        }

        HeaderRenderer.Draw(canvas, width, height, layout);
        return RgbFrame.Create(width, height, canvas);
    }
}
