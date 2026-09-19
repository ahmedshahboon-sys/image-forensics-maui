using System.Globalization;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using SkiaSharp;

namespace ImageForensics.Imaging;

public sealed class PerceptualHashService : IPerceptualHashService
{
    private const long MaxDecodedPixels = 24_000_000;

    public Task<PerceptualHashResult> ComputeAsync(string filePath, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var codec = SKCodec.Create(filePath) ?? throw new InvalidDataException("Unsupported or corrupt image.");
            var pixels = (long)codec.Info.Width * codec.Info.Height;
            if (pixels <= 0 || pixels > MaxDecodedPixels)
                throw new InvalidDataException($"Image exceeds perceptual-hash decode limit ({MaxDecodedPixels:N0} pixels).");

            using var bitmap = SKBitmap.Decode(filePath) ?? throw new InvalidDataException("Image could not be decoded.");
            var a = AverageHash(bitmap, cancellationToken);
            var d = DifferenceHash(bitmap, cancellationToken);
            var p = PerceptualHash(bitmap, cancellationToken);
            return new PerceptualHashResult(a, d, p);
        }, cancellationToken);

    private static string AverageHash(SKBitmap bitmap, CancellationToken ct)
    {
        Span<double> values = stackalloc double[64];
        double sum = 0;
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            ct.ThrowIfCancellationRequested();
            var v = Luma(Sample(bitmap, x, y, 8, 8));
            values[y * 8 + x] = v;
            sum += v;
        }

        var avg = sum / 64.0;
        ulong bits = 0;
        for (var i = 0; i < 64; i++)
            if (values[i] >= avg) bits |= 1UL << i;
        return bits.ToString("x16", CultureInfo.InvariantCulture);
    }

    private static string DifferenceHash(SKBitmap bitmap, CancellationToken ct)
    {
        ulong bits = 0;
        var bit = 0;
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            ct.ThrowIfCancellationRequested();
            var left = Luma(Sample(bitmap, x, y, 9, 8));
            var right = Luma(Sample(bitmap, x + 1, y, 9, 8));
            if (left > right) bits |= 1UL << bit;
            bit++;
        }
        return bits.ToString("x16", CultureInfo.InvariantCulture);
    }

    private static string PerceptualHash(SKBitmap bitmap, CancellationToken ct)
    {
        const int n = 32;
        var samples = new double[n, n];
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
            samples[y, x] = Luma(Sample(bitmap, x, y, n, n));

        var coeff = new double[8, 8];
        for (var v = 0; v < 8; v++)
        for (var u = 0; u < 8; u++)
        {
            ct.ThrowIfCancellationRequested();
            double sum = 0;
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
                sum += samples[y, x] *
                       Math.Cos((2 * x + 1) * u * Math.PI / (2 * n)) *
                       Math.Cos((2 * y + 1) * v * Math.PI / (2 * n));
            coeff[v, u] = sum;
        }

        var values = new List<double>(63);
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
            if (!(x == 0 && y == 0)) values.Add(coeff[y, x]);

        values.Sort();
        var median = values[values.Count / 2];

        ulong bits = 0;
        var bit = 0;
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            if (x == 0 && y == 0) continue;
            if (coeff[y, x] >= median) bits |= 1UL << bit;
            bit++;
        }
        return bits.ToString("x16", CultureInfo.InvariantCulture);
    }

    private static SKColor Sample(SKBitmap bitmap, int x, int y, int gridWidth, int gridHeight)
    {
        var sx = Math.Clamp((int)((x + 0.5) * bitmap.Width / gridWidth), 0, bitmap.Width - 1);
        var sy = Math.Clamp((int)((y + 0.5) * bitmap.Height / gridHeight), 0, bitmap.Height - 1);
        return bitmap.GetPixel(sx, sy);
    }

    private static double Luma(SKColor c) => 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue;
}
