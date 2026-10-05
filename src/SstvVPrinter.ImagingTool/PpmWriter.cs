using System.Text;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.ImagingTool;

public static class PpmWriter
{
    public static async Task WriteAsync(
        string destinationPath,
        RgbFrame frame,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(frame);
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(destinationPath))
        {
            throw new IOException($"Destination already exists: {destinationPath}");
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Output directory does not exist: {directory}");
        }

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var header = Encoding.ASCII.GetBytes($"P6\n{frame.Width} {frame.Height}\n255\n");
                await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
                var row = new byte[checked(frame.Width * 3)];
                for (var y = 0; y < frame.Height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    for (var x = 0; x < frame.Width; x++)
                    {
                        var pixel = frame.GetPixel(x, y);
                        row[x * 3] = pixel.R;
                        row[(x * 3) + 1] = pixel.G;
                        row[(x * 3) + 2] = pixel.B;
                    }

                    await stream.WriteAsync(row, cancellationToken).ConfigureAwait(false);
                }

                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, destinationPath, overwrite: false);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
