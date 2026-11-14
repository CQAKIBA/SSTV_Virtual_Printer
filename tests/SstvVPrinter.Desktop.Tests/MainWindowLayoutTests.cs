using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SstvVPrinter.Desktop.Views;

namespace SstvVPrinter.Desktop.Tests;

public sealed class MainWindowLayoutTests
{
    [AvaloniaTheory]
    [InlineData("PttConnectionButton")]
    [InlineData("PttButton")]
    public void Ptt_red_state_remains_rendered_when_hovered_and_disabled(string buttonName)
    {
        var window = new MainWindow { AllowExplicitClose = true };
        window.Show();
        try
        {
            var button = window.FindControl<Button>(buttonName)!;
            button.Background = Brush.Parse("#FF3D21");
            var presenter = button.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(control => control.Name == "PART_ContentPresenter");
            var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center);
            Assert.True(button.IsPointerOver);
            Assert.Equal(Color.Parse("#FF3D21"), Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color);

            button.IsEnabled = false;
            Assert.False(button.IsEffectivelyEnabled);
            Assert.Equal(Color.Parse("#FF3D21"), Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color);
            window.MouseMove(default);
            Assert.Equal(Color.Parse("#FF3D21"), Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Failed_ptt_press_releases_once_and_clears_input_ownership(bool pointerInput, bool canceled)
    {
        var window = new MainWindow();
        var button = window.FindControl<Button>("PttButton")!;
        var pointer = new Pointer(1, PointerType.Mouse, true);
        var presses = 0;
        var releases = 0;
        var unhandled = new List<Exception>();
        void OnUnhandled(object? sender, DispatcherUnhandledExceptionEventArgs args)
        {
            unhandled.Add(args.Exception);
            args.Handled = true;
        }
        Dispatcher.UIThread.UnhandledException += OnUnhandled;
        window.ManualPttPressed = () =>
        {
            presses++;
            return canceled ? Task.FromCanceled(new CancellationToken(true)) : Task.FromException(new IOException("PTT failed"));
        };
        window.ManualPttReleased = () => { releases++; return Task.CompletedTask; };
        void Press()
        {
            if (pointerInput)
                button.RaiseEvent(new PointerPressedEventArgs(button, pointer, button, default, 0,
                    new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1));
            else
                button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        }
        try
        {
            Press();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, releases);
            Assert.Null(pointer.Captured);
            button.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
            Assert.Equal(1, releases);
            Press();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, presses);
            Assert.Equal(2, releases);
            Assert.Null(pointer.Captured);
            Assert.Empty(unhandled);
        }
        finally { Dispatcher.UIThread.UnhandledException -= OnUnhandled; }
    }

    [AvaloniaFact]
    public void Failure_from_a_released_press_does_not_release_a_newer_hold()
    {
        var window = new MainWindow();
        var button = window.FindControl<Button>("PttButton")!;
        var firstPress = new TaskCompletionSource();
        var presses = 0;
        var releases = 0;
        window.ManualPttPressed = () => ++presses == 1 ? firstPress.Task : Task.CompletedTask;
        window.ManualPttReleased = () => { releases++; return Task.CompletedTask; };

        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        firstPress.SetException(new IOException("Previous press failed late"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, releases);
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        Assert.Equal(2, releases);
    }

    [Fact]
    public void Hardware_exposes_ptt_controls_and_locks_only_editable_subgroups()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SstvVPrinter.Desktop", "Views", "MainWindow.axaml"));
        var hardware = XDocument.Load(path).Descendants().Single(element => element.Attribute("AutomationProperties.Name")?.Value == "Hardware");
        Assert.Null(hardware.Attribute("IsEnabled"));
        Assert.Contains(hardware.Descendants(), element => element.Attribute("Text")?.Value == "PTT PORT");
        var port = hardware.Descendants().Single(element => element.Attribute("Name")?.Value == "PttPortComboBox");
        Assert.Equal("OnPttPortDropDownOpened", port.Attribute("DropDownOpened")?.Value);
        Assert.Null(port.Attribute("SelectionChanged"));
        Assert.Null(port.Attribute("KeyDown"));
        Assert.Equal("{Binding CanEditPttSettings}", port.Attribute("IsEnabled")?.Value);
        var audio = hardware.Descendants().Single(element => element.Attribute("ItemsSource")?.Value == "{Binding AudioEndpoints}");
        Assert.Equal("{Binding !IsSettingsLocked}", audio.Attribute("IsEnabled")?.Value);
        var method = hardware.Descendants().Single(element => element.Attribute("ItemsSource")?.Value == "{Binding PttMethods}");
        Assert.Equal("{Binding CanEditPttSettings}", method.Attribute("IsEnabled")?.Value);
        var connection = hardware.Descendants().Single(element => element.Attribute("Name")?.Value == "PttConnectionButton");
        Assert.Equal("OnPttOpenCloseClick", connection.Attribute("Click")?.Value);
        Assert.Equal("{Binding CanTogglePttConnection}", connection.Attribute("IsEnabled")?.Value);
        var ptt = hardware.Descendants().Single(element => element.Attribute("Name")?.Value == "PttButton");
        foreach (var (name, handler) in new[] { ("PointerPressed", "OnPttPointerPressed"), ("PointerReleased", "OnPttPointerReleased"), ("PointerCaptureLost", "OnPttPointerCaptureLost"), ("KeyDown", "OnPttKeyDown"), ("KeyUp", "OnPttKeyUp"), ("LostFocus", "OnPttLostFocus") })
            Assert.Equal(handler, ptt.Attribute(name)?.Value);
        Assert.Equal("{Binding CanPressPtt}", ptt.Attribute("IsEnabled")?.Value);
    }

