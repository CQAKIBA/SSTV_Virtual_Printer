using SstvVPrinter.Sstv.Radiofax;

namespace SstvVPrinter.Sstv;

public static class TransmissionModes
{
    public static void Validate(SstvMode mode)
    {
        if (RadiofaxModes.IsRadiofax(mode))
        {
            _ = RadiofaxModes.Get(mode);
            return;
        }

        _ = SstvModes.Get(mode);
    }

    public static string DisplayName(SstvMode mode) => IsRadiofax(mode)
        ? RadiofaxModes.Get(mode).DisplayName
        : SstvModes.Get(mode).DisplayName;

    public static bool IsRadiofax(SstvMode mode) => RadiofaxModes.IsRadiofax(mode);
}
