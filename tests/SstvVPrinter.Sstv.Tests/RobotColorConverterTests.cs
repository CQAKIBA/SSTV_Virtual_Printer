using SstvVPrinter.Sstv.Planning;

namespace SstvVPrinter.Sstv.Tests;

public sealed class RobotColorConverterTests
{
    public static TheoryData<Rgb24, byte, byte, byte> Colors => new()
    {
        { new Rgb24(0, 0, 0), 0, 128, 128 },
        { new Rgb24(255, 255, 255), 255, 128, 128 },
        { new Rgb24(128, 128, 128), 128, 128, 128 },
        { new Rgb24(255, 0, 0), 76, 255, 85 },
        { new Rgb24(0, 255, 0), 150, 21, 44 },
        { new Rgb24(0, 0, 255), 29, 107, 255 },
    };

    [Theory]
    [MemberData(nameof(Colors))]
    public void Convert_uses_approved_full_range_equations(Rgb24 input, byte y, byte redDifference, byte blueDifference)
    {
        Assert.Equal(new RobotComponents(y, redDifference, blueDifference), RobotColorConverter.Convert(input));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 1, 1)]
    [InlineData(1, 2, 2)]
    [InlineData(100, 200, 150)]
    [InlineData(255, 255, 255)]
    public void Average_uses_both_lines_and_rounds_half_up(byte first, byte second, byte expected)
    {
        Assert.Equal(expected, RobotColorConverter.Average(first, second));
    }
}
