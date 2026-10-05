namespace SstvVPrinter.Sstv;

public static class SstvModes
{
    public static SstvModeDefinition Get(SstvMode mode) => mode switch
    {
        SstvMode.Martin1 => new(mode, "Martin 1", 44, 320, 256, SstvScanFamily.Martin),
        SstvMode.Scottie1 => new(mode, "Scottie 1", 60, 320, 256, SstvScanFamily.Scottie),
        SstvMode.Scottie2 => new(mode, "Scottie 2", 56, 320, 256, SstvScanFamily.Scottie),
        SstvMode.Robot36 => new(mode, "Robot 36", 8, 320, 240, SstvScanFamily.Robot36),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
}
