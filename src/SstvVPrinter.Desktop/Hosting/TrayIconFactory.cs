using Avalonia.Controls;

namespace SstvVPrinter.Desktop.Hosting;

internal static class TrayIconFactory
{
    public static TrayIcon Create(Action show, Action exit)
    {
        var open = new NativeMenuItem("開く");
        open.Click += (_, _) => show();
        var quit = new NativeMenuItem("終了");
        quit.Click += (_, _) => exit();
        var menu = new NativeMenu();
        menu.Add(open);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(quit);
        var tray = new TrayIcon
        {
            Icon = CreateIcon(),
            ToolTipText = "SSTV Virtual Printer",
            Menu = menu,
            IsVisible = true,
        };
        tray.Clicked += (_, _) => show();
        return tray;
    }

    private static WindowIcon CreateIcon()
    {
        const int width = 16;
        const int height = 16;
        const int pixelBytes = width * height * 4;
        const int maskBytes = height * 4;
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)1);
            writer.Write((byte)width); writer.Write((byte)height); writer.Write((byte)0); writer.Write((byte)0);
            writer.Write((ushort)1); writer.Write((ushort)32);
            writer.Write(40 + pixelBytes + maskBytes); writer.Write(22);
            writer.Write(40); writer.Write(width); writer.Write(height * 2);
            writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(0); writer.Write(pixelBytes);
            writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var magenta = x is >= 2 and <= 5 || y is >= 10 and <= 13;
                    writer.Write(magenta ? (byte)216 : (byte)255);
                    writer.Write(magenta ? (byte)39 : (byte)229);
                    writer.Write(magenta ? (byte)255 : (byte)0);
                    writer.Write((byte)255);
                }
            }
            writer.Write(new byte[maskBytes]);
        }
        stream.Position = 0;
        return new WindowIcon(stream);
    }
}
