using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using SstvVPrinter.IppProbe.Capture;
using SstvVPrinter.IppProbe.Ipp;
namespace SstvVPrinter.IppProbe.Hosting;

public static class ProbeApplication
{
    public static WebApplication Build(string captureDirectory, int port)
    {
        if (port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port)); var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options => { LoopbackBinding.Validate(IPAddress.Loopback); LoopbackBinding.Validate(IPAddress.IPv6Loopback); options.Limits.MaxRequestBodySize = IppEndpoint.MaximumRequestBytes; options.Listen(IPAddress.Loopback, port, l => l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1); options.Listen(IPAddress.IPv6Loopback, port, l => l.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1); });
        WebApplication app = builder.Build();
        string printerUri = port == 0 ? ProbePrinter.DefaultPrinterUri : $"ipp://localhost:{port}/ipp/print";
        IppEndpoint.Map(app, new ProbePrinter(printerUri, PrintJobCapture.FindHighestJobId(captureDirectory)), new PrintJobCapture(new CaptureOptions(captureDirectory)));
        return app;
    }
}
