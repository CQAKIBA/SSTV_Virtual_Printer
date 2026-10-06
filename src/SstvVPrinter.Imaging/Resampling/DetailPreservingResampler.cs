using SstvVPrinter.Imaging.Model;

namespace SstvVPrinter.Imaging.Resampling;

public class DetailPreservingResampler
{
    public virtual FittedImage Fit(
        DecodedPage source,
        int targetWidth,
        int targetHeight,
        CancellationToken cancellationToken = default)
        => Fit(source, targetWidth, targetHeight, 1, cancellationToken);

    public virtual FittedImage Fit(
        DecodedPage source,
        int targetWidth,
        int targetHeight,
        int horizontalSampleMultiplier,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);
        if (horizontalSampleMultiplier is not (1 or 2)
            || targetWidth % horizontalSampleMultiplier != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(horizontalSampleMultiplier));
        }

        var logicalTargetWidth = targetWidth / horizontalSampleMultiplier;
        var sourceIsWider = checked((long)source.Width * targetHeight) >= checked((long)source.Height * logicalTargetWidth);
        int fittedWidth;
        int fittedHeight;
        if (sourceIsWider)
        {
            fittedWidth = targetWidth;
            fittedHeight = Math.Max(1, (int)Math.Round(
                (double)logicalTargetWidth * source.Height / source.Width,
                MidpointRounding.AwayFromZero));
        }
        else
        {
            fittedHeight = targetHeight;
            var logicalFittedWidth = Math.Max(1, (int)Math.Round(
                (double)targetHeight * source.Width / source.Height,
                MidpointRounding.AwayFromZero));
            fittedWidth = checked(logicalFittedWidth * horizontalSampleMultiplier);
        }

        fittedWidth = Math.Min(fittedWidth, targetWidth);
        fittedHeight = Math.Min(fittedHeight, targetHeight);
        var bounds = new PixelRect(
            (targetWidth - fittedWidth) / 2,
            (targetHeight - fittedHeight) / 2,
            fittedWidth,
            fittedHeight);
        var canvas = new byte[checked(targetWidth * targetHeight * 3)];
        var working = new byte[checked(fittedWidth * fittedHeight * 3)];

        var shrinks = source.Width > fittedWidth || source.Height > fittedHeight;
        for (var y = 0; y < fittedHeight; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < fittedWidth; x++)
            {
                var color = shrinks
                    ? SampleArea(source, x, y, fittedWidth, fittedHeight, cancellationToken)
                    : SampleBilinear(source, x, y, fittedWidth, fittedHeight);
                var offset = ((y * fittedWidth) + x) * 3;
                working[offset] = SrgbTransfer.ToByte(color.R);
                working[offset + 1] = SrgbTransfer.ToByte(color.G);
                working[offset + 2] = SrgbTransfer.ToByte(color.B);
            }
        }

        if (shrinks)
        {
            working = SharpenLuminance(working, fittedWidth, fittedHeight, cancellationToken);
        }

        for (var y = 0; y < fittedHeight; y++)
        {
            working.AsSpan(y * fittedWidth * 3, fittedWidth * 3)
                .CopyTo(canvas.AsSpan(((bounds.Y + y) * targetWidth + bounds.X) * 3, fittedWidth * 3));
        }

        return new FittedImage(targetWidth, targetHeight, canvas, bounds);
    }

    private static LinearColor SampleArea(
        DecodedPage source,
        int x,
        int y,
        int targetWidth,
        int targetHeight,
        CancellationToken cancellationToken)
    {
        var x0 = (double)x * source.Width / targetWidth;
        var x1 = (double)(x + 1) * source.Width / targetWidth;
        var y0 = (double)y * source.Height / targetHeight;
        var y1 = (double)(y + 1) * source.Height / targetHeight;
        var sumR = 0.0;
        var sumG = 0.0;
        var sumB = 0.0;
        var sumWeight = 0.0;
        var samples = 0;
        var minimum = new LinearColor(0, 0, 0);
        var maximum = new LinearColor(0, 0, 0);
        var minimumLuminance = double.PositiveInfinity;
        var maximumLuminance = double.NegativeInfinity;
        var cancellationCounter = 0;

        for (var sourceY = (int)Math.Floor(y0); sourceY < Math.Ceiling(y1); sourceY++)
        {
            var weightY = Math.Min(y1, sourceY + 1.0) - Math.Max(y0, sourceY);
            if (weightY <= 0 || sourceY >= source.Height) continue;
            for (var sourceX = (int)Math.Floor(x0); sourceX < Math.Ceiling(x1); sourceX++)
            {
                if ((cancellationCounter++ & 4095) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var weightX = Math.Min(x1, sourceX + 1.0) - Math.Max(x0, sourceX);
                if (weightX <= 0 || sourceX >= source.Width) continue;
                var weight = weightX * weightY;
                var pixel = source.GetPixel(sourceX, sourceY);
                var linear = Linear(pixel);
                sumR += linear.R * weight;
                sumG += linear.G * weight;
                sumB += linear.B * weight;
                sumWeight += weight;
                samples++;
                var luminance = Luminance(linear);
                if (luminance < minimumLuminance)
                {
                    minimumLuminance = luminance;
                    minimum = linear;
                }

                if (luminance > maximumLuminance)
                {
                    maximumLuminance = luminance;
                    maximum = linear;
                }
            }
        }

        var mean = new LinearColor(sumR / sumWeight, sumG / sumWeight, sumB / sumWeight);
        if (samples <= 1)
        {
            return mean;
        }

        var meanLuminance = Luminance(mean);
        var minimumDistance = Math.Abs(meanLuminance - minimumLuminance);
        var maximumDistance = Math.Abs(maximumLuminance - meanLuminance);
        var extremum = maximumDistance > minimumDistance ? maximum : minimum;
        if (Math.Max(minimumDistance, maximumDistance) < 0.125)
        {
            return mean;
        }

        return Lerp(mean, extremum, 0.20);
    }

    private static LinearColor SampleBilinear(DecodedPage source, int x, int y, int targetWidth, int targetHeight)
    {
        var sourceX = Math.Clamp(((x + 0.5) * source.Width / targetWidth) - 0.5, 0, source.Width - 1.0);
        var sourceY = Math.Clamp(((y + 0.5) * source.Height / targetHeight) - 0.5, 0, source.Height - 1.0);
        var x0 = Math.Clamp((int)Math.Floor(sourceX), 0, source.Width - 1);
        var y0 = Math.Clamp((int)Math.Floor(sourceY), 0, source.Height - 1);
        var x1 = Math.Min(x0 + 1, source.Width - 1);
        var y1 = Math.Min(y0 + 1, source.Height - 1);
        var tx = Math.Clamp(sourceX - Math.Floor(sourceX), 0, 1);
        var ty = Math.Clamp(sourceY - Math.Floor(sourceY), 0, 1);
        var top = Lerp(Linear(source.GetPixel(x0, y0)), Linear(source.GetPixel(x1, y0)), tx);
        var bottom = Lerp(Linear(source.GetPixel(x0, y1)), Linear(source.GetPixel(x1, y1)), tx);
        return Lerp(top, bottom, ty);
    }

    private static LinearColor Linear(Sstv.Rgb24 pixel) =>
        new(SrgbTransfer.ToLinear(pixel.R), SrgbTransfer.ToLinear(pixel.G), SrgbTransfer.ToLinear(pixel.B));

    private static LinearColor Lerp(LinearColor first, LinearColor second, double amount) =>
        new(first.R + ((second.R - first.R) * amount),
            first.G + ((second.G - first.G) * amount),
            first.B + ((second.B - first.B) * amount));

    private static double Luminance(LinearColor color) =>
        (0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B);

    private static byte[] SharpenLuminance(byte[] input, int width, int height, CancellationToken cancellationToken)
    {
        var output = new byte[input.Length];
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var center = ReadLinear(input, width, x, y);
                var centerLuminance = Luminance(center);
                var sum = 0.0;
                var count = 0;
                for (var neighborY = Math.Max(0, y - 1); neighborY <= Math.Min(height - 1, y + 1); neighborY++)
                for (var neighborX = Math.Max(0, x - 1); neighborX <= Math.Min(width - 1, x + 1); neighborX++)
                {
                    sum += Luminance(ReadLinear(input, width, neighborX, neighborY));
                    count++;
                }

                var difference = centerLuminance - (sum / count);
                var delta = Math.Abs(difference) < (4.0 / 255.0)
                    ? 0
                    : Math.Clamp(difference * 0.20, -0.10, 0.10);
                var targetLuminance = Math.Clamp(centerLuminance + delta, 0, 1);
                LinearColor sharpened;
                if (centerLuminance > 1e-12)
                {
                    var scale = targetLuminance / centerLuminance;
                    sharpened = new(
                        Math.Clamp(center.R * scale, 0, 1),
                        Math.Clamp(center.G * scale, 0, 1),
                        Math.Clamp(center.B * scale, 0, 1));
                }
                else
                {
                    sharpened = new(targetLuminance, targetLuminance, targetLuminance);
                }

                var offset = ((y * width) + x) * 3;
                output[offset] = SrgbTransfer.ToByte(sharpened.R);
                output[offset + 1] = SrgbTransfer.ToByte(sharpened.G);
                output[offset + 2] = SrgbTransfer.ToByte(sharpened.B);
            }
        }

        return output;
    }

    private static LinearColor ReadLinear(byte[] bytes, int width, int x, int y)
    {
        var offset = ((y * width) + x) * 3;
        return new(
            SrgbTransfer.ToLinear(bytes[offset]),
            SrgbTransfer.ToLinear(bytes[offset + 1]),
            SrgbTransfer.ToLinear(bytes[offset + 2]));
    }

    private readonly record struct LinearColor(double R, double G, double B);
}
