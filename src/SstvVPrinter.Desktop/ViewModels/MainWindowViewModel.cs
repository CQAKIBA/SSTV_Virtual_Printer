using CommunityToolkit.Mvvm.ComponentModel;
using SstvVPrinter.App.Jobs;
using SstvVPrinter.App.Transmission;
using SstvVPrinter.App.Validation;

namespace SstvVPrinter.Desktop.ViewModels;

public sealed partial class MainWindowViewModel(ITransmissionPreparationService preparationService)
    : ObservableObject
{
    private CancellationTokenSource? _preparationCancellation;
    private long _preparationGeneration;

    [ObservableProperty]
    private PreparedTransmission? _preview;

    [ObservableProperty]
    private bool _isReady;

    [ObservableProperty]
    private string? _validationError;

    public async Task UpdatePreviewAsync(PrintJob job, TransmissionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(settings);
        var validation = CallSignValidator.Validate(settings.CallSign);
        if (!validation.IsValid)
        {
            _preparationCancellation?.Cancel();
            Interlocked.Increment(ref _preparationGeneration);
            Preview = null;
            IsReady = false;
            ValidationError = validation.Error;
            return;
        }

        _preparationCancellation?.Cancel();
        _preparationCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _preparationCancellation = cancellation;
        var generation = Interlocked.Increment(ref _preparationGeneration);
        IsReady = false;
        ValidationError = null;
        try
        {
            var normalized = settings with { CallSign = validation.Normalized };
            var prepared = await preparationService.PrepareAsync(
                job, normalized, cancellation.Token);
            if (generation == Volatile.Read(ref _preparationGeneration))
            {
                Preview = prepared;
                IsReady = true;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
    }
}
