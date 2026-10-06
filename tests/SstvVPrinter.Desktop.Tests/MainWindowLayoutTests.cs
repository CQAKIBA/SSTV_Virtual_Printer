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
    }
}
