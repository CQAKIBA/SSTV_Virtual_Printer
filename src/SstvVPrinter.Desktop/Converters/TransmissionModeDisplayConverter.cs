using System.Globalization;
using Avalonia.Data.Converters;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Desktop.Converters;

public sealed class TransmissionModeDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is SstvMode mode ? TransmissionModes.DisplayName(mode) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
