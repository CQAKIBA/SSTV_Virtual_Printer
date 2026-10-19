using SstvVPrinter.App.Jobs;
using SstvVPrinter.App.Transmission;
using SstvVPrinter.Imaging.Composition;
using SstvVPrinter.Imaging.Model;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.App.Tests.Transmission;

public sealed class TransmissionPreparationServiceTests
{
    [Fact]
    public void Transmission_settings_accept_radiofax_mode_metadata()
    {
        var settings = new TransmissionSettings("TEST", SstvMode.Radiofax120Ioc576, 1);

        Assert.Equal(SstvMode.Radiofax120Ioc576, settings.Mode);
    }

    [Theory]
    [InlineData(SstvMode.Scottie2, 1, 320, 256)]
    [InlineData(SstvMode.Scottie2, 2, 640, 256)]
    [InlineData(SstvMode.Robot36, 1, 320, 240)]
    public async Task Prepare_produces_mode_canvas(
        SstvMode mode, int multiplier, int expectedWidth, int expectedHeight)
    {
        var service = new TransmissionPreparationService(new SstvFrameComposer());

        var result = await service.PrepareAsync(
            CreateJob(), new TransmissionSettings("JA1UMW", mode, multiplier), CancellationToken.None);

        Assert.Equal(expectedWidth, result.Frame.Width);
        Assert.Equal(expectedHeight, result.Frame.Height);
        Assert.Equal(mode, result.Options.Mode);
        Assert.Equal(multiplier, result.Options.HorizontalSampleMultiplier);
        Assert.Equal(SstvDurationEstimator.Estimate(result.Options), result.EstimatedDuration);
    }

    [Fact]
    public async Task Prepare_uses_job_time_converted_to_JST()
    {
        var service = new TransmissionPreparationService(new SstvFrameComposer());
        var job = CreateJob(new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero));

        var result = await service.PrepareAsync(
            job, new TransmissionSettings("JA1UMW", SstvMode.Scottie2, 1), CancellationToken.None);

        Assert.Equal(new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.FromHours(9)), result.HeaderTime);
    }

    [Fact]
    public async Task Prepare_rotates_source_before_fitting_it_to_the_sstv_canvas()
    {
        var service = new TransmissionPreparationService(new SstvFrameComposer());

        var result = await service.PrepareAsync(
            CreateJob(), new TransmissionSettings("JA1UMW", SstvMode.Scottie2, 1, 90),
            CancellationToken.None);

        Assert.Equal(new Rgb24(0, 0, 0), result.Frame.GetPixel(0, 128));
        Assert.NotEqual(new Rgb24(0, 0, 0), result.Frame.GetPixel(160, 128));
    }

    private static PrintJob CreateJob(DateTimeOffset? acceptedAt = null) =>
        new(
            Guid.NewGuid(),
            "Paint",
            acceptedAt ?? DateTimeOffset.UtcNow,
            DecodedPage.Create(
                2, 1, [255, 0, 0, 0, 255, 0],
                new PwgPageMetadata(2, 1, 2, 1, 300, 300, 0, 612, 792)));
}
