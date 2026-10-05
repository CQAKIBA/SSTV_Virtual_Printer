using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using SstvVPrinter.Desktop.Imaging;
using SstvVPrinter.Desktop.ViewModels;

namespace SstvVPrinter.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private bool _printerOperationRunning;
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) => AttachViewModel();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
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
            Margin = new Avalonia.Thickness(20), Spacing = 18,
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
                    ? AvaloniaFrameBitmapFactory.Create(preview.Frame)
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
