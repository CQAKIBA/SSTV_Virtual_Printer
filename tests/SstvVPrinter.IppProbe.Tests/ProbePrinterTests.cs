using System.Buffers.Binary;
using SstvVPrinter.IppProbe.Ipp;

namespace SstvVPrinter.IppProbe.Tests;

public sealed class ProbePrinterTests
{
    private readonly ProbePrinter printer = new();

    [Fact]
    public void AllocateJob_continues_after_initial_job_id()
    {
        var resumed = new ProbePrinter(12);

        Assert.Equal(13, resumed.AllocateJob("operator", null).Id);
    }

    [Fact]
    public void Configured_printer_uri_is_validated_advertised_and_used_for_jobs()
    {
        const string uri = "ipp://localhost:9863/ipp/print";
        var configured = new ProbePrinter(uri);

        IppMessage response = configured.CreateResponse(RequestForUri(IppOperations.GetPrinterAttributes, 13, uri));
        ProbeJob job = configured.AllocateJob("operator", null);

        AssertValues(response, "printer-uri-supported", uri);
        AssertValues(response, "printer-more-info", "http://localhost:9863/");
        Assert.StartsWith("ipp://localhost:9863/jobs/", job.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void GetPrinterAttributes_returns_windows_required_capabilities()
    {
        IppMessage response = printer.CreateResponse(Request(IppOperations.GetPrinterAttributes, 77));

        Assert.Equal(77, response.RequestId);
        Assert.Equal(IppStatusCodes.SuccessfulOk, response.Code);
        AssertValues(response, "ipp-versions-supported", "2.0", "2.1");
        AssertValues(response, "document-format-supported", "image/pwg-raster");
        AssertValues(response, "operations-supported", 2, 4, 8, 9, 11);
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
    public void GetPrinterAttributes_returns_required_uri_companions()
    {
        IppMessage request = Request(IppOperations.GetPrinterAttributes, 6,
            new IppAttribute("requested-attributes", [
                IppValue.FromString(IppValueTags.Keyword, "printer-uri-supported"),
                IppValue.FromString(IppValueTags.Keyword, "uri-authentication-supported"),
                IppValue.FromString(IppValueTags.Keyword, "uri-security-supported")
            ]));

        IppMessage response = printer.CreateResponse(request);

        AssertValues(response, "printer-uri-supported", printer.PrinterUri);
        AssertValues(response, "uri-authentication-supported", "none");
        AssertValues(response, "uri-security-supported", "none");
    }

    [Fact]
    public void GetPrinterAttributes_returns_required_default_document_format()
    {
        IppMessage request = Request(IppOperations.GetPrinterAttributes, 7,
            new IppAttribute("requested-attributes", [
                IppValue.FromString(IppValueTags.Keyword, "document-format-default")
            ]));

        IppMessage response = printer.CreateResponse(request);

        AssertValues(response, "document-format-default", "image/pwg-raster");
    }

    [Fact]
    public void GetPrinterAttributes_returns_ipp_everywhere_device_id()
    {
        IppMessage request = Request(IppOperations.GetPrinterAttributes, 8,
            new IppAttribute("requested-attributes", [
                IppValue.FromString(IppValueTags.Keyword, "printer-device-id")
            ]));

        IppMessage response = printer.CreateResponse(request);

        AssertValues(response, "printer-device-id", "MFG:SSTV;MDL:Virtual Printer Probe;CMD:PWGRaster;");
    }

    [Fact]
    public void GetPrinterAttributes_returns_complete_windows_identity_surface()
    {
        string[] names =
        [
            "document-format-preferred", "media-type-supported", "mopria-certified",
            "printer-dns-sd-name", "printer-firmware-name", "printer-firmware-string-version",
            "printer-info", "printer-location", "printer-make-and-model", "printer-more-info",
            "printer-uuid"
        ];
        IppMessage request = Request(IppOperations.GetPrinterAttributes, 9,
            new IppAttribute("requested-attributes",
                names.Select(name => IppValue.FromString(IppValueTags.Keyword, name)).ToArray()));

        IppMessage response = printer.CreateResponse(request);

        foreach (string name in names) Assert.NotNull(response.FindAttribute(name));
        AssertValues(response, "document-format-preferred", "image/pwg-raster");
        AssertValues(response, "media-type-supported", "stationery");
        AssertValues(response, "mopria-certified", "1.3");
        AssertValues(response, "printer-uuid", "urn:uuid:24e4c1b2-6ae7-4e6f-bb95-92f1da1a865d");
    }

    [Fact]
    public void GetPrinterAttributes_returns_coherent_pwg_raster_defaults()
    {
        IppMessage response = printer.CreateResponse(Request(IppOperations.GetPrinterAttributes, 10));

        Assert.True(response.FindAttribute("printer-is-accepting-jobs")!.Values.Single().Data.Single() != 0);
        AssertValues(response, "media-default", "iso_a4_210x297mm");
        AssertValues(response, "sides-default", "one-sided");
        AssertValues(response, "print-color-mode-default", "color");
        AssertValues(response, "print-color-mode-supported", "color");
        AssertValues(response, "pwg-raster-document-type-supported", "srgb_8");
        AssertValues(response, "print-scaling-default", "auto");
        AssertValues(response, "print-scaling-supported", "auto", "auto-fit", "fit");
        AssertValues(response, "copies-default", 1);
        AssertValues(response, "orientation-requested-default", 3);
        AssertValues(response, "orientation-requested-supported", 3, 4, 5, 6);
        AssertValues(response, "print-quality-default", 4);
        AssertValues(response, "print-quality-supported", 3, 4, 5);

        IppValue resolution = response.FindAttribute("printer-resolution-default")!.Values.Single();
        Assert.Equal(response.FindAttribute("printer-resolution-supported")!.Values.Single(), resolution);
        Assert.Equal(response.FindAttribute("pwg-raster-document-resolution-supported")!.Values.Single(), resolution);
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

    [Fact]
    public void Completed_job_can_be_queried_and_cancelled_idempotently()
    {
        ProbeJob job = printer.AllocateJob("operator", "document");
        IppAttribute jobId = new("job-id", [IppValue.FromInt32(IppValueTags.Integer, job.Id)]);

        IppMessage queried = printer.CreateResponse(Request(IppOperations.GetJobAttributes, 11, jobId));
        IppMessage cancelled = printer.CreateResponse(Request(IppOperations.CancelJob, 12, jobId));

        Assert.Equal(IppStatusCodes.SuccessfulOk, queried.Code);
        AssertValues(queried, "job-id", job.Id);
        AssertValues(queried, "job-state", 9);
        AssertValues(queried, "job-state-reasons", "job-completed-successfully");
        Assert.Equal(IppStatusCodes.SuccessfulOk, cancelled.Code);
    }

    [Fact]
    public void Job_operations_reject_malformed_job_id_as_bad_request()
    {
        IppAttribute malformed = new("job-id", [IppValue.FromString(IppValueTags.NameWithoutLanguage, "x")]);

        IppMessage response = printer.CreateResponse(Request(IppOperations.GetJobAttributes, 14, malformed));

        Assert.Equal(IppStatusCodes.ClientErrorBadRequest, response.Code);
    }

    private static IppMessage Request(ushort operation, int requestId, params IppAttribute[] overrides) =>
        RequestForUri(operation, requestId, ProbePrinter.DefaultPrinterUri, overrides);

    private static IppMessage RequestForUri(ushort operation, int requestId, string printerUri, params IppAttribute[] overrides)
    {
        var attributes = new List<IppAttribute>
        {
            new("attributes-charset", [IppValue.FromString(IppValueTags.Charset, "utf-8")]),
            new("attributes-natural-language", [IppValue.FromString(IppValueTags.NaturalLanguage, "en")]),
            new("printer-uri", [IppValue.FromString(IppValueTags.Uri, printerUri)]),
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
