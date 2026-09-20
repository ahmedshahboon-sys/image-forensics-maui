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

            SKBitmap bitmap;

            try
            {
                bitmap =
                    BoundedImageDecoder.DecodePreview(
                        filePath,
                        2048,
                        MaxDecodedPixels,
                        cancellationToken);
            }
            catch (InvalidDataException)
            {
                return Array.Empty<BarcodeHit>();
            }

            using (bitmap)
            {

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
            }
        }, cancellationToken);
}
