using SstvVPrinter.Desktop.ViewModels;

namespace SstvVPrinter.Desktop.Hosting;

public static class PreviewRegenerationPolicy
{
    public static bool RequiresRegeneration(string? propertyName) => propertyName is
        nameof(MainWindowViewModel.SelectedMode)
        or nameof(MainWindowViewModel.IsDoubleHorizontal)
        or nameof(MainWindowViewModel.RotationDegrees);
}
