using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Tests;

public sealed class VideoFrequencyTests
{
    [Theory]
    [InlineData(0, 1500.0)]
    [InlineData(128, 1901.5686274509803)]
    [InlineData(255, 2300.0)]
    public void FromByte_maps_full_video_range(byte value, double expected)
    {
        Assert.Equal(expected, VideoFrequency.FromByte(value), 6);
    }
}
