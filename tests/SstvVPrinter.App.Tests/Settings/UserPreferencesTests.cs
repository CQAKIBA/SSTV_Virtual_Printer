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
        Assert.Equal(0, result.RotationDegrees);
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
    public async Task Settings_saved_before_rotation_support_load_as_zero_degrees()
    {
        var settingsDirectory = Path.Combine(_directory, "SstvVPrinter");
        Directory.CreateDirectory(settingsDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(settingsDirectory, "settings.json"),
            """{"CallSign":"JA1UMW","Mode":2,"HorizontalSampleMultiplier":1,"AudioEndpointId":null,"StartAtSignIn":true}""");

        var result = await new JsonUserPreferencesStore(_directory).LoadAsync();

        Assert.Equal("JA1UMW", result.CallSign);
        Assert.Equal(0, result.RotationDegrees);
    }

    [Theory]
    [InlineData(0, SstvMode.Martin1, 1)]
    [InlineData(1, SstvMode.Scottie1, 2)]
    [InlineData(2, SstvMode.Scottie2, 1)]
    [InlineData(3, SstvMode.Robot36, 2)]
    public async Task Legacy_numeric_sstv_modes_keep_their_original_meaning(
        int storedMode,
        SstvMode expectedMode,
        int multiplier)
    {
        var settingsDirectory = Path.Combine(_directory, "SstvVPrinter");
        Directory.CreateDirectory(settingsDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(settingsDirectory, "settings.json"),
            $$"""{"CallSign":"TEST","Mode":{{storedMode}},"HorizontalSampleMultiplier":{{multiplier}},"AudioEndpointId":null,"StartAtSignIn":false}""");

        var result = await new JsonUserPreferencesStore(_directory).LoadAsync();

        Assert.Equal(expectedMode, result.Mode);
        Assert.Equal(multiplier, result.HorizontalSampleMultiplier);
    }

    [Fact]
    public void Preferences_accept_radiofax_mode_with_effective_multiplier_one()
    {
        var preferences = new UserPreferences(
            "TEST",
            SstvMode.Radiofax120Ioc288,
            1,
            null,
            true);

        Assert.Equal(SstvMode.Radiofax120Ioc288, preferences.Mode);
    }

    [Fact]
    public async Task Save_is_atomic_and_round_trips_only_preferences()
    {
        var store = new JsonUserPreferencesStore(_directory);
        var expected = new UserPreferences("JA1UMW", SstvMode.Robot36, 2, "endpoint-7", false, 270);

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(expected, actual);
        var settingsDirectory = Path.Combine(_directory, "SstvVPrinter");
        Assert.Empty(Directory.EnumerateFiles(settingsDirectory, "*.tmp"));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(settingsDirectory, "settings.json")));
        Assert.Equal(6, document.RootElement.EnumerateObject().Count());
        Assert.False(document.RootElement.TryGetProperty("Page", out _));
        Assert.False(document.RootElement.TryGetProperty("Jobs", out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Preferences_reject_invalid_multiplier(int multiplier) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UserPreferences("JA1UMW", SstvMode.Scottie2, multiplier, null, true));

    [Theory]
    [InlineData(-90)]
    [InlineData(360)]
    public void Preferences_reject_non_normalized_rotation(int rotationDegrees) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UserPreferences("JA1UMW", SstvMode.Scottie2, 1, null, true, rotationDegrees));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
