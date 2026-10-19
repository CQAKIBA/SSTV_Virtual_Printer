namespace SstvVPrinter.App.Transmission;

public sealed class TransmissionValidationException : Exception
{
    public TransmissionValidationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
