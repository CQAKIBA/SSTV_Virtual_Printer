using System.IO.Ports;
using SstvVPrinter.Desktop.Ptt;

namespace SstvVPrinter.Desktop.Platforms.Windows;

public sealed class WindowsPttPlatform : IPttPlatform
{
    public string[] GetPortNames() => SerialPort.GetPortNames();
    public IPttSerialConnection CreateConnection(string portName) => new Connection(portName);
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.Delay(delay, cancellationToken);

    private sealed class Connection(string portName) : IPttSerialConnection
    {
        private readonly SerialPort serial = new(portName)
        {
            Handshake = Handshake.None,
            RtsEnable = false,
            DtrEnable = false,
        };
        public bool IsOpen => serial.IsOpen;
        public void Open() => serial.Open();
        public void Close() => serial.Close();
        public void SetRts(bool value) => serial.RtsEnable = value;
        public void SetDtr(bool value) => serial.DtrEnable = value;
        public void Dispose() => serial.Dispose();
    }
}
