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

        return request.Mode == SstvMode.Robot36
            ? ComposeRobot(page, canvasWidth, canvasHeight, layout, cancellationToken)
            : ComposeSequential(page, canvasWidth, canvasHeight, layout, cancellationToken);
    }

    private RgbFrame ComposeSequential(
        DecodedPage page,
        int width,
        int height,
        HeaderLayout layout,
        CancellationToken cancellationToken)
    {
        var contentHeight = height - HeaderHeight;
        var fitted = _resampler.Fit(page, width, contentHeight, cancellationToken);
        var canvas = new byte[checked(width * height * 3)];
        fitted.Pixels.CopyTo(canvas.AsSpan(width * HeaderHeight * 3));
        canvas.AsSpan(0, width * HeaderHeight * 3).Fill(255);
        HeaderRenderer.Draw(canvas, width, height, layout);
        return RgbFrame.Create(width, height, canvas);
    }

    private RgbFrame ComposeRobot(
        DecodedPage page,
        int width,
        int height,
        HeaderLayout layout,
        CancellationToken cancellationToken)
    {
        var fitted = _resampler.Fit(page, width, height, cancellationToken);
        var canvas = fitted.Pixels.ToArray();
        var headerByteCount = width * HeaderHeight * 3;
        if (fitted.ContentBounds.Y >= HeaderHeight)
        {
            canvas.AsSpan(0, headerByteCount).Fill(255);
        }
        else
        {
            for (var index = 0; index < headerByteCount; index++)
            {
                canvas[index] = (byte)((canvas[index] + 255) / 2);
            }
        }

        HeaderRenderer.Draw(canvas, width, height, layout);
        return RgbFrame.Create(width, height, canvas);
    }
}
