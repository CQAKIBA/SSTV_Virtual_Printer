using System.Globalization;
using SstvVPrinter.Desktop.Converters;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Desktop.Tests;

public sealed class TransmissionModeDisplayConverterTests
{
    [Theory]
    [InlineData(SstvMode.Scottie2, "Scottie 2")]
    [InlineData(SstvMode.Radiofax120Ioc288Sync10Ms, "RadioFAX - 120/288/10ms")]
    [InlineData(SstvMode.Radiofax120Ioc288Sync20Ms, "RadioFAX - 120/288/20ms")]
    [InlineData(SstvMode.Radiofax120Ioc576Sync10Ms, "RadioFAX - 120/576/10ms")]
    [InlineData(SstvMode.Radiofax120Ioc576Sync20Ms, "RadioFAX - 120/576/20ms (JMH Standard)")]
    public void Convert_uses_transmission_catalog_labels(SstvMode mode, string expected)
    {
        var converter = new TransmissionModeDisplayConverter();

        Assert.Equal(expected, converter.Convert(mode, typeof(string), null, CultureInfo.InvariantCulture));
    }
}
