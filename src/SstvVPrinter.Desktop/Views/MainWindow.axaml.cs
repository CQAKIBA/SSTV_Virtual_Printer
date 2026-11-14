using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Data.Converters;
using SstvVPrinter.Desktop.Ptt;
using SstvVPrinter.Desktop.Imaging;
using SstvVPrinter.Desktop.ViewModels;

namespace SstvVPrinter.Desktop.Views;

public sealed partial class MainWindow : Window
{
    public static readonly FuncValueConverter<PttControlMethod, string> PttMethodDisplayConverter =
        new(method => method.ToString().ToUpperInvariant());

    private bool _printerOperationRunning;
    private bool _manualPttHeld;
    private Key? _manualPttKey;
    private IPointer? _manualPttPointer;
    private long _manualPttGeneration;
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        // Handle momentary input before Button consumes Enter or captures the pointer.
        var pttButton = this.FindControl<Button>("PttButton")!;
        pttButton.RemoveHandler(PointerPressedEvent, OnPttPointerPressed);
        pttButton.RemoveHandler(PointerReleasedEvent, OnPttPointerReleased);
        pttButton.RemoveHandler(KeyDownEvent, OnPttKeyDown);
        pttButton.RemoveHandler(KeyUpEvent, OnPttKeyUp);
        pttButton.AddHandler(PointerPressedEvent, OnPttPointerPressed, RoutingStrategies.Tunnel);
        pttButton.AddHandler(PointerReleasedEvent, OnPttPointerReleased, RoutingStrategies.Tunnel);
        pttButton.AddHandler(KeyDownEvent, OnPttKeyDown, RoutingStrategies.Tunnel);
        pttButton.AddHandler(KeyUpEvent, OnPttKeyUp, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) => AttachViewModel();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
        Deactivated += async (_, _) => await ReleaseManualPttAsync();
        Closing += (_, args) =>
        {
            if (!AllowExplicitClose)
            {
                args.Cancel = true;
                Hide();
            }
        };
    }

    public bool AllowExplicitClose { get; set; }
    public Func<Task>? SendRequested { get; set; }
    public Func<string, Task>? SaveWaveRequested { get; set; }
    public Action? DiscardRequested { get; set; }
    public Action? StopRequested { get; set; }
    public Func<bool, Task>? StartupChanged { get; set; }
    public Func<Task>? ExitRequested { get; set; }
    public Func<Task>? VerifyPrinterRequested { get; set; }
    public Func<Task>? RegisterPrinterRequested { get; set; }
    public Func<Task>? RepairPrinterRequested { get; set; }
    public Func<Task>? CallSignCommitRequested { get; set; }
    public Func<Task>? PttPortsRefreshRequested { get; set; }
    public Func<Task>? PttOpenCloseRequested { get; set; }
    public Func<Task>? ManualPttPressed { get; set; }
    public Func<Task>? ManualPttReleased { get; set; }

    private async void OnPttPortDropDownOpened(object? sender, EventArgs args)
    {
        if (PttPortsRefreshRequested is not null) await PttPortsRefreshRequested();
    }

    private async void OnPttOpenCloseClick(object? sender, RoutedEventArgs args)
    {
        if (PttOpenCloseRequested is not null) await PttOpenCloseRequested();
    }

    private async void OnPttPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (sender is not Button { IsEffectivelyEnabled: true } button
            || !args.GetCurrentPoint(button).Properties.IsLeftButtonPressed || _manualPttHeld) return;
        args.Handled = true;
        button.Focus();
        _manualPttHeld = true;
        _manualPttPointer = args.Pointer;
        args.Pointer.Capture(button);
        await BeginManualPttAsync();
    }

    private async void OnPttPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (args.InitialPressMouseButton != MouseButton.Left) return;
        args.Handled = true;
        await ReleaseManualPttAsync();
    }

    private async void OnPttPointerCaptureLost(object? sender, PointerCaptureLostEventArgs args) =>
        await ReleaseManualPttAsync();

    private async void OnPttKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key is not (Key.Space or Key.Enter)) return;
        args.Handled = true;
        if (_manualPttHeld || sender is not Button { IsEffectivelyEnabled: true }) return;
        _manualPttHeld = true;
        _manualPttKey = args.Key;
        await BeginManualPttAsync();
    }

    private async Task BeginManualPttAsync()
    {
        var generation = ++_manualPttGeneration;
        try
        {
            if (ManualPttPressed is not null) await ManualPttPressed();
        }
        catch (Exception)
        {
            // The delegate owns error reporting; failed/canceled presses still relinquish input.
            if (generation == _manualPttGeneration) await ReleaseManualPttAsync();
        }
    }

    private async void OnPttKeyUp(object? sender, KeyEventArgs args)
    {
        if (args.Key is not (Key.Space or Key.Enter)) return;
        args.Handled = true;
        if (_manualPttKey == args.Key) await ReleaseManualPttAsync();
    }

    private async void OnPttLostFocus(object? sender, RoutedEventArgs args) => await ReleaseManualPttAsync();

    private Task ReleaseManualPttAsync()
    {
        if (!_manualPttHeld) return Task.CompletedTask;
        _manualPttHeld = false;
        _manualPttKey = null;
        var pointer = _manualPttPointer;
        _manualPttPointer = null;
        pointer?.Capture(null);
        return ManualPttReleased?.Invoke() ?? Task.CompletedTask;
    }

    private async void OnCallSignKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Enter || sender is not TextBox textBox || CallSignCommitRequested is null) return;
        args.Handled = true;
        var caretIndex = textBox.CaretIndex;
        var selectionStart = textBox.SelectionStart;
        var selectionEnd = textBox.SelectionEnd;
        await CallSignCommitRequested();
        textBox.Focus();
        textBox.CaretIndex = Math.Min(caretIndex, textBox.Text?.Length ?? 0);
        textBox.SelectionStart = Math.Min(selectionStart, textBox.Text?.Length ?? 0);
        textBox.SelectionEnd = Math.Min(selectionEnd, textBox.Text?.Length ?? 0);
    }

    private async void OnCallSignLostFocus(object? sender, RoutedEventArgs args)
    {
        if (CallSignCommitRequested is not null) await CallSignCommitRequested();
    }

    private async void OnSendClick(object? sender, RoutedEventArgs args)
    {
        if (SendRequested is not null) await SendRequested();
    }

    private async void OnSaveWaveClick(object? sender, RoutedEventArgs args)
    {
        if (SaveWaveRequested is null) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "SSTV WAVを保存",
            SuggestedFileName = $"sstv-{DateTime.Now:yyyyMMdd-HHmmss}.wav",
            DefaultExtension = "wav",
            FileTypeChoices = [new FilePickerFileType("WAV audio") { Patterns = ["*.wav"] }],
        });
        if (file is not null) await SaveWaveRequested(file.Path.LocalPath);
    }

    private void OnDiscardClick(object? sender, RoutedEventArgs args) => DiscardRequested?.Invoke();
    private void OnStopClick(object? sender, RoutedEventArgs args) => StopRequested?.Invoke();
    private void OnRotateClockwiseClick(object? sender, RoutedEventArgs args) =>
        (DataContext as MainWindowViewModel)?.RotateClockwise();
    private void OnRotateCounterclockwiseClick(object? sender, RoutedEventArgs args) =>
        (DataContext as MainWindowViewModel)?.RotateCounterclockwise();
    private async void OnStartupClick(object? sender, RoutedEventArgs args)
    {
        if (sender is MenuItem item && StartupChanged is not null) await StartupChanged(item.IsChecked);
    }
    private async void OnExitClick(object? sender, RoutedEventArgs args) { if (ExitRequested is not null) await ExitRequested(); }
    private async void OnVerifyPrinterClick(object? sender, RoutedEventArgs args) => await RunPrinterOperationAsync(VerifyPrinterRequested);
    private async void OnRegisterPrinterClick(object? sender, RoutedEventArgs args) => await RunPrinterOperationAsync(RegisterPrinterRequested);
    private async void OnRepairPrinterClick(object? sender, RoutedEventArgs args) => await RunPrinterOperationAsync(RepairPrinterRequested);

    private async Task RunPrinterOperationAsync(Func<Task>? operation)
    {
        if (_printerOperationRunning || operation is null) return;
        _printerOperationRunning = true;
        try { await operation(); }
        finally { _printerOperationRunning = false; }
    }

    public async Task<bool> ConfirmPendingExitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = false;
        var dialog = new Window
        {
            Title = "未送信ジョブがあります",
            Width = 420,
            Height = 170,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var yes = new Button { Content = "終了する", MinWidth = 100 };
        var no = new Button { Content = "戻る", MinWidth = 100 };
        yes.Click += (_, _) => { result = true; dialog.Close(); };
        no.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 18,
            Children =
            {
                new TextBlock { Text = "待機中の印刷ジョブは終了時に破棄されます。" },
                new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Children = { no, yes } },
            },
        };
        await dialog.ShowDialog(this);
        return result;
    }

    private void AttachViewModel()
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.Preview))
            {
                viewModel.PreviewBitmap?.Dispose();
                viewModel.PreviewBitmap = viewModel.Preview is { } preview
                    ? AvaloniaFrameBitmapFactory.Create(
                        preview.Frame, preview.Options.HorizontalSampleMultiplier)
                    : null;
            }
        };
    }

    private void ApplyResponsiveLayout()
    {
        var layout = this.FindControl<Grid>("ContentLayout");
        var panel = this.FindControl<ScrollViewer>("ControlPanel");
        if (layout is null || panel is null) return;
        if (Bounds.Width < 900)
        {
            layout.ColumnDefinitions = new ColumnDefinitions("*");
            layout.RowDefinitions = new RowDefinitions("*,Auto");
            Grid.SetColumn(panel, 0);
            Grid.SetRow(panel, 1);
            panel.MaxHeight = 360;
        }
        else
        {
            layout.ColumnDefinitions = new ColumnDefinitions("3*,340");
            layout.RowDefinitions = new RowDefinitions("*");
            Grid.SetColumn(panel, 1);
            Grid.SetRow(panel, 0);
            panel.MaxHeight = double.PositiveInfinity;
        }
    }
}
