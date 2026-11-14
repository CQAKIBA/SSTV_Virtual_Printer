using SstvVPrinter.Imaging.Model;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Composition;

public sealed class TransmissionFrameComposer
{
    private readonly SstvFrameComposer _sstvComposer;
    private readonly RadiofaxFrameComposer _radiofaxComposer;

    public TransmissionFrameComposer()
        : this(new SstvFrameComposer(), new RadiofaxFrameComposer())
    {
    }

    public TransmissionFrameComposer(SstvFrameComposer sstvComposer, RadiofaxFrameComposer radiofaxComposer)
    {
        _sstvComposer = sstvComposer ?? throw new ArgumentNullException(nameof(sstvComposer));
        _radiofaxComposer = radiofaxComposer ?? throw new ArgumentNullException(nameof(radiofaxComposer));
    }

    public RgbFrame Compose(
        DecodedPage page,
        CompositionRequest request,
        CancellationToken cancellationToken = default) =>
        TransmissionModes.IsRadiofax(request.Mode)
            ? _radiofaxComposer.Compose(page, request, cancellationToken)
            : _sstvComposer.Compose(page, request, cancellationToken);
}
