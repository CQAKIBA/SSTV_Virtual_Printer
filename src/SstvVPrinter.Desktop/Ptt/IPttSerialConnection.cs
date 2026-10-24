namespace SstvVPrinter.Desktop.Ptt;

public interface IPttSerialConnection : IDisposable
{
    bool IsOpen { get; }
    void Open();
    void Close();
    void SetRts(bool value);
    void SetDtr(bool value);
}

public interface IPttAutomaticLease : IAsyncDisposable
{
    CancellationToken FaultToken { get; }
    Task ReleaseAsync();
    Task AbortAsync();
}
