namespace SstvVPrinter.Desktop.Ptt;

public sealed record PttSnapshot(
    string? PortName,
    PttControlMethod Method,
    bool IsOpen,
    bool IsAsserted,
    bool IsBusy,
    string? Error);
