using System.Buffers.Binary;

namespace SstvVPrinter.IppProbe.Ipp;

public sealed record ProbeJob(int Id, string Uri, string UserName, string? JobName);

public sealed class ProbePrinter
{
    public const string PrinterUri = "ipp://localhost:8631/ipp/print";
    public const string DisplayName = "SSTV Virtual Printer Probe";
    private int nextJobId;

    public ProbeJob AllocateJob(string userName, string? jobName)
    {
        int id = Interlocked.Increment(ref nextJobId);
        return new ProbeJob(id, $"ipp://localhost:8631/jobs/{id}", userName, jobName);
    }

    public IppMessage CreateResponse(IppMessage request)
    {
        ushort validationStatus = ValidateRequiredAttributes(request);
        if (validationStatus != IppStatusCodes.SuccessfulOk) return Response(request, validationStatus);

        return request.Code switch
        {
            IppOperations.GetPrinterAttributes => GetPrinterAttributes(request),
            IppOperations.ValidateJob or IppOperations.PrintJob => ValidateJob(request),
            _ => Response(request, IppStatusCodes.ServerErrorOperationNotSupported)
        };
    }

    private static ushort ValidateRequiredAttributes(IppMessage request)
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

    private static IppMessage GetPrinterAttributes(IppMessage request)
    {
        IppAttribute[] all =
        [
            Strings("printer-uri-supported", IppValueTags.Uri, PrinterUri),
            Strings("printer-name", IppValueTags.NameWithoutLanguage, DisplayName),
            Strings("ipp-versions-supported", IppValueTags.Keyword, "2.0", "2.1"),
            Integers("operations-supported", IppValueTags.Enum, IppOperations.PrintJob, IppOperations.ValidateJob, IppOperations.GetPrinterAttributes),
            Strings("document-format-supported", IppValueTags.MimeMediaType, "image/pwg-raster"),
            new("color-supported", [IppValue.FromBoolean(true)]),
            new("printer-resolution-supported", [Resolution(300, 300)]),
            Strings("sides-supported", IppValueTags.Keyword, "one-sided"),
            Strings("media-supported", IppValueTags.Keyword, "iso_a4_210x297mm"),
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
}
