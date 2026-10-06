using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Desktop.Imaging;

public static class AvaloniaFrameBitmapFactory
{
    public static WriteableBitmap Create(RgbFrame frame, int verticalScale = 1)
    {
        ArgumentNullException.ThrowIfNull(frame);
        frame = PreviewFrameFactory.Create(frame, verticalScale);
        var bitmap = new WriteableBitmap(
            new PixelSize(frame.Width, frame.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
        var rgb = new byte[checked(frame.Width * frame.Height * 3)];
        frame.CopyPixelsTo(rgb);
        using var buffer = bitmap.Lock();
        var row = new byte[buffer.RowBytes];
        for (var y = 0; y < frame.Height; y++)
        {
            Array.Clear(row);
            for (var x = 0; x < frame.Width; x++)
            {
                var source = ((y * frame.Width) + x) * 3;
                var target = x * 4;
                row[target] = rgb[source + 2];
                row[target + 1] = rgb[source + 1];
                row[target + 2] = rgb[source];
                row[target + 3] = 255;
            }

            Marshal.Copy(row, 0, buffer.Address + (y * buffer.RowBytes), buffer.RowBytes);
        }

        return bitmap;
    }
}
