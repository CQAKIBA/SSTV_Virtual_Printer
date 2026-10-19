using SstvVPrinter.Sstv;

namespace SstvVPrinter.App.Transmission;

public sealed record PreparedTransmission(
    RgbFrame Frame,
    TransmissionEncodeOptions Options,
    TimeSpan EstimatedDuration,
    DateTimeOffset HeaderTime);
