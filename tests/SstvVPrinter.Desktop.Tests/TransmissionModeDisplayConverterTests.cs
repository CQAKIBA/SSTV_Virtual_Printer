using System.Globalization;
using SstvVPrinter.Desktop.Converters;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Desktop.Tests;

public sealed class TransmissionModeDisplayConverterTests
{
    [Theory]
    [InlineData(SstvMode.Scottie2, "Scottie 2")]
    [InlineData(SstvMode.Radiofax120Ioc288, "Radiofax 120/288")]
    [InlineData(SstvMode.Radiofax120Ioc576, "Radiofax 120/576")]
    public void Convert_uses_transmission_catalog_labels(SstvMode mode, string expected)
    {
        var converter = new TransmissionModeDisplayConverter();

        Assert.Equal(expected, converter.Convert(mode, typeof(string), null, CultureInfo.InvariantCulture));
    }
}
