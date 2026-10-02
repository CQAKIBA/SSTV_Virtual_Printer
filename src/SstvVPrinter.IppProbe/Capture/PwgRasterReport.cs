namespace SstvVPrinter.IppProbe.Capture;

public sealed record PwgRasterReport(bool IsPwgRaster, int Width = 0, int Height = 0, int XResolution = 0, int YResolution = 0, int BitsPerColor = 0, int BitsPerPixel = 0, int BytesPerLine = 0, int ColorSpace = 0, int Orientation = 0, int PageWidthPoints = 0, int PageHeightPoints = 0)
{
    public bool IsLandscape => Orientation is 1 or 3;
}
