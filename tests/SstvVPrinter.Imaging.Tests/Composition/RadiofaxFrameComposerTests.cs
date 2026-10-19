using SstvVPrinter.Imaging.Composition;
using SstvVPrinter.Imaging.Model;
using SstvVPrinter.Imaging.Resampling;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Tests.Composition;

public sealed class RadiofaxFrameComposerTests
{
    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    [InlineData(210, 297)]
    [InlineData(297, 210)]
    [InlineData(2696, 1728)]
    public void Compose_accepts_inclusive_practical_page_ratios(int width, int height)
    {
        var frame = new RadiofaxFrameComposer().Compose(
            SolidPage(width, height, new(255, 0, 0)),
            Request(SstvMode.Radiofax120Ioc288));

        var expectedBodyHeight = (int)Math.Round(864.0 * height / width, MidpointRounding.AwayFromZero);
        Assert.Equal(864, frame.Width);
        Assert.Equal(32 + expectedBodyHeight, frame.Height);
        Assert.Equal(new Rgb24(77, 77, 77), frame.GetPixel(432, 32 + (expectedBodyHeight / 2)));
    }

    [Theory]
    [InlineData(2001, 1000)]
    [InlineData(1000, 2001)]
    [InlineData(3, 1)]
    [InlineData(1, 3)]
    public void Compose_rejects_page_ratios_outside_two_to_one(int width, int height)
    {
        var exception = Assert.Throws<RadiofaxPageShapeException>(() =>
            new RadiofaxFrameComposer().Compose(
                SolidPage(width, height, new(1, 2, 3)),
                Request(SstvMode.Radiofax120Ioc288)));

        Assert.Contains("1:2", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SstvMode.Radiofax120Ioc288, 864, 32, 1728)]
    [InlineData(SstvMode.Radiofax120Ioc576, 1728, 64, 3456)]
    public void Compose_uses_mode_width_header_and_maximum_body(
        SstvMode mode,
        int expectedWidth,
        int headerHeight,
        int maximumBodyHeight)
    {
        var frame = new RadiofaxFrameComposer().Compose(SolidPage(1, 2, new(8, 8, 8)), Request(mode));

        Assert.Equal(expectedWidth, frame.Width);
        Assert.Equal(headerHeight + maximumBodyHeight, frame.Height);
        new TransmissionEncodeOptions(mode).Validate(frame);
    }

    [Fact]
    public void Compose_propagates_cancellation_during_resampling()
    {
        using var cancellation = new CancellationTokenSource();
        var composer = new RadiofaxFrameComposer(new CancellingResampler(cancellation));

        Assert.Throws<OperationCanceledException>(() => composer.Compose(
            SolidPage(2, 1, new(10, 20, 30)),
            Request(SstvMode.Radiofax120Ioc288),
            cancellation.Token));
    }

    [Fact]
    public void Transmission_composer_preserves_sstv_and_dispatches_radiofax()
    {
        var composer = new TransmissionFrameComposer();

        var sstv = composer.Compose(SolidPage(2, 1, new(9, 8, 7)),
            new(SstvMode.Scottie2, 1, "JA1UMW", DateTimeOffset.UnixEpoch));
        var fax = composer.Compose(SolidPage(2, 1, new(9, 8, 7)),
            Request(SstvMode.Radiofax120Ioc288));

        Assert.Equal((320, 256), (sstv.Width, sstv.Height));
        Assert.Equal((864, 464), (fax.Width, fax.Height));
    }

    private static CompositionRequest Request(SstvMode mode) =>
        new(mode, 1, "JA1UMW", DateTimeOffset.UnixEpoch);

    private static DecodedPage SolidPage(int width, int height, Rgb24 color)
    {
        var bytes = new byte[checked(width * height * 3)];
        for (var index = 0; index < bytes.Length; index += 3)
        {
            bytes[index] = color.R;
            bytes[index + 1] = color.G;
            bytes[index + 2] = color.B;
        }

        return DecodedPage.Create(width, height, bytes, new(width, height, width, height, 300, 300, 0, 612, 792));
    }

    private sealed class CancellingResampler(CancellationTokenSource cancellation) : DetailPreservingResampler
    {
        public override FittedImage Fit(
            DecodedPage source,
            int targetWidth,
            int targetHeight,
            CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return base.Fit(source, targetWidth, targetHeight, cancellationToken);
        }
    }
}
