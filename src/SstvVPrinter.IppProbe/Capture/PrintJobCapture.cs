using System.Security.Cryptography;
using System.Text.Json;
using SstvVPrinter.IppProbe.Ipp;

namespace SstvVPrinter.IppProbe.Capture;

public sealed class PrintJobCapture(CaptureOptions options)
{
    public static int FindHighestJobId(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return 0;
        int highest = 0;
        foreach (string path in Directory.EnumerateFiles(directoryPath))
        {
            string extension = Path.GetExtension(path);
            if (extension is not ".pwg" and not ".json") continue;
            if (int.TryParse(Path.GetFileNameWithoutExtension(path), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int id) && id > highest && id < int.MaxValue)
                highest = id;
        }
        return highest;
    }

    public async Task<CapturedJob> CaptureAsync(ProbeJob job, Stream document, string documentFormat, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.DirectoryPath);
        string stem = job.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string payload = Path.Combine(options.DirectoryPath, $"{stem}.pwg");
        string metadata = Path.Combine(options.DirectoryPath, $"{stem}.json");
        string nonce = Guid.NewGuid().ToString("N");
        string payloadTemp = Path.Combine(options.DirectoryPath, $".{stem}.{nonce}.pwg.tmp");
        string metadataTemp = Path.Combine(options.DirectoryPath, $".{stem}.{nonce}.json.tmp");
        try
        {
            long count = 0; using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(payloadTemp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                byte[] buffer = new byte[81920]; int read;
                while ((read = await document.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    count = checked(count + read); if (count > CaptureOptions.MaximumDocumentBytes) throw new InvalidDataException("Print document exceeds 64 MiB.");
                    hash.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                await output.FlushAsync(cancellationToken);
            }
            var result = new CapturedJob(job.Id, job.UserName, job.JobName, documentFormat, count, Convert.ToHexString(hash.GetHashAndReset()), DateTimeOffset.UtcNow, payload, metadata);
            await File.WriteAllTextAsync(metadataTemp, JsonSerializer.Serialize(result), cancellationToken);
            File.Move(payloadTemp, payload, false); File.Move(metadataTemp, metadata, false); return result;
        }
        catch
        {
            TryDelete(payloadTemp); TryDelete(metadataTemp); throw;
        }
    }
    private static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}
