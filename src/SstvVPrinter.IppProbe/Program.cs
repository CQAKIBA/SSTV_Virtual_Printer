using SstvVPrinter.IppProbe.Hosting;

string? captureDirectory = null;
int port = 8631;
for (int index = 0; index < args.Length; index++)
{
    if (args[index] == "--capture-directory" && index + 1 < args.Length) captureDirectory = args[++index];
    else if (args[index] == "--port" && index + 1 < args.Length && int.TryParse(args[++index], out int parsed)) port = parsed;
    else throw new ArgumentException($"Unknown or incomplete argument: {args[index]}");
}
if (string.IsNullOrWhiteSpace(captureDirectory) || !Path.IsPathFullyQualified(captureDirectory))
    throw new ArgumentException("--capture-directory must be an absolute path.");

var app = ProbeApplication.Build(captureDirectory, port);
Console.WriteLine($"SSTV IPP probe: ipp://localhost:{port}/ipp/print");
Console.WriteLine($"Captures: {captureDirectory}");
await app.RunAsync();
