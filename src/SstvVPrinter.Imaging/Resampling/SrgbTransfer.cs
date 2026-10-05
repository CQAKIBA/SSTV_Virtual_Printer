namespace SstvVPrinter.Imaging.Resampling;

internal static class SrgbTransfer
{
    private static readonly double[] LinearTable = CreateTable();

    public static double ToLinear(byte value) => LinearTable[value];

    public static byte ToByte(double linear)
    {
        linear = Math.Clamp(linear, 0, 1);
        var srgb = linear <= 0.0031308
            ? linear * 12.92
            : (1.055 * Math.Pow(linear, 1.0 / 2.4)) - 0.055;
        return (byte)Math.Clamp(Math.Round(srgb * 255, MidpointRounding.AwayFromZero), 0, 255);
    }

    private static double[] CreateTable()
    {
        var table = new double[256];
        for (var index = 0; index < table.Length; index++)
        {
            var value = index / 255.0;
            table[index] = value <= 0.04045
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return table;
    }
}
