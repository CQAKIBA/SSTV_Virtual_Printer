namespace SstvVPrinter.Imaging.Pwg;

public sealed class PwgRasterException : IOException
{
    public PwgRasterException(string message) : base(message)
    {
    }

    public PwgRasterException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
