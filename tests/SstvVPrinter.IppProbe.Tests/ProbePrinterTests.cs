using System.Buffers.Binary;
using SstvVPrinter.IppProbe.Ipp;

namespace SstvVPrinter.IppProbe.Tests;

public sealed class ProbePrinterTests
{
    private readonly ProbePrinter printer = new();

    [Fact]
    public void GetPrinterAttributes_returns_windows_required_capabilities()
    {
        IppMessage response = printer.CreateResponse(Request(IppOperations.GetPrinterAttributes, 77));

        Assert.Equal(77, response.RequestId);
        Assert.Equal(IppStatusCodes.SuccessfulOk, response.Code);
        AssertValues(response, "ipp-versions-supported", "2.0", "2.1");
        AssertValues(response, "document-format-supported", "image/pwg-raster");
        AssertValues(response, "operations-supported", 2, 4, 11);
        Assert.True(response.FindAttribute("color-supported")!.Values.Single().Data.Single() != 0);
        AssertValues(response, "sides-supported", "one-sided");
        AssertValues(response, "media-supported", "iso_a4_210x297mm");
        AssertValues(response, "charset-supported", "utf-8");
        AssertValues(response, "generated-natural-language-supported", "en");
        AssertValues(response, "compression-supported", "none");
        AssertValues(response, "printer-state", 3);
        AssertValues(response, "queued-job-count", 0);
        IppValue resolution = response.FindAttribute("printer-resolution-supported")!.Values.Single();
        Assert.Equal(300, BinaryPrimitives.ReadInt32BigEndian(resolution.Data.AsSpan(0, 4)));
        Assert.Equal(300, BinaryPrimitives.ReadInt32BigEndian(resolution.Data.AsSpan(4, 4)));
        Assert.Equal(3, resolution.Data[8]);
    }

    [Fact]
    public void GetPrinterAttributes_honors_requested_attributes()
    {
        IppMessage request = Request(IppOperations.GetPrinterAttributes, 1,
            new IppAttribute("requested-attributes", [
                IppValue.FromString(IppValueTags.Keyword, "printer-name"),
                IppValue.FromString(IppValueTags.Keyword, "printer-state")
            ]));

        IppMessage response = printer.CreateResponse(request);

        Assert.NotNull(response.FindAttribute("printer-name"));
        Assert.NotNull(response.FindAttribute("printer-state"));
        Assert.Null(response.FindAttribute("document-format-supported"));
    }

    [Fact]
    public void ValidateJob_accepts_pwg_raster()
    {
        Assert.Equal(IppStatusCodes.SuccessfulOk, printer.CreateResponse(Request(IppOperations.ValidateJob, 2)).Code);
    }

    [Fact]
    public void ValidateJob_rejects_unsupported_document_format()
    {
        IppMessage request = Request(IppOperations.ValidateJob, 3,
            new IppAttribute("document-format", [IppValue.FromString(IppValueTags.MimeMediaType, "application/pdf")]));

        Assert.Equal(IppStatusCodes.ClientErrorDocumentFormatNotSupported, printer.CreateResponse(request).Code);
    }

    [Fact]
    public void CreateResponse_rejects_unknown_operation()
    {
        Assert.Equal(IppStatusCodes.ServerErrorOperationNotSupported, printer.CreateResponse(Request(0x7777, 4)).Code);
    }

    [Fact]
    public void CreateResponse_rejects_wrong_printer_uri()
    {
        IppMessage request = Request(IppOperations.ValidateJob, 5,
            new IppAttribute("printer-uri", [IppValue.FromString(IppValueTags.Uri, "ipp://example.test/ipp/print")]));

        Assert.Equal(IppStatusCodes.ClientErrorNotFound, printer.CreateResponse(request).Code);
    }

    [Fact]
    public void CreateResponse_echoes_request_id_on_error()
    {
        Assert.Equal(9876, printer.CreateResponse(Request(0x7777, 9876)).RequestId);
    }

    private static IppMessage Request(ushort operation, int requestId, params IppAttribute[] overrides)
    {
        var attributes = new List<IppAttribute>
        {
            new("attributes-charset", [IppValue.FromString(IppValueTags.Charset, "utf-8")]),
            new("attributes-natural-language", [IppValue.FromString(IppValueTags.NaturalLanguage, "en")]),
            new("printer-uri", [IppValue.FromString(IppValueTags.Uri, ProbePrinter.PrinterUri)]),
            new("document-format", [IppValue.FromString(IppValueTags.MimeMediaType, "image/pwg-raster")])
        };
        foreach (IppAttribute replacement in overrides)
        {
            attributes.RemoveAll(item => item.Name == replacement.Name);
            attributes.Add(replacement);
        }
        return new IppMessage(new IppVersion(2, 0), operation, requestId,
            [new IppAttributeGroup(IppDelimiterTags.OperationAttributes, attributes)]);
    }

    private static void AssertValues(IppMessage message, string name, params string[] expected) =>
        Assert.Equal(expected, message.FindAttribute(name)!.Values.Select(value => value.AsString()));

    private static void AssertValues(IppMessage message, string name, params int[] expected) =>
        Assert.Equal(expected, message.FindAttribute(name)!.Values.Select(value => value.AsInt32()));
}
