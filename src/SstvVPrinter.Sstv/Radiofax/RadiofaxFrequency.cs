namespace SstvVPrinter.Sstv.Radiofax;

public static class RadiofaxFrequency
{
    public static double FromLuminance(byte value) => 1500.0 + (value * (800.0 / 255.0));
}
