namespace SstvVPrinter.Imaging.Model;

public sealed record PwgPageMetadata(
    int PhysicalWidth,
    int PhysicalHeight,
    int LogicalWidth,
    int LogicalHeight,
    int DpiX,
    int DpiY,
    int Orientation,
    int PageWidthPoints,
    int PageHeightPoints);
