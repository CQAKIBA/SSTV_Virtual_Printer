namespace SstvVPrinter.Desktop.Ptt;

public interface IPttPlatform
{
    string[] GetPortNames();
    IPttSerialConnection CreateConnection(string portName);
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
