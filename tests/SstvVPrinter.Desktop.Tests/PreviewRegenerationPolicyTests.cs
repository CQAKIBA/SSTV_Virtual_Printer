using SstvVPrinter.Desktop.Hosting;
using SstvVPrinter.Desktop.ViewModels;

namespace SstvVPrinter.Desktop.Tests;

public sealed class PreviewRegenerationPolicyTests
{
    [Theory]
    [InlineData(nameof(MainWindowViewModel.CallSign))]
    [InlineData(nameof(MainWindowViewModel.SelectedMode))]
    [InlineData(nameof(MainWindowViewModel.IsDoubleHorizontal))]
    public void Image_affecting_preferences_require_regeneration(string propertyName)
    {
        Assert.True(PreviewRegenerationPolicy.RequiresRegeneration(propertyName));
    }

    [Fact]
    public void Audio_endpoint_refresh_does_not_require_regeneration()
    {
        Assert.False(PreviewRegenerationPolicy.RequiresRegeneration(
            nameof(MainWindowViewModel.SelectedAudioEndpoint)));
    }
}
