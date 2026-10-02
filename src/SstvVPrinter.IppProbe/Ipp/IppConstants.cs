namespace SstvVPrinter.IppProbe.Ipp;

public static class IppOperations { public const ushort PrintJob = 2, ValidateJob = 4, GetPrinterAttributes = 11; }
public static class IppStatusCodes { public const ushort SuccessfulOk = 0, ClientErrorBadRequest = 0x400, ClientErrorNotFound = 0x406, ClientErrorDocumentFormatNotSupported = 0x40A, ServerErrorOperationNotSupported = 0x501; }
public static class IppDelimiterTags { public const byte OperationAttributes = 1, JobAttributes = 2, EndOfAttributes = 3, PrinterAttributes = 4, UnsupportedAttributes = 5; public static bool IsGroup(byte tag) => tag is 1 or 2 or 4 or 5; }
public static class IppValueTags { public const byte Integer = 0x21, Boolean = 0x22, Enum = 0x23, Resolution = 0x32, TextWithoutLanguage = 0x41, NameWithoutLanguage = 0x42, Keyword = 0x44, Uri = 0x45, Charset = 0x47, NaturalLanguage = 0x48, MimeMediaType = 0x49; }
