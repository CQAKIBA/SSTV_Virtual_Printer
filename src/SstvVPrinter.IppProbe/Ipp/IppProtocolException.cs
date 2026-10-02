namespace SstvVPrinter.IppProbe.Ipp;

public sealed class IppProtocolException(string message, ushort statusCode, Exception? inner = null) : Exception(message, inner) { public ushort StatusCode { get; } = statusCode; }
