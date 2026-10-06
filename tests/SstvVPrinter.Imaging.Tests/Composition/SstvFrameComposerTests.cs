using SstvVPrinter.Imaging.Composition;
using SstvVPrinter.Imaging.Model;
using SstvVPrinter.Imaging.Resampling;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Tests.Composition;

public sealed class SstvFrameComposerTests
{
    [Theory]
    [InlineData(SstvMode.Martin1, 1, 320)]
    [InlineData(SstvMode.Scottie1, 2, 640)]
    [InlineData(SstvMode.Scottie2, 1, 320)]
    public void Compose_sequential_modes_reserve_exact_header_and_content(SstvMode mode, int multiplier, int width)
    {
        var page = SolidPage(width / multiplier, 240, new(21, 43, 65));
        var request = new CompositionRequest(mode, multiplier, "JA1UMW", DateTimeOffset.UnixEpoch);
        var frame = new SstvFrameComposer().Compose(page, request);

        Assert.Equal(width, frame.Width);
        Assert.Equal(256, frame.Height);
        Assert.Equal(new Rgb24(255, 0, 0), frame.GetPixel(0, 0));
        Assert.Equal(new Rgb24(255, 255, 255), frame.GetPixel(width - 1, 15));
        Assert.Equal(new Rgb24(21, 43, 65), frame.GetPixel(width / 2, 16));
        Assert.Equal(new Rgb24(21, 43, 65), frame.GetPixel(width / 2, 255));
        new SstvEncodeOptions(mode, multiplier).Validate(frame);
    }

    [Fact]
    public void Compose_sequential_mode_letterboxes_content_in_black()
    {
        var frame = new SstvFrameComposer().Compose(
            SolidPage(100, 240, new(100, 110, 120)),
            new(SstvMode.Martin1, 1, "JA1UMW", DateTimeOffset.UnixEpoch));
        Assert.Equal(new Rgb24(0, 0, 0), frame.GetPixel(0, 100));
        Assert.Equal(new Rgb24(100, 110, 120), frame.GetPixel(160, 100));
    }

    [Fact]
    public void Double_horizontal_mode_preserves_display_aspect_while_using_all_samples()
    {
        var color = new Rgb24(70, 80, 90);
        var frame = new SstvFrameComposer().Compose(
            SolidPage(320, 240, color),
            new(SstvMode.Scottie2, 2, "JA1UMW", DateTimeOffset.UnixEpoch));

        Assert.Equal(640, frame.Width);
        Assert.Equal(color, frame.GetPixel(0, 16));
        Assert.Equal(color, frame.GetPixel(639, 255));
    }

    [Fact]
    public void Compose_validates_header_before_resampling()
    {
        var spy = new SpyResampler();
        var composer = new SstvFrameComposer(spy);
        var request = new CompositionRequest(SstvMode.Martin1, 1, new string('A', 100), DateTimeOffset.UnixEpoch);
        Assert.Throws<ArgumentException>(() => composer.Compose(SolidPage(1, 1, new(0, 0, 0)), request));
        Assert.False(spy.WasCalled);
    }

    [Theory]
    [InlineData(1, 320)]
    [InlineData(2, 640)]
    public void Robot36_uses_padding_only_when_top_gap_is_at_least_sixteen(int multiplier, int width)
    {
        var composer = new SstvFrameComposer();
        var padded = composer.Compose(SolidPage(width / multiplier, 208, new(11, 21, 31)),
            new(SstvMode.Robot36, multiplier, "JA1UMW", DateTimeOffset.UnixEpoch));
        var overlaid = composer.Compose(SolidPage(width / multiplier, 210, new(11, 21, 31)),
            new(SstvMode.Robot36, multiplier, "JA1UMW", DateTimeOffset.UnixEpoch));

        Assert.Equal(width, padded.Width);
        Assert.Equal(240, padded.Height);
        Assert.Equal(new Rgb24(255, 255, 255), padded.GetPixel(width - 1, 15));
        Assert.Equal(new Rgb24(133, 138, 143), overlaid.GetPixel(width - 1, 15));
        Assert.Equal(new Rgb24(11, 21, 31), padded.GetPixel(width / 2, 16));
        new SstvEncodeOptions(SstvMode.Robot36, multiplier).Validate(padded);
    }

    [Fact]
    public void Robot36_overlay_uses_integer_half_white_and_opaque_foreground()
    {
        var frame = new SstvFrameComposer().Compose(SolidPage(320, 240, new(10, 11, 12)),
            new(SstvMode.Robot36, 1, "JA1UMW", DateTimeOffset.UnixEpoch));
        Assert.Equal(new Rgb24(255, 0, 0), frame.GetPixel(0, 0));
        Assert.Equal(new Rgb24(132, 133, 133), frame.GetPixel(319, 15));
        Assert.Equal(new Rgb24(10, 11, 12), frame.GetPixel(319, 16));
    }

    [Fact]
    public async Task Compose_is_deterministic_concurrent_and_cancellable()
    {
        var page = SolidPage(320, 240, new(91, 92, 93));
        var request = new CompositionRequest(SstvMode.Robot36, 1, "JA1UMW", DateTimeOffset.UnixEpoch);
        var composer = new SstvFrameComposer();
        var frames = await Task.WhenAll(Task.Run(() => composer.Compose(page, request)), Task.Run(() => composer.Compose(page, request)));
        Assert.Equal(frames[0].GetPixel(200, 100), frames[1].GetPixel(200, 100));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => composer.Compose(page, request, cancellation.Token));
    }

    private static DecodedPage SolidPage(int width, int height, Rgb24 color)
    {
        var bytes = new byte[width * height * 3];
        for (var index = 0; index < bytes.Length; index += 3)
        {
            bytes[index] = color.R;
            bytes[index + 1] = color.G;
            bytes[index + 2] = color.B;
        }
        return DecodedPage.Create(width, height, bytes, new(width, height, width, height, 300, 300, 0, 612, 792));
    }

    private sealed class SpyResampler : DetailPreservingResampler
    {
        public bool WasCalled { get; private set; }
        public override FittedImage Fit(DecodedPage source, int targetWidth, int targetHeight, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return base.Fit(source, targetWidth, targetHeight, cancellationToken);
        }
    }
}
