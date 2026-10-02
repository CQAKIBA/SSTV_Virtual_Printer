using System.Buffers.Binary;

namespace SstvVPrinter.IppProbe.Ipp;

public sealed record ProbeJob(int Id, string Uri, string UserName, string? JobName);

public sealed class ProbePrinter
{
    public const string DefaultPrinterUri = "ipp://localhost:8631/ipp/print";
    public const string DisplayName = "SSTV Virtual Printer Probe";
    public string PrinterUri { get; }
    private int nextJobId;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, ProbeJob> jobs = new();

    public ProbePrinter(int lastJobId = 0) : this(DefaultPrinterUri, lastJobId) { }

    public ProbePrinter(string printerUri, int lastJobId = 0)
    {
        if (lastJobId is < 0 or int.MaxValue) throw new ArgumentOutOfRangeException(nameof(lastJobId));
        if (!Uri.TryCreate(printerUri, UriKind.Absolute, out Uri? parsed) || parsed.Scheme != "ipp") throw new ArgumentException("A valid absolute IPP URI is required.", nameof(printerUri));
        PrinterUri = printerUri;
        nextJobId = lastJobId;
    }

    public ProbeJob AllocateJob(string userName, string? jobName)
    {
        int id = Interlocked.Increment(ref nextJobId);
        var endpoint = new Uri(PrinterUri);
        var job = new ProbeJob(id, $"ipp://{endpoint.Authority}/jobs/{id}", userName, jobName);
        jobs[id] = job;
        return job;
    }

    public IppMessage CreateResponse(IppMessage request)
    {
        ushort validationStatus = ValidateRequiredAttributes(request);
        if (validationStatus != IppStatusCodes.SuccessfulOk) return Response(request, validationStatus);

        return request.Code switch
        {
            IppOperations.GetPrinterAttributes => GetPrinterAttributes(request),
            IppOperations.ValidateJob or IppOperations.PrintJob => ValidateJob(request),
            IppOperations.GetJobAttributes => GetJobAttributes(request),
            IppOperations.CancelJob => CancelJob(request),
            _ => Response(request, IppStatusCodes.ServerErrorOperationNotSupported)
        };
    }

    private IppMessage GetJobAttributes(IppMessage request)
    {
        ushort status = FindJob(request, out ProbeJob job);
        if (status != IppStatusCodes.SuccessfulOk) return Response(request, status);
        return Response(request, IppStatusCodes.SuccessfulOk,
            new IppAttributeGroup(IppDelimiterTags.JobAttributes,
            [
                Integers("job-id", IppValueTags.Integer, job.Id),
                Strings("job-uri", IppValueTags.Uri, job.Uri),
                Integers("job-state", IppValueTags.Enum, 9),
                Strings("job-state-reasons", IppValueTags.Keyword, "job-completed-successfully")
            ]));
    }

    private IppMessage CancelJob(IppMessage request) => Response(request, FindJob(request, out _));

    private ushort FindJob(IppMessage request, out ProbeJob job)
    {
        job = null!;
        IppAttribute? id = request.FindAttribute("job-id");
        if (id is null || id.Values.Count != 1 || id.Values[0].Tag != IppValueTags.Integer || id.Values[0].Data.Length != 4)
            return IppStatusCodes.ClientErrorBadRequest;
        if (!jobs.TryGetValue(id.Values[0].AsInt32(), out ProbeJob? found)) return IppStatusCodes.ClientErrorNotFound;
        job = found;
        return IppStatusCodes.SuccessfulOk;
    }

    private ushort ValidateRequiredAttributes(IppMessage request)
    {
        if (request.FindAttribute("attributes-charset") is null || request.FindAttribute("attributes-natural-language") is null)
            return IppStatusCodes.ClientErrorBadRequest;
        IppAttribute? uri = request.FindAttribute("printer-uri");
        if (uri is null) return IppStatusCodes.ClientErrorBadRequest;
        return uri.Values[0].AsString() == PrinterUri ? IppStatusCodes.SuccessfulOk : IppStatusCodes.ClientErrorNotFound;
    }

