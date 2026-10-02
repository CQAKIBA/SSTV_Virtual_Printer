using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Net.Http.Headers;
using SstvVPrinter.IppProbe.Capture;
using SstvVPrinter.IppProbe.Ipp;
namespace SstvVPrinter.IppProbe.Hosting;

public static class IppEndpoint
{
    public const long MaximumRequestBytes = CaptureOptions.MaximumDocumentBytes + 1024 * 1024;
    public static void Map(IEndpointRouteBuilder endpoints, ProbePrinter printer, PrintJobCapture capture) => endpoints.MapPost("/ipp/print", async context =>
    {
        if (!MediaTypeHeaderValue.TryParse(context.Request.ContentType, out MediaTypeHeaderValue? contentType) ||
            !string.Equals(contentType.MediaType, "application/ipp", StringComparison.OrdinalIgnoreCase)) { context.Response.StatusCode = 415; return; }
        if (context.Request.ContentLength > MaximumRequestBytes) { context.Response.StatusCode = 413; return; }
        IppMessage request;
        try { request = await IppMessageReader.ReadAsync(context.Request.Body, context.RequestAborted); }
        catch (IppProtocolException exception) when (exception.Version.HasValue && exception.RequestId.HasValue)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/ipp";
            await IppMessageWriter.WriteAsync(context.Response.Body, ProtocolError(exception), context.RequestAborted);
            return;
        }
        catch (IppProtocolException) { context.Response.StatusCode = 400; return; }
        Console.WriteLine($"IPP request 0x{request.Code:X4} id={request.RequestId}: {string.Join(", ", request.Groups.SelectMany(group => group.Attributes).Select(attribute => $"{attribute.Name}=[{string.Join("|", attribute.Values.Select(value => value.AsString()))}]"))}");
        IppMessage response = printer.CreateResponse(request);
        if (request.Code == IppOperations.PrintJob && response.Code == IppStatusCodes.SuccessfulOk)
        {
            var job = printer.AllocateJob(request.FindAttribute("requesting-user-name")?.Values[0].AsString() ?? "unknown", request.FindAttribute("job-name")?.Values[0].AsString());
            try
            {
                CapturedJob captured = await capture.CaptureAsync(job, context.Request.Body, request.GetRequiredString("document-format"), context.RequestAborted);
                await using var payload = File.OpenRead(captured.PayloadPath);
                PwgRasterReport report = await PwgRasterInspector.InspectAsync(payload, context.RequestAborted);
                Console.WriteLine($"Job {captured.JobId}: {captured.ByteLength} bytes SHA-256 {captured.Sha256}; PWG={report.IsPwgRaster} {report.Width}x{report.Height} {report.XResolution}x{report.YResolution} dpi; orientation={report.Orientation} landscape={report.IsLandscape} page={report.PageWidthPoints}x{report.PageHeightPoints}pt");
            }
            catch (InvalidDataException) { context.Response.StatusCode = 413; return; }
            response = WithJob(response, job);
        }
        Console.WriteLine($"IPP response 0x{response.Code:X4} id={response.RequestId}: {string.Join(", ", response.Groups.SelectMany(group => group.Attributes).Select(attribute => attribute.Name))}");
        context.Response.StatusCode = 200; context.Response.ContentType = "application/ipp"; await IppMessageWriter.WriteAsync(context.Response.Body, response, context.RequestAborted);
    });
    static IppMessage WithJob(IppMessage r, ProbeJob j) => new(r.Version, r.Code, r.RequestId, [.. r.Groups, new IppAttributeGroup(IppDelimiterTags.JobAttributes, [new IppAttribute("job-id", [IppValue.FromInt32(IppValueTags.Integer, j.Id)]), new IppAttribute("job-uri", [IppValue.FromString(IppValueTags.Uri, j.Uri)])])]);
    static IppMessage ProtocolError(IppProtocolException exception) => new(exception.Version!.Value, exception.StatusCode, exception.RequestId!.Value,
    [
        new IppAttributeGroup(IppDelimiterTags.OperationAttributes,
        [
            new IppAttribute("attributes-charset", [IppValue.FromString(IppValueTags.Charset, "utf-8")]),
            new IppAttribute("attributes-natural-language", [IppValue.FromString(IppValueTags.NaturalLanguage, "en")])
        ])
    ]);
}
