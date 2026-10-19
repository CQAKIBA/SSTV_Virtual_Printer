namespace SstvVPrinter.Sstv.Radiofax;

public sealed record RadiofaxModeDefinition(
    SstvMode Mode,
    string DisplayName,
    int LinesPerMinute,
    int Ioc,
    int TotalLineGrid,
    int ActiveWidth,
    int HeaderHeight,
    int MaximumBodyLines,
    double IocSelectionHz);
