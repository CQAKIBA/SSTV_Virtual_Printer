namespace SstvVPrinter.Sstv.Radiofax;

public static class RadiofaxModes
{
    public static RadiofaxModeDefinition Get(SstvMode mode) => mode switch
    {
        SstvMode.Radiofax120Ioc288Sync10Ms => new(mode, "RadioFAX - 120/288/10ms", 120, 288, 904, 864, 32, 1728, 675, 10),
        SstvMode.Radiofax120Ioc288Sync20Ms => new(mode, "RadioFAX - 120/288/20ms", 120, 288, 904, 864, 32, 1728, 675, 20),
        SstvMode.Radiofax120Ioc576Sync10Ms => new(mode, "RadioFAX - 120/576/10ms", 120, 576, 1809, 1728, 64, 3456, 300, 10),
        SstvMode.Radiofax120Ioc576Sync20Ms => new(mode, "RadioFAX - 120/576/20ms (JMH Standard)", 120, 576, 1809, 1728, 64, 3456, 300, 20),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    public static bool IsRadiofax(SstvMode mode) =>
        mode is SstvMode.Radiofax120Ioc288Sync10Ms
            or SstvMode.Radiofax120Ioc288Sync20Ms
            or SstvMode.Radiofax120Ioc576Sync10Ms
            or SstvMode.Radiofax120Ioc576Sync20Ms;
}