    private static IppMessage ValidateJob(IppMessage request)
    {
        string format = request.FindAttribute("document-format")?.Values[0].AsString() ?? "application/octet-stream";
        return Response(request, format == "image/pwg-raster"
            ? IppStatusCodes.SuccessfulOk
            : IppStatusCodes.ClientErrorDocumentFormatNotSupported);
    }

    private IppMessage GetPrinterAttributes(IppMessage request)
    {
        IppAttribute[] all =
        [
            Strings("printer-uri-supported", IppValueTags.Uri, PrinterUri),
            Strings("uri-authentication-supported", IppValueTags.Keyword, "none"),
            Strings("uri-security-supported", IppValueTags.Keyword, "none"),
            Strings("printer-name", IppValueTags.NameWithoutLanguage, DisplayName),
            Strings("printer-dns-sd-name", IppValueTags.NameWithoutLanguage, DisplayName),
            Strings("printer-info", IppValueTags.TextWithoutLanguage, DisplayName),
            Strings("printer-location", IppValueTags.TextWithoutLanguage, "Local computer"),
            Strings("printer-make-and-model", IppValueTags.TextWithoutLanguage, "SSTV Virtual Printer Probe"),
            Strings("printer-more-info", IppValueTags.Uri, MoreInfoUri()),
            Strings("printer-uuid", IppValueTags.Uri, "urn:uuid:24e4c1b2-6ae7-4e6f-bb95-92f1da1a865d"),
            Strings("printer-device-id", IppValueTags.TextWithoutLanguage, "MFG:SSTV;MDL:Virtual Printer Probe;CMD:PWGRaster;"),
            Strings("printer-firmware-name", IppValueTags.NameWithoutLanguage, "SSTV IPP Probe"),
            Strings("printer-firmware-string-version", IppValueTags.TextWithoutLanguage, "0.1.0"),
            Strings("mopria-certified", IppValueTags.TextWithoutLanguage, "1.3"),
            Strings("ipp-versions-supported", IppValueTags.Keyword, "2.0", "2.1"),
            Integers("operations-supported", IppValueTags.Enum, IppOperations.PrintJob, IppOperations.ValidateJob, IppOperations.CancelJob, IppOperations.GetJobAttributes, IppOperations.GetPrinterAttributes),
            Strings("document-format-default", IppValueTags.MimeMediaType, "image/pwg-raster"),
            Strings("document-format-preferred", IppValueTags.MimeMediaType, "image/pwg-raster"),
            Strings("document-format-supported", IppValueTags.MimeMediaType, "image/pwg-raster"),
            new("color-supported", [IppValue.FromBoolean(true)]),
            new("printer-is-accepting-jobs", [IppValue.FromBoolean(true)]),
            new("printer-resolution-default", [Resolution(300, 300)]),
            new("printer-resolution-supported", [Resolution(300, 300)]),
            new("pwg-raster-document-resolution-supported", [Resolution(300, 300)]),
            Strings("pwg-raster-document-type-supported", IppValueTags.Keyword, "srgb_8"),
            Strings("pwg-raster-document-sheet-back", IppValueTags.Keyword, "normal"),
            Strings("sides-default", IppValueTags.Keyword, "one-sided"),
            Strings("sides-supported", IppValueTags.Keyword, "one-sided"),
            Strings("media-default", IppValueTags.Keyword, "iso_a4_210x297mm"),
            Strings("media-supported", IppValueTags.Keyword, "iso_a4_210x297mm"),
            Strings("media-type-supported", IppValueTags.Keyword, "stationery"),
            Integers("media-left-margin-supported", IppValueTags.Integer, 0),
            Integers("media-right-margin-supported", IppValueTags.Integer, 0),
            Integers("media-top-margin-supported", IppValueTags.Integer, 0),
            Integers("media-bottom-margin-supported", IppValueTags.Integer, 0),
            Integers("copies-default", IppValueTags.Integer, 1),
            new("copies-supported", [Range(1, 1)]),
            Integers("orientation-requested-default", IppValueTags.Enum, 3),
            Integers("orientation-requested-supported", IppValueTags.Enum, 3, 4, 5, 6),
            Integers("print-quality-default", IppValueTags.Enum, 4),
            Integers("print-quality-supported", IppValueTags.Enum, 3, 4, 5),
            Strings("print-color-mode-default", IppValueTags.Keyword, "color"),
            Strings("print-color-mode-supported", IppValueTags.Keyword, "color"),
            Strings("print-scaling-default", IppValueTags.Keyword, "auto"),
            Strings("print-scaling-supported", IppValueTags.Keyword, "auto", "auto-fit", "fit"),
            Strings("multiple-document-handling-default", IppValueTags.Keyword, "separate-documents-uncollated-copies"),
            Strings("multiple-document-handling-supported", IppValueTags.Keyword, "separate-documents-uncollated-copies"),
            Integers("number-up-default", IppValueTags.Integer, 1),
            new("number-up-supported", [Range(1, 1)]),
            Strings("output-bin-default", IppValueTags.Keyword, "face-down"),
            Strings("output-bin-supported", IppValueTags.Keyword, "face-down"),
            Strings("printer-mandatory-job-attributes", IppValueTags.Keyword, "attributes-charset", "attributes-natural-language", "printer-uri"),
            Strings("charset-supported", IppValueTags.Charset, "utf-8"),
            Strings("natural-language-configured", IppValueTags.NaturalLanguage, "en"),
            Strings("generated-natural-language-supported", IppValueTags.NaturalLanguage, "en"),
            Strings("compression-supported", IppValueTags.Keyword, "none"),
            Integers("printer-state", IppValueTags.Enum, 3),
            Strings("printer-state-reasons", IppValueTags.Keyword, "none"),
            Integers("queued-job-count", IppValueTags.Integer, 0)
        ];
        HashSet<string>? requested = request.FindAttribute("requested-attributes") is { } attribute
            ? attribute.Values.Select(value => value.AsString()).ToHashSet(StringComparer.Ordinal)
            : null;
        IppAttribute[] selected = requested is null ? all : all.Where(item => requested.Contains(item.Name)).ToArray();
        return Response(request, IppStatusCodes.SuccessfulOk,
            new IppAttributeGroup(IppDelimiterTags.PrinterAttributes, selected));
    }

