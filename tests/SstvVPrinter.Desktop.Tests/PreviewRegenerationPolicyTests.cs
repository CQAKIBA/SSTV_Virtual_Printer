using SstvVPrinter.Desktop.Hosting;
using SstvVPrinter.Desktop.ViewModels;

namespace SstvVPrinter.Desktop.Tests;

public sealed class PreviewRegenerationPolicyTests
{
    [Theory]
    [InlineData(nameof(MainWindowViewModel.SelectedMode))]
    [InlineData(nameof(MainWindowViewModel.IsDoubleHorizontal))]
    [InlineData(nameof(MainWindowViewModel.RotationDegrees))]
    public void Image_affecting_preferences_require_regeneration(string propertyName)
    {
        Assert.True(PreviewRegenerationPolicy.RequiresRegeneration(propertyName));
    }

    [Fact]
    public void Call_sign_edit_waits_for_an_explicit_commit()
    {
        Assert.False(PreviewRegenerationPolicy.RequiresRegeneration(
            nameof(MainWindowViewModel.CallSign)));
    }

    [Fact]
    public void Audio_endpoint_refresh_does_not_require_regeneration()
    {
        Assert.False(PreviewRegenerationPolicy.RequiresRegeneration(
            nameof(MainWindowViewModel.SelectedAudioEndpoint)));
    }
}