    [AvaloniaTheory]
    [InlineData(Key.Space)]
    [InlineData(Key.Enter)]
    public void Ptt_key_hold_ignores_repeats_and_releases_once_on_key_up_or_focus_loss(Key key)
    {
        var window = new MainWindow();
        var presses = 0;
        var releases = 0;
        window.ManualPttPressed = () => { presses++; return Task.CompletedTask; };
        window.ManualPttReleased = () => { releases++; return Task.CompletedTask; };
        var button = window.FindControl<Button>("PttButton")!;

        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        Assert.Equal(1, presses);
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.A });
        Assert.Equal(0, releases);
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = key });
        button.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
        Assert.Equal(1, releases);

        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        button.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = key });
        Assert.Equal(2, presses);
        Assert.Equal(2, releases);
    }

    [AvaloniaFact]
    public void Ptt_ignores_other_keys_and_port_selection_does_not_refresh_discovery()
    {
        var window = new MainWindow { AllowExplicitClose = true };
        window.Show();
        var presses = 0;
        var refreshes = 0;
        var connections = 0;
        window.ManualPttPressed = () => { presses++; return Task.CompletedTask; };
        window.PttPortsRefreshRequested = () => { refreshes++; return Task.CompletedTask; };
        window.PttOpenCloseRequested = () => { connections++; return Task.CompletedTask; };
        var button = window.FindControl<Button>("PttButton")!;
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.A });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.A });
        var ports = window.FindControl<ComboBox>("PttPortComboBox")!;
        ports.ItemsSource = new[] { "N/A", "COM4", "COM5" };
        ports.SelectedIndex = 1;
        ports.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
        Assert.Equal(0, presses);
        Assert.Equal(0, refreshes);
        ports.IsDropDownOpen = true;
        Assert.Equal(1, refreshes);
        window.FindControl<Button>("PttConnectionButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, connections);
        window.Close();
    }

    [AvaloniaFact]
    public void Ptt_pointer_hold_releases_once_on_capture_or_focus_loss_and_normal_release()
    {
        var window = new MainWindow();
        var presses = 0;
        var releases = 0;
        window.ManualPttPressed = () => { presses++; return Task.CompletedTask; };
        window.ManualPttReleased = () => { releases++; return Task.CompletedTask; };
        var button = window.FindControl<Button>("PttButton")!;
        var pointer = new Pointer(1, PointerType.Mouse, true);
        var pressed = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        var released = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);

        button.RaiseEvent(new PointerPressedEventArgs(button, pointer, button, default, 0, pressed, KeyModifiers.None, 1));
        Assert.Equal(1, presses);
        button.RaiseEvent(new PointerCaptureLostEventArgs(button, pointer));
        button.RaiseEvent(new PointerReleasedEventArgs(button, pointer, button, default, 1, released, KeyModifiers.None, MouseButton.Left));
        button.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
        Assert.Equal(1, releases);

        button.RaiseEvent(new PointerPressedEventArgs(button, pointer, button, default, 2, pressed, KeyModifiers.None, 1));
        button.RaiseEvent(new PointerReleasedEventArgs(button, pointer, button, default, 3, released, KeyModifiers.None, MouseButton.Left));
        Assert.Equal(2, presses);
        Assert.Equal(2, releases);
    }

    [AvaloniaFact]
    public void Ptt_release_is_routed_even_while_the_async_press_is_still_pending()
    {
        var window = new MainWindow();
        var pendingPress = new TaskCompletionSource();
        var releases = 0;
        window.ManualPttPressed = () => pendingPress.Task;
        window.ManualPttReleased = () => { releases++; return Task.CompletedTask; };
        var button = window.FindControl<Button>("PttButton")!;

        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        button.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
        Assert.Equal(1, releases);
        pendingPress.SetResult();
    }

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

    [Fact]
    public void Protocol_mode_uses_display_converter_and_disables_two_x_for_radiofax()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SstvVPrinter.Desktop", "Views", "MainWindow.axaml"));
        var document = XDocument.Load(path);
        var protocol = document.Descendants().Single(element =>
            element.Attribute("AutomationProperties.Name")?.Value == "Protocol");
        var combo = protocol.Descendants().Single(element => element.Name.LocalName == "ComboBox");
        var checkbox = protocol.Descendants().Single(element =>
            element.Name.LocalName == "CheckBox" && element.Attribute("Content")?.Value == "水平解像度 2x");

        Assert.Contains("TransmissionModeDisplayConverter", combo.ToString());
        Assert.Equal("Stretch", combo.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("{Binding IsHorizontalMultiplierEnabled}", checkbox.Attribute("IsEnabled")?.Value);
        Assert.Contains(protocol.Descendants(), element =>
            element.Name.LocalName == "CheckBox"
            && element.Attribute("Content")?.Value == "ヘッダを画像に重ねる"
            && element.Attribute("IsChecked")?.Value == "{Binding IsHeaderOverlay}");
    }


    [Fact]
    public void Print_queue_uses_datetime_source_and_uuid_columns_in_that_order()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SstvVPrinter.Desktop", "Views", "MainWindow.axaml"));
        var document = XDocument.Load(path);
        var queue = document.Descendants().Single(element =>
            element.Attribute("AutomationProperties.Name")?.Value == "Print job queue");
        var headers = queue.Descendants()
            .Where(element => element.Name.LocalName == "TextBlock")
            .Select(element => element.Attribute("Text")?.Value)
            .Where(value => value is "日時" or "ソース名" or "UUID")
            .Select(value => value!)
            .ToArray();
        var list = queue.Descendants().Single(element => element.Name.LocalName == "ListBox");

        Assert.Equal(["日時", "ソース名", "UUID"], headers);
        Assert.Contains("{Binding AcceptedAt", list.ToString());
        Assert.Contains("{Binding SourceName}", list.ToString());
        Assert.Contains("{Binding Id}", list.ToString());
        Assert.Contains(list.Descendants(), element =>
            element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "HorizontalContentAlignment"
            && element.Attribute("Value")?.Value == "Stretch");
    }

    [Fact]
    public void Actions_panel_contains_delete_after_send_setting_below_buttons()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SstvVPrinter.Desktop", "Views", "MainWindow.axaml"));
        var document = XDocument.Load(path);
        var actions = document.Descendants().Single(element =>
            element.Attribute("AutomationProperties.Name")?.Value == "Actions");
        var children = actions.Descendants().Where(element =>
            element.Name.LocalName is "Grid" or "CheckBox").ToArray();
        var checkbox = children.Single(element =>
            element.Name.LocalName == "CheckBox"
            && element.Attribute("Content")?.Value == "送信完了後に削除");

        Assert.True(Array.IndexOf(children, checkbox) > Array.FindIndex(children, element => element.Name.LocalName == "Grid"));
        Assert.Equal("{Binding DeleteAfterSend}", checkbox.Attribute("IsChecked")?.Value);
    }

    [Fact]
    public void Preview_contains_red_transmission_line_bound_to_scan_position()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SstvVPrinter.Desktop", "Views", "MainWindow.axaml"));
        var document = XDocument.Load(path);
        var preview = document.Descendants().Single(element =>
            element.Attribute("AutomationProperties.Name")?.Value == "SSTV preview");
        var line = preview.Descendants().Single(element =>
            element.Attribute("Name")?.Value == "TransmissionScanLine");

        Assert.Equal("#FFFF3030", line.Attribute("Background")?.Value);
        Assert.Equal("{Binding IsScanLineVisible}", line.Attribute("IsVisible")?.Value);
        Assert.Contains("{Binding ScanLineY}", line.ToString());
    }
}
