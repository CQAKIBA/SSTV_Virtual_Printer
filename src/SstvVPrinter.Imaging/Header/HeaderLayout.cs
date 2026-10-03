namespace SstvVPrinter.Imaging.Header;

internal sealed record HeaderLayout(
    string MetadataText,
    string CallSign,
    int HorizontalScale,
    int PatchWidth,
    int MetadataX,
    int CallSignX,
    int GlyphAdvance);
