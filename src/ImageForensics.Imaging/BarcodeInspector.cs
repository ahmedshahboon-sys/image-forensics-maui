using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using SkiaSharp;
using ZXing.SkiaSharp;

namespace ImageForensics.Imaging;

public sealed class BarcodeInspector : IBarcodeInspector
{
    private const long MaxDecodedPixels = 24_000_000;

    public Task<IReadOnlyList<BarcodeHit>> InspectAsync(string filePath, CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<BarcodeHit>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var codec = SKCodec.Create(filePath) ?? throw new InvalidDataException("Unsupported or corrupt image.");
            var pixels = (long)codec.Info.Width * codec.Info.Height;
            if (pixels <= 0 || pixels > MaxDecodedPixels)
                return Array.Empty<BarcodeHit>();

            using var bitmap = SKBitmap.Decode(filePath);
            if (bitmap is null) return Array.Empty<BarcodeHit>();

            var reader = new BarcodeReader
            {
                AutoRotate = true,
                Options = new ZXing.Common.DecodingOptions
                {
                    TryHarder = true,
                    TryInverted = true
                }
            };

            var result = reader.Decode(bitmap);
            if (result is null) return Array.Empty<BarcodeHit>();

            return new[] { new BarcodeHit(result.BarcodeFormat.ToString(), result.Text ?? string.Empty) };
        }, cancellationToken);
}
