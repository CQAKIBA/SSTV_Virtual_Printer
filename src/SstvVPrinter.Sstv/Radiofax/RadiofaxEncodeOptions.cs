namespace SstvVPrinter.Sstv.Radiofax;

public sealed record RadiofaxEncodeOptions
{
    public RadiofaxEncodeOptions(SstvMode Mode, float Amplitude = 0.8f)
    {
        _ = RadiofaxModes.Get(Mode);
        if (!float.IsFinite(Amplitude) || Amplitude <= 0 || Amplitude > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Amplitude));
        }

        this.Mode = Mode;
        this.Amplitude = Amplitude;
    }

    public SstvMode Mode { get; }

    public float Amplitude { get; }

    public void Validate(RgbFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var definition = RadiofaxModes.Get(Mode);
        var maximumHeight = checked(definition.HeaderHeight + definition.MaximumBodyLines);
        if (frame.Width != definition.ActiveWidth ||
            frame.Height < definition.HeaderHeight ||
            frame.Height > maximumHeight)
        {
            throw new ArgumentException(
                $"Mode {definition.DisplayName} requires width {definition.ActiveWidth} and height " +
                $"{definition.HeaderHeight} through {maximumHeight}, but received {frame.Width}x{frame.Height}.",
                nameof(frame));
        }
    }
}
