using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using Microsoft.Maui.Storage;
using SkiaSharp;
using TesseractOcrMaui;
using TesseractOcrMaui.Results;

namespace ImageForensics.App;

public sealed class OfflineOcrInspector : IOcrInspector
{
    private const long MaxDecodedPixels = 60_000_000;
    private const int MaxSide = 3200;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly ITesseract _tesseract;

    public OfflineOcrInspector(ITesseract tesseract)
        => _tesseract = tesseract;

    public async Task<OcrInspectionResult> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Input image was not found.", filePath);

        await Gate.WaitAsync(cancellationToken);
        string? normalizedPath = null;

        try
        {
            normalizedPath = await NormalizeToPngAsync(filePath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _tesseract.RecognizeTextAsync(normalizedPath);
            cancellationToken.ThrowIfCancellationRequested();

            if (!result.FinishedWithSuccess())
            {
                return new OcrInspectionResult(
                    string.Empty,
                    result.Confidence,
                    "ara+eng",
                    false,
                    result.Message ?? result.Status.ToString());
            }

            return new OcrInspectionResult(
                result.RecognisedText?.Trim() ?? string.Empty,
                result.Confidence,
                "ara+eng",
                true,
                null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new OcrInspectionResult(
                string.Empty,
                -1,
                "ara+eng",
                false,
                $"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Gate.Release();
            if (!string.IsNullOrWhiteSpace(normalizedPath))
            {
                try { if (File.Exists(normalizedPath)) File.Delete(normalizedPath); }
                catch { }
            }
        }
    }

    private static Task<string> NormalizeToPngAsync(
        string inputPath,
        CancellationToken ct)
        => Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            using var codec = SKCodec.Create(inputPath)
                ?? throw new InvalidDataException("Unsupported or corrupt image for OCR.");

            var pixels = (long)codec.Info.Width * codec.Info.Height;
            if (pixels <= 0 || pixels > MaxDecodedPixels)
                throw new InvalidDataException($"OCR skipped: decoded image exceeds safe limit of {MaxDecodedPixels:N0} pixels.");

            using var source = SKBitmap.Decode(inputPath)
                ?? throw new InvalidDataException("Unable to decode image for OCR.");

            SKBitmap? resized = null;
            try
            {
                SKBitmap working = source;
                if (source.Width > MaxSide || source.Height > MaxSide)
                {
                    var scale = Math.Min((double)MaxSide / source.Width, (double)MaxSide / source.Height);
                    resized = source.Resize(
                        new SKImageInfo(
                            Math.Max(1, (int)Math.Round(source.Width * scale)),
                            Math.Max(1, (int)Math.Round(source.Height * scale))),
                        SKSamplingOptions.Default)
                        ?? throw new InvalidDataException("Unable to resize image for OCR.");
                    working = resized;
                }

                var dir = Path.Combine(FileSystem.CacheDirectory, "ocr");
                Directory.CreateDirectory(dir);
                var output = Path.Combine(dir, $"ocr-{Guid.NewGuid():N}.png");

                using var image = SKImage.FromBitmap(working);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100)
                    ?? throw new InvalidOperationException("Unable to encode OCR preview.");
                using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                data.SaveTo(stream);
                return output;
            }
            finally
            {
                resized?.Dispose();
            }
        }, ct);
}
