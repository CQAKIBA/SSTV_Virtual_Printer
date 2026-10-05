namespace SstvVPrinter.Sstv.Signal;

public static class VideoFrequency
{
    public static double FromByte(byte value) => 1500.0 + (800.0 * value / 255.0);
}
