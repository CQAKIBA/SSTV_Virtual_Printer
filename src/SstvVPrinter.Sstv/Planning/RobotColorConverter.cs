namespace SstvVPrinter.Sstv.Planning;

internal readonly record struct RobotComponents(byte Y, byte RedDifference, byte BlueDifference);

internal static class RobotColorConverter
{
    public static RobotComponents Convert(Rgb24 pixel) => new(
        RoundAndClamp((0.299000 * pixel.R) + (0.587000 * pixel.G) + (0.114000 * pixel.B)),
        RoundAndClamp(128 + (0.500000 * pixel.R) - (0.418688 * pixel.G) - (0.081312 * pixel.B)),
        RoundAndClamp(128 - (0.168736 * pixel.R) - (0.331264 * pixel.G) + (0.500000 * pixel.B)));

    public static byte Average(byte first, byte second) =>
        RoundAndClamp((first + second) / 2.0);

    private static byte RoundAndClamp(double value) =>
        (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), byte.MinValue, byte.MaxValue);
}
