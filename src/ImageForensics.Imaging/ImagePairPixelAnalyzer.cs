using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using SkiaSharp;

namespace ImageForensics.Imaging;

public sealed class ImagePairPixelAnalyzer : IImagePairPixelAnalyzer
{
    private const long MaxDecodedPixels = 60_000_000;
    private const int CompareSize = 256;

    public Task<PixelComparisonMetrics> CompareAsync(
        string leftPath,
        string rightPath,
        CancellationToken cancellationToken = default)
        => Task.Run(
            () => CompareCore(
                leftPath,
                rightPath,
                cancellationToken),
            cancellationToken);

    private static PixelComparisonMetrics CompareCore(
        string leftPath,
        string rightPath,
        CancellationToken ct)
    {
        using var leftCodec =
            SKCodec.Create(leftPath)
            ?? throw new InvalidDataException("Unable to decode left image.");

        using var rightCodec =
            SKCodec.Create(rightPath)
            ?? throw new InvalidDataException("Unable to decode right image.");

        ValidatePixelCount(leftCodec.Info.Width, leftCodec.Info.Height);
        ValidatePixelCount(rightCodec.Info.Width, rightCodec.Info.Height);

        using var left =
            SKBitmap.Decode(leftPath)
            ?? throw new InvalidDataException("Unable to decode left image.");

        using var right =
            SKBitmap.Decode(rightPath)
            ?? throw new InvalidDataException("Unable to decode right image.");

        using var leftFixed =
            ResizeFixed(
                left,
                CompareSize,
                CompareSize);

        using var rightFixed =
            ResizeFixed(
                right,
                CompareSize,
                CompareSize);

        var metrics =
            CompareBitmaps(
                leftFixed,
                rightFixed,
                ct);

        var cropSimilarity =
            Math.Max(
                DirectionalCenterCropSimilarity(
                    left,
                    right,
                    ct),
                DirectionalCenterCropSimilarity(
                    right,
                    left,
                    ct));

        return new PixelComparisonMetrics(
            metrics.Similarity,
            cropSimilarity,
            metrics.Mae,
            metrics.Rmse,
            metrics.Psnr,
            CompareSize,
            CompareSize);
    }

    private static void ValidatePixelCount(
        int width,
        int height)
    {
        var pixels =
            (long)width *
            height;

        if (width <= 0 ||
            height <= 0 ||
            pixels > MaxDecodedPixels)
        {
            throw new InvalidDataException(
                $"Pair pixel comparison skipped: decoded image exceeds safe limit of {MaxDecodedPixels:N0} pixels.");
        }
    }

    private static double DirectionalCenterCropSimilarity(
        SKBitmap source,
        SKBitmap target,
        CancellationToken ct)
    {
        var targetAspect =
            (double)target.Width /
            target.Height;

        using var cropped =
            CenterCropToAspect(
                source,
                targetAspect);

        using var a =
            ResizeFixed(
                cropped,
                CompareSize,
                CompareSize);

        using var b =
            ResizeFixed(
                target,
                CompareSize,
                CompareSize);

        return CompareBitmaps(
            a,
            b,
            ct).Similarity;
    }

    private static SKBitmap CenterCropToAspect(
        SKBitmap source,
        double targetAspect)
    {
        var sourceAspect =
            (double)source.Width /
            source.Height;

        var x = 0;
        var y = 0;
        var width = source.Width;
        var height = source.Height;

        if (sourceAspect > targetAspect)
        {
            width =
                Math.Max(
                    1,
                    (int)Math.Round(
                        source.Height *
                        targetAspect));

            x =
                Math.Max(
                    0,
                    (source.Width - width) / 2);
        }
        else if (sourceAspect < targetAspect)
        {
            height =
                Math.Max(
                    1,
                    (int)Math.Round(
                        source.Width /
                        targetAspect));

            y =
                Math.Max(
                    0,
                    (source.Height - height) / 2);
        }

        var subset =
            new SKBitmap(
                width,
                height,
                SKColorType.Rgba8888,
                SKAlphaType.Premul);

        using var canvas =
            new SKCanvas(subset);

        canvas.DrawBitmap(
            source,
            new SKRect(
                x,
                y,
                x + width,
                y + height),
            new SKRect(
                0,
                0,
                width,
                height));

        return subset;
    }

    private static SKBitmap ResizeFixed(
        SKBitmap source,
        int width,
        int height)
        => source.Resize(
               new SKImageInfo(
                   width,
                   height,
                   SKColorType.Rgba8888,
                   SKAlphaType.Premul),
               SKSamplingOptions.Default)
           ?? throw new InvalidDataException(
               "Unable to normalize image for comparison.");

    private static (
        double Similarity,
        double Mae,
        double Rmse,
        double? Psnr)
        CompareBitmaps(
            SKBitmap left,
            SKBitmap right,
            CancellationToken ct)
    {
        double absolute = 0;
        double squared = 0;
        long components = 0;

        for (var y = 0;
             y < left.Height;
             y++)
        {
            ct.ThrowIfCancellationRequested();

            for (var x = 0;
                 x < left.Width;
                 x++)
            {
                var a =
                    left.GetPixel(x, y);

                var b =
                    right.GetPixel(x, y);

                Accumulate(
                    a.Red,
                    b.Red,
                    ref absolute,
                    ref squared,
                    ref components);

                Accumulate(
                    a.Green,
                    b.Green,
                    ref absolute,
                    ref squared,
                    ref components);

                Accumulate(
                    a.Blue,
                    b.Blue,
                    ref absolute,
                    ref squared,
                    ref components);
            }
        }

        if (components == 0)
            return (0, 255, 255, 0);

        var mae =
            absolute /
            components;

        var rmse =
            Math.Sqrt(
                squared /
                components);

        var similarity =
            Math.Clamp(
                1.0 -
                mae / 255.0,
                0,
                1);

        double? psnr =
            rmse <= 0.0000001
                ? null
                : 20.0 *
                  Math.Log10(
                      255.0 /
                      rmse);

        return (
            similarity,
            mae,
            rmse,
            psnr);
    }

    private static void Accumulate(
        byte left,
        byte right,
        ref double absolute,
        ref double squared,
        ref long components)
    {
        var diff =
            left - right;

        absolute +=
            Math.Abs(diff);

        squared +=
            diff * diff;

        components++;
    }
}
