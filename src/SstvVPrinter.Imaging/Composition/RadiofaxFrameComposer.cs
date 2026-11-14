using SstvVPrinter.Imaging.Model;
using SstvVPrinter.Imaging.Resampling;
using SstvVPrinter.Imaging.Header;
using SstvVPrinter.Sstv;
using SstvVPrinter.Sstv.Radiofax;

namespace SstvVPrinter.Imaging.Composition;

public sealed class RadiofaxFrameComposer
{
    private readonly DetailPreservingResampler _resampler;

    public RadiofaxFrameComposer() : this(new DetailPreservingResampler())
    {
    }

    public RadiofaxFrameComposer(DetailPreservingResampler resampler)
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
        var definition = RadiofaxModes.Get(request.Mode);
        var longer = Math.Max(page.Width, page.Height);
        var shorter = Math.Min(page.Width, page.Height);
        if (checked((long)longer) > checked(2L * shorter))
        {
            throw new RadiofaxPageShapeException();
        }

        var bodyHeight = Math.Max(1, (int)Math.Round(
            (double)definition.ActiveWidth * page.Height / page.Width,
            MidpointRounding.AwayFromZero));
        if (bodyHeight > definition.MaximumBodyLines)
        {
            throw new RadiofaxPageShapeException();
        }

        var contentHeight = request.Placement == HeaderPlacement.ReserveSpace
            ? bodyHeight - definition.HeaderHeight
            : bodyHeight;
        var fitted = _resampler.Fit(page, definition.ActiveWidth, contentHeight, cancellationToken);
        var canvas = new byte[checked(definition.ActiveWidth * bodyHeight * 3)];
        var headerByteCount = definition.ActiveWidth * definition.HeaderHeight * 3;
        if (request.Placement == HeaderPlacement.ReserveSpace)
        {
            canvas.AsSpan(0, headerByteCount).Fill(255);
        }
        var destination = request.Placement == HeaderPlacement.ReserveSpace
            ? canvas.AsSpan(headerByteCount)
            : canvas.AsSpan();
        var source = fitted.Pixels;
        for (var index = 0; index < source.Length; index += 3)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var luminance = (byte)((77 * source[index] + 150 * source[index + 1] + 29 * source[index + 2] + 128) >> 8);
            destination[index] = luminance;
            destination[index + 1] = luminance;
            destination[index + 2] = luminance;
        }

        if (request.Placement == HeaderPlacement.Overlay)
        {
            for (var index = 0; index < headerByteCount; index++)
            {
                canvas[index] = (byte)((canvas[index] + 255) / 2);
            }
        }

        RadiofaxHeaderRenderer.Draw(canvas, definition.ActiveWidth, bodyHeight, request);

        return RgbFrame.Create(definition.ActiveWidth, bodyHeight, canvas);
    }
}
