using System.Text.Json;
using SstvVPrinter.App.Settings;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.App.Tests.Settings;

public sealed class UserPreferencesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"sstv-settings-{Guid.NewGuid():N}");

    [Fact]
    public async Task Missing_file_returns_safe_defaults()
    {
        var store = new JsonUserPreferencesStore(_directory);

        var result = await store.LoadAsync();

        Assert.Equal(string.Empty, result.CallSign);
        Assert.Equal(SstvMode.Scottie2, result.Mode);
        Assert.Equal(1, result.HorizontalSampleMultiplier);
        Assert.Null(result.AudioEndpointId);
        Assert.True(result.StartAtSignIn);
    }

    [Fact]
    public async Task Corrupt_json_returns_safe_defaults()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "SstvVPrinter"));
        await File.WriteAllTextAsync(Path.Combine(_directory, "SstvVPrinter", "settings.json"), "{ broken");
        var store = new JsonUserPreferencesStore(_directory);

        var result = await store.LoadAsync();

        Assert.Equal(UserPreferences.Default, result);
    }

    [Fact]
    public async Task Save_is_atomic_and_round_trips_only_preferences()
    {
        var store = new JsonUserPreferencesStore(_directory);
        var expected = new UserPreferences("JA1UMW", SstvMode.Robot36, 2, "endpoint-7", false);

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(expected, actual);
        var settingsDirectory = Path.Combine(_directory, "SstvVPrinter");
        Assert.Empty(Directory.EnumerateFiles(settingsDirectory, "*.tmp"));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(settingsDirectory, "settings.json")));
        Assert.Equal(5, document.RootElement.EnumerateObject().Count());
        Assert.False(document.RootElement.TryGetProperty("Page", out _));
        Assert.False(document.RootElement.TryGetProperty("Jobs", out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Preferences_reject_invalid_multiplier(int multiplier) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UserPreferences("JA1UMW", SstvMode.Scottie2, multiplier, null, true));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
