using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SstvVPrinter.App.Platform;

namespace SstvVPrinter.Desktop.Platforms.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsUserAttentionService(Func<nint> windowHandle, Action<string, string>? notification = null)
    : IUserAttentionService
{
    public bool TryBringToForeground()
    {
        var handle = windowHandle();
        return handle != 0 && SetForegroundWindow(handle);
    }

    public void FlashTaskbar()
    {
        var info = new FlashWindowInfo
        {
            Size = (uint)Marshal.SizeOf<FlashWindowInfo>(),
            Window = windowHandle(),
            Flags = 3,
            Count = 3,
            Timeout = 0,
        };
        if (info.Window != 0) FlashWindowEx(ref info);
    }

    public void ShowNotification(string title, string message) => notification?.Invoke(title, message);

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashWindowInfo
    {
        public uint Size;
        public nint Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashWindowInfo info);
}
