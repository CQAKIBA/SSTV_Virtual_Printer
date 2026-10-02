namespace SstvVPrinter.Sstv;

public enum SstvScanFamily
{
    Martin,
    Scottie,
    Robot36,
}

public sealed record SstvModeDefinition(
    SstvMode Mode,
    string DisplayName,
    byte VisCode,
    int Width,
    int Height,
    SstvScanFamily ScanFamily);
