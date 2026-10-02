namespace SstvVPrinter.Sstv.Planning;

internal static class SstvScanPlannerFactory
{
    public static ISstvScanPlanner Create(SstvMode mode) => mode switch
    {
        SstvMode.Martin1 or SstvMode.Scottie1 or SstvMode.Scottie2 => new SequentialRgbScanPlanner(mode),
        _ => throw new NotSupportedException($"Mode {mode} is not implemented."),
    };
}