    private static IppMessage Response(IppMessage request, ushort status, params IppAttributeGroup[] groups)
    {
        var operation = new IppAttributeGroup(IppDelimiterTags.OperationAttributes,
        [
            Strings("attributes-charset", IppValueTags.Charset, "utf-8"),
            Strings("attributes-natural-language", IppValueTags.NaturalLanguage, "en")
        ]);
        return new IppMessage(request.Version, status, request.RequestId, [operation, .. groups]);
    }

    private static IppAttribute Strings(string name, byte tag, params string[] values) =>
        new(name, values.Select(value => IppValue.FromString(tag, value)).ToArray());

    private static IppAttribute Integers(string name, byte tag, params int[] values) =>
        new(name, values.Select(value => IppValue.FromInt32(tag, value)).ToArray());

    private static IppValue Resolution(int x, int y)
    {
        byte[] data = new byte[9];
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(0, 4), x);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4, 4), y);
        data[8] = 3;
        return new IppValue(IppValueTags.Resolution, data);
    }

    private static IppValue Range(int lower, int upper)
    {
        byte[] data = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(0, 4), lower);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4, 4), upper);
        return new IppValue(IppValueTags.RangeOfInteger, data);
    }

    private string MoreInfoUri()
    {
        var builder = new UriBuilder(PrinterUri) { Scheme = Uri.UriSchemeHttp, Path = "/" };
        return builder.Uri.AbsoluteUri;
    }
}
