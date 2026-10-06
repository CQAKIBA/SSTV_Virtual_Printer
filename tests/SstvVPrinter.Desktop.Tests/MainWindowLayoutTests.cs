using System.Xml.Linq;

namespace SstvVPrinter.Desktop.Tests;

public sealed class MainWindowLayoutTests
{
    [Fact]
    public void Main_window_keeps_preview_queue_and_approved_control_group_order()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SstvVPrinter.Desktop", "Views", "MainWindow.axaml"));
        var document = XDocument.Load(path);
        XNamespace automation = "https://github.com/avaloniaui";
        var names = document.Descendants()
            .Select(element => element.Attribute("AutomationProperties.Name")?.Value)
            .Where(value => value is not null)
            .ToArray();

        Assert.Contains("SSTV preview", names);
        Assert.Contains("Print job queue", names);
        Assert.Equal(
            ["Call sign", "Hardware", "Protocol", "Status", "Actions"],
            names.Where(value => value is "Call sign" or "Hardware" or "Protocol" or "Status" or "Actions"));
    }

    [Fact]
    public void Call_sign_commits_on_enter_or_focus_loss()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SstvVPrinter.Desktop", "Views", "MainWindow.axaml"));
        var document = XDocument.Load(path);
        var textBox = document.Descendants().Single(element =>
            element.Name.LocalName == "TextBox" && element.Attribute("Name")?.Value == "CallSignInput");

        Assert.Equal("OnCallSignKeyDown", textBox.Attribute("KeyDown")?.Value);
        Assert.Equal("OnCallSignLostFocus", textBox.Attribute("LostFocus")?.Value);
        Assert.Equal("Enter your call sign", textBox.Attribute("PlaceholderText")?.Value);
    }

    [Fact]
    public void Hardware_panel_contains_master_and_application_volume_bars()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SstvVPrinter.Desktop", "Views", "MainWindow.axaml"));
        var document = XDocument.Load(path);
        var automationNames = document.Descendants()
            .Select(element => element.Attribute("AutomationProperties.Name")?.Value)
            .Where(value => value is not null)
            .ToArray();

        Assert.Contains("Master volume", automationNames);
        Assert.Contains("Application volume", automationNames);
    }

    [Fact]
    public void Audio_warning_wraps_inside_the_actions_panel()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SstvVPrinter.Desktop", "Views", "MainWindow.axaml"));
        var document = XDocument.Load(path);
        var warning = document.Descendants().Single(element =>
            element.Name.LocalName == "TextBlock" && element.Attribute("Text")?.Value == "{Binding MuteWarning}");

        Assert.Equal("Wrap", warning.Attribute("TextWrapping")?.Value);
    }

    [Fact]
    public void Protocol_panel_contains_rotation_controls()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SstvVPrinter.Desktop", "Views", "MainWindow.axaml"));
        var document = XDocument.Load(path);

        var clockwise = document.Descendants().Single(element => element.Attribute("Name")?.Value == "RotateClockwiseButton");
        var counterclockwise = document.Descendants().Single(element => element.Attribute("Name")?.Value == "RotateCounterclockwiseButton");

        Assert.Equal("OnRotateClockwiseClick", clockwise.Attribute("Click")?.Value);
        Assert.Equal("OnRotateCounterclockwiseClick", counterclockwise.Attribute("Click")?.Value);
        Assert.Contains(document.Descendants(), element => element.Attribute("Text")?.Value == "{Binding RotationText}");
    }
}
