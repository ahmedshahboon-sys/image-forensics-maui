using SkiaSharp;

namespace ImageForensics.Imaging;

internal static class BoundedImageDecoder
{
    private const long MaxFallbackFullDecodePixels = 16_000_000;

    public static SKBitmap DecodePreview(
        string path,
        int maxSide,
        long maxSourcePixels,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (maxSide <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSide));

        cancellationToken.ThrowIfCancellationRequested();

        using var codec = SKCodec.Create(path)
            ?? throw new InvalidDataException("Unsupported or corrupt image.");

        var width = codec.Info.Width;
        var height = codec.Info.Height;
        var sourcePixels = (long)width * height;

        if (width <= 0 ||
            height <= 0 ||
            sourcePixels <= 0 ||
            sourcePixels > maxSourcePixels)
        {
            throw new InvalidDataException(
                $"Image exceeds safe decoded-pixel limit ({maxSourcePixels:N0} pixels).");
        }

        var scale = Math.Min(
            1f,
            Math.Min(
                maxSide / (float)width,
                maxSide / (float)height));

        var scaled =
            codec.GetScaledDimensions(scale);

        if (scaled.Width <= 0 ||
            scaled.Height <= 0)
        {
            throw new InvalidDataException(
                "Image decoder returned invalid scaled dimensions.");
        }

        var decodeInfo =
            new SKImageInfo(
                scaled.Width,
                scaled.Height,
                SKColorType.Rgba8888,
                SKAlphaType.Premul);

        using var scaledBitmap =
            new SKBitmap(decodeInfo);

        var decodeResult =
            codec.GetPixels(
                decodeInfo,
                scaledBitmap.GetPixels());

        if (decodeResult == SKCodecResult.Success)
        {
            if (scaled.Width <= maxSide &&
                scaled.Height <= maxSide)
            {
                return scaledBitmap.Copy();
            }

            return ResizeWithin(
                scaledBitmap,
                maxSide);
        }

        if (sourcePixels >
            MaxFallbackFullDecodePixels)
        {
            throw new InvalidDataException(
                $"Scaled decode was unavailable ({decodeResult}); refusing a fallback full decode above {MaxFallbackFullDecodePixels:N0} pixels.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var full =
            SKBitmap.Decode(path)
            ?? throw new InvalidDataException(
                $"Image could not be decoded ({decodeResult}).");

        return ResizeWithin(
            full,
            maxSide);
    }

    private static SKBitmap ResizeWithin(
        SKBitmap source,
        int maxSide)
    {
        if (source.Width <= maxSide &&
            source.Height <= maxSide)
        {
            return source.Copy();
        }

        var scale =
            Math.Min(
                (double)maxSide / source.Width,
                (double)maxSide / source.Height);

        return source.Resize(
                   new SKImageInfo(
                       Math.Max(
                           1,
                           (int)Math.Round(
                               source.Width *
                               scale)),
                       Math.Max(
                           1,
                           (int)Math.Round(
                               source.Height *
                               scale)),
                       SKColorType.Rgba8888,
                       SKAlphaType.Premul),
                   SKSamplingOptions.Default)
               ?? throw new InvalidDataException(
                   "Unable to resize decoded image preview.");
    }
}
