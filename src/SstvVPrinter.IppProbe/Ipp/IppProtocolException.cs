namespace SstvVPrinter.IppProbe.Ipp;

public sealed class IppProtocolException(string message, ushort statusCode, Exception? inner = null, IppVersion? version = null, int? requestId = null) : Exception(message, inner)
{
    public ushort StatusCode { get; } = statusCode;
    public IppVersion? Version { get; } = version;
    public int? RequestId { get; } = requestId;
}
