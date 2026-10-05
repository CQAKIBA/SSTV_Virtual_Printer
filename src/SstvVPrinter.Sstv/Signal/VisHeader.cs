namespace SstvVPrinter.Sstv.Signal;

public static class VisHeader
{
    public static IEnumerable<ToneSpan> Plan(byte visCode)
    {
        if (visCode > 0x7f)
        {
            throw new ArgumentOutOfRangeException(nameof(visCode));
        }

        yield return new ToneSpan(1900, 0.300);
        yield return new ToneSpan(1200, 0.010);
        yield return new ToneSpan(1900, 0.300);
        yield return new ToneSpan(1200, 0.030);

        var ones = 0;
        for (var bit = 0; bit < 7; bit++)
        {
            var isOne = ((visCode >> bit) & 1) == 1;
            if (isOne)
            {
                ones++;
            }

            yield return new ToneSpan(isOne ? 1100 : 1300, 0.030);
        }

        yield return new ToneSpan((ones & 1) == 1 ? 1100 : 1300, 0.030);
        yield return new ToneSpan(1200, 0.030);
    }
}
