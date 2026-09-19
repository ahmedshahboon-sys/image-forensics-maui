using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using SkiaSharp;

namespace ImageForensics.Imaging;

public sealed class ImageDiagnosticGenerator : IImageDiagnosticGenerator
{
    private const long MaxDecodedPixels = 24_000_000;

    public Task<DiagnosticImageResult> GenerateElaAsync(
        string sourcePath,
        string destinationDirectory,
        int jpegQuality = 90,
        int amplification = 12,
        CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            jpegQuality = Math.Clamp(jpegQuality, 50, 100);
            amplification = Math.Clamp(amplification, 1, 50);
            using var original = DecodeSafe(sourcePath);
            cancellationToken.ThrowIfCancellationRequested();

            using var originalImage = SKImage.FromBitmap(original);
            using var jpegData = originalImage.Encode(SKEncodedImageFormat.Jpeg, jpegQuality)
                ?? throw new InvalidOperationException("JPEG re-encode failed.");
            using var recompressed = SKBitmap.Decode(jpegData)
                ?? throw new InvalidDataException("Could not decode ELA comparison image.");

            using var output = new SKBitmap(original.Width, original.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
            for (var y = 0; y < original.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < original.Width; x++)
                {
                    var a = original.GetPixel(x, y);
                    var b = recompressed.GetPixel(x, y);
                    var r = Math.Clamp(Math.Abs(a.Red - b.Red) * amplification, 0, 255);
                    var g = Math.Clamp(Math.Abs(a.Green - b.Green) * amplification, 0, 255);
                    var bl = Math.Clamp(Math.Abs(a.Blue - b.Blue) * amplification, 0, 255);
                    output.SetPixel(x, y, new SKColor((byte)r, (byte)g, (byte)bl));
                }
            }

            var path = SavePng(output, destinationDirectory, "ela");
            return new DiagnosticImageResult(
                path,
                "ELA",
                "ELA is a visualization aid only. Recompression history, gradients, texture and source encoding can create patterns unrelated to manipulation.");
        }, cancellationToken);

    public Task<DiagnosticImageResult> GenerateChannelAsync(
        string sourcePath,
        string destinationDirectory,
        char channel,
        CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            using var source = DecodeSafe(sourcePath);
            channel = char.ToUpperInvariant(channel);
            if (channel is not ('R' or 'G' or 'B' or 'A'))
                throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be R, G, B or A.");

            using var output = new SKBitmap(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
            for (var y = 0; y < source.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < source.Width; x++)
                {
                    var p = source.GetPixel(x, y);
                    var value = channel switch { 'R' => p.Red, 'G' => p.Green, 'B' => p.Blue, _ => p.Alpha };
                    output.SetPixel(x, y, new SKColor(value, value, value));
                }
            }

            var path = SavePng(output, destinationDirectory, $"channel-{channel}");
            return new DiagnosticImageResult(path, $"Channel {channel}", "Channel visualization shows pixel values only and does not by itself establish manipulation.");
        }, cancellationToken);

    public Task<DiagnosticImageResult> GenerateBitPlaneAsync(
        string sourcePath,
        string destinationDirectory,
        char channel,
        int bit,
        CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            using var source = DecodeSafe(sourcePath);
            channel = char.ToUpperInvariant(channel);
            if (channel is not ('R' or 'G' or 'B' or 'A'))
                throw new ArgumentOutOfRangeException(nameof(channel));
            if (bit is < 0 or > 7)
                throw new ArgumentOutOfRangeException(nameof(bit));

            using var output = new SKBitmap(source.Width, source.Height, SKColorType.Gray8, SKAlphaType.Opaque);
            for (var y = 0; y < source.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < source.Width; x++)
                {
                    var p = source.GetPixel(x, y);
                    var value = channel switch { 'R' => p.Red, 'G' => p.Green, 'B' => p.Blue, _ => p.Alpha };
                    var v = ((value >> bit) & 1) == 1 ? (byte)255 : (byte)0;
                    output.SetPixel(x, y, new SKColor(v, v, v));
                }
            }

            var path = SavePng(output, destinationDirectory, $"bitplane-{channel}-{bit}");
            return new DiagnosticImageResult(path, $"Bit plane {channel}:{bit}", "Visible bit-plane patterns are heuristic observations, not proof of steganography.");
        }, cancellationToken);

    public Task<DiagnosticImageResult> GenerateDifferenceMapAsync(
        string leftPath,
        string rightPath,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            using var left = DecodeSafe(leftPath);
            using var right = DecodeSafe(rightPath);
            if (left.Width != right.Width || left.Height != right.Height)
                throw new InvalidOperationException("Pixel difference map requires equal image dimensions.");

            using var output = new SKBitmap(left.Width, left.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
            for (var y = 0; y < left.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < left.Width; x++)
                {
                    var a = left.GetPixel(x, y);
                    var b = right.GetPixel(x, y);
                    var dr = Math.Abs(a.Red - b.Red);
                    var dg = Math.Abs(a.Green - b.Green);
                    var db = Math.Abs(a.Blue - b.Blue);
                    output.SetPixel(x, y, new SKColor(
                        (byte)Math.Clamp(dr * 4, 0, 255),
                        (byte)Math.Clamp(dg * 4, 0, 255),
                        (byte)Math.Clamp(db * 4, 0, 255)));
                }
            }

            var path = SavePng(output, destinationDirectory, "difference");
            return new DiagnosticImageResult(path, "Pixel difference map", "Difference amplification is for visualization and does not identify the cause of a change.");
        }, cancellationToken);

    private static SKBitmap DecodeSafe(string path)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException("Unsupported or corrupt image.");
        var pixels = (long)codec.Info.Width * codec.Info.Height;
        if (pixels <= 0 || pixels > MaxDecodedPixels)
            throw new InvalidDataException($"Image exceeds diagnostic decode limit ({MaxDecodedPixels:N0} pixels).");
        return SKBitmap.Decode(path) ?? throw new InvalidDataException("Could not decode image.");
    }

    private static string SavePng(SKBitmap bitmap, string destinationDirectory, string kind)
    {
        Directory.CreateDirectory(destinationDirectory);
        var path = Path.Combine(destinationDirectory, $"{kind}-{Guid.NewGuid():N}.png");
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("PNG encode failed.");
        using var stream = File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
        return path;
    }
}
