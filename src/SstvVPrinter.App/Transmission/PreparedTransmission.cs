using SstvVPrinter.Sstv;

namespace SstvVPrinter.App.Transmission;

public sealed record PreparedTransmission(
    RgbFrame Frame,
    SstvEncodeOptions Options,
    TimeSpan EstimatedDuration,
    DateTimeOffset HeaderTime);
