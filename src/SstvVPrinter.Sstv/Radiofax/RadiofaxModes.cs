namespace SstvVPrinter.Sstv.Radiofax;

public static class RadiofaxModes
{
    public static RadiofaxModeDefinition Get(SstvMode mode) => mode switch
    {
        SstvMode.Radiofax120Ioc288 => new(mode, "Radiofax 120/288", 120, 288, 904, 864, 32, 1728, 675),
        SstvMode.Radiofax120Ioc576 => new(mode, "Radiofax 120/576", 120, 576, 1809, 1728, 64, 3456, 300),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    public static bool IsRadiofax(SstvMode mode) =>
        mode is SstvMode.Radiofax120Ioc288 or SstvMode.Radiofax120Ioc576;
}
