using System.Security.Cryptography;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using SkiaSharp;

namespace ImageForensics.Imaging;

public sealed class MetadataCleaner : IMetadataCleaner
{
    private readonly IMetadataInspector _metadata;

    public MetadataCleaner(IMetadataInspector metadata) => _metadata = metadata;

    public async Task<MetadataCleanResult> CreateCleanCopyAsync(
        string sourcePath,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        var sourceInfo = new FileInfo(sourcePath);
        if (!sourceInfo.Exists) throw new FileNotFoundException("Source image not found.", sourcePath);

        var before = await _metadata.InspectAsync(sourcePath, cancellationToken);
        var originalHash = await Sha256Async(sourcePath, cancellationToken);

        using var codec = SKCodec.Create(sourcePath) ?? throw new InvalidDataException("Unsupported or corrupt image.");
        if (codec.FrameCount > 1)
            throw new NotSupportedException("Animated/multi-frame images are not cleaned yet because flattening them would change evidence.");

        using var decoded = SKBitmap.Decode(sourcePath) ?? throw new InvalidDataException("Could not decode source image.");
        using var oriented = ApplyOrientation(decoded, (int)codec.EncodedOrigin, cancellationToken);

        var opaque = oriented.AlphaType == SKAlphaType.Opaque;
        var format = opaque ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png;
        var extension = opaque ? ".jpg" : ".png";

        Directory.CreateDirectory(destinationDirectory);
        var baseName = SanitizeBaseName(Path.GetFileNameWithoutExtension(sourceInfo.Name));
        var output = Path.Combine(destinationDirectory, $"{baseName}.clean-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}{extension}");

        using (var image = SKImage.FromBitmap(oriented))
        using (var data = image.Encode(format, opaque ? 95 : 100))
        await using (var destination = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, true))
        {
            if (data is null) throw new InvalidOperationException("Image encoder failed.");
            data.SaveTo(destination);
            await destination.FlushAsync(cancellationToken);
        }

        var after = await _metadata.InspectAsync(output, cancellationToken);
        var cleanHash = await Sha256Async(output, cancellationToken);

        return new MetadataCleanResult(
            output,
            format.ToString(),
            originalHash,
            cleanHash,
            before.Fields.Count,
            after.Fields.Count,
            before.Gps is not null && after.Gps is null,
            File.Exists(sourcePath) && originalHash == await Sha256Async(sourcePath, cancellationToken));
    }

    private static SKBitmap ApplyOrientation(SKBitmap source, int origin, CancellationToken ct)
    {
        var swap = origin is 5 or 6 or 7 or 8;
        var dest = new SKBitmap(
            swap ? source.Height : source.Width,
            swap ? source.Width : source.Height,
            source.ColorType,
            source.AlphaType);

        for (var y = 0; y < source.Height; y++)
        for (var x = 0; x < source.Width; x++)
        {
            if (((y * source.Width + x) & 0x3FFF) == 0)
                ct.ThrowIfCancellationRequested();

            var (dx, dy) = origin switch
            {
                2 => (source.Width - 1 - x, y),
                3 => (source.Width - 1 - x, source.Height - 1 - y),
                4 => (x, source.Height - 1 - y),
                5 => (y, x),
                6 => (source.Height - 1 - y, x),
                7 => (source.Height - 1 - y, source.Width - 1 - x),
                8 => (y, source.Width - 1 - x),
                _ => (x, y)
            };
            dest.SetPixel(dx, dy, source.GetPixel(x, y));
        }

        return dest;
    }

    private static async Task<string> Sha256Async(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break;
            hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string SanitizeBaseName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Where(c => !invalid.Contains(c) && !char.IsControl(c)).Take(80).ToArray();
        return chars.Length == 0 ? "image" : new string(chars);
    }
}
