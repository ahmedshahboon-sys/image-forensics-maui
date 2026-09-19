using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using SkiaSharp;

namespace ImageForensics.Imaging;

public sealed class PixelForensicsAnalyzer : IPixelForensicsAnalyzer
{
    private const long MaxDecodedPixels = 24_000_000;
    private const int MaxSamples = 1_200_000;

    public Task<PixelForensicsResult> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
        => Task.Run(() => AnalyzeCore(filePath, cancellationToken), cancellationToken);

    private static PixelForensicsResult AnalyzeCore(string filePath, CancellationToken ct)
    {
        using var codec = SKCodec.Create(filePath) ?? throw new InvalidDataException("Unsupported or corrupt image.");
        var pixels = (long)codec.Info.Width * codec.Info.Height;
        if (pixels <= 0 || pixels > MaxDecodedPixels)
            throw new InvalidDataException($"Image exceeds pixel-forensics decode limit ({MaxDecodedPixels:N0} pixels).");

        using var bitmap = SKBitmap.Decode(filePath) ?? throw new InvalidDataException("Could not decode image.");
        var step = Math.Max(1, (int)Math.Sqrt((double)pixels / MaxSamples));

        long count = 0;
        double sum = 0;
        double sumSq = 0;
        long dark = 0;
        long bright = 0;
        double hEdge = 0;
        double vEdge = 0;
        long hCount = 0;
        long vCount = 0;
        double boundary = 0;
        double nonBoundary = 0;
        long boundaryCount = 0;
        long nonBoundaryCount = 0;

        for (var y = 0; y < bitmap.Height; y += step)
        {
            ct.ThrowIfCancellationRequested();
            for (var x = 0; x < bitmap.Width; x += step)
            {
                var l = Luma(bitmap.GetPixel(x, y));
                sum += l;
                sumSq += l * l;
                count++;
                if (l <= 8) dark++;
                if (l >= 247) bright++;

                if (x + step < bitmap.Width)
                {
                    var d = Math.Abs(l - Luma(bitmap.GetPixel(x + step, y)));
                    hEdge += d;
                    hCount++;
                    if (((x + step) % 8) == 0) { boundary += d; boundaryCount++; }
                    else { nonBoundary += d; nonBoundaryCount++; }
                }

                if (y + step < bitmap.Height)
                {
                    var d = Math.Abs(l - Luma(bitmap.GetPixel(x, y + step)));
                    vEdge += d;
                    vCount++;
                    if (((y + step) % 8) == 0) { boundary += d; boundaryCount++; }
                    else { nonBoundary += d; nonBoundaryCount++; }
                }
            }
        }

        if (count == 0)
            throw new InvalidDataException("No pixels could be sampled.");

        var mean = sum / count;
        var variance = Math.Max(0, sumSq / count - mean * mean);
        var boundaryMean = boundaryCount == 0 ? 0 : boundary / boundaryCount;
        var nonBoundaryMean = nonBoundaryCount == 0 ? 0 : nonBoundary / nonBoundaryCount;
        var ratio = nonBoundaryMean <= 0.0001 ? 1.0 : boundaryMean / nonBoundaryMean;

        var indicators = new List<EvidenceItem>();
        if (ratio >= 1.55 && bitmap.Width >= 64 && bitmap.Height >= 64)
        {
            indicators.Add(new EvidenceItem(
                "pixel.jpeg_block_boundary",
                "Elevated 8-pixel block-boundary energy",
                $"Boundary/non-boundary edge ratio = {ratio:F3}.",
                ForensicConfidence.Possible,
                "Sampled luminance discontinuities aligned to an 8-pixel grid.",
                "JPEG compression, screenshots, resizing and normal scene structure can all influence this metric; it is not proof of editing or double compression."));
        }

        return new PixelForensicsResult(
            mean,
            Math.Sqrt(variance),
            (double)dark / count,
            (double)bright / count,
            hCount == 0 ? 0 : hEdge / hCount,
            vCount == 0 ? 0 : vEdge / vCount,
            ratio,
            indicators);
    }

    private static double Luma(SKColor c) => 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue;
}
