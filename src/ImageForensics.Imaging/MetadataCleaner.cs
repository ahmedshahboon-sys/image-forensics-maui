using System.Security.Cryptography;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using SkiaSharp;

namespace ImageForensics.Imaging;

public sealed class MetadataCleaner : IMetadataCleaner
{
    private const long MaxDecodedPixels = 60_000_000;

    private static readonly HashSet<string> SensitiveRiskCodes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "privacy.gps",
            "privacy.device_model",
            "privacy.serial",
            "privacy.owner",
            "privacy.timestamp",
            "privacy.software",
            "privacy.thumbnail",
            "privacy.xmp",
            "privacy.iptc",
            "privacy.comments",
            "privacy.trailing"
        };

    private readonly IMetadataInspector _metadata;
    private readonly IContainerInspector _container;
    private readonly IPrivacyRiskAnalyzer _privacy;

    public MetadataCleaner(
        IMetadataInspector metadata,
        IContainerInspector container,
        IPrivacyRiskAnalyzer privacy)
    {
        _metadata = metadata;
        _container = container;
        _privacy = privacy;
    }

    public async Task<MetadataCleanResult> CreateCleanCopyAsync(
        string sourcePath,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        var sourceInfo = new FileInfo(sourcePath);
        if (!sourceInfo.Exists)
            throw new FileNotFoundException("Source image not found.", sourcePath);

        cancellationToken.ThrowIfCancellationRequested();

        var beforeMetadata =
            await _metadata.InspectAsync(
                sourcePath,
                cancellationToken);

        var beforeContainer =
            await _container.InspectAsync(
                sourcePath,
                cancellationToken);

        var beforePrivacy =
            _privacy.Analyze(
                beforeMetadata,
                beforeContainer);

        var originalHash =
            await Sha256Async(
                sourcePath,
                cancellationToken);

        using var codec =
            SKCodec.Create(sourcePath)
            ?? throw new InvalidDataException("Unsupported or corrupt image.");

        if (codec.FrameCount > 1)
        {
            throw new NotSupportedException(
                "Animated/multi-frame images are not cleaned because flattening them would silently change the visual evidence.");
        }

        var sourceWidth = codec.Info.Width;
        var sourceHeight = codec.Info.Height;
        var sourcePixels =
            (long)sourceWidth *
            sourceHeight;

        if (sourceWidth <= 0 ||
            sourceHeight <= 0 ||
            sourcePixels > MaxDecodedPixels)
        {
            throw new InvalidDataException(
                $"Metadata cleaning skipped: decoded image exceeds safe limit of {MaxDecodedPixels:N0} pixels.");
        }

        var sourceOrientation =
            codec.EncodedOrigin.ToString();

        var orientationApplied =
            codec.EncodedOrigin !=
            SKEncodedOrigin.TopLeft;

        using var decoded =
            SKBitmap.Decode(sourcePath)
            ?? throw new InvalidDataException("Could not decode source image.");

        using var oriented =
            ApplyOrientation(
                decoded,
                codec.EncodedOrigin,
                cancellationToken);

        var opaque =
            oriented.AlphaType ==
            SKAlphaType.Opaque;

        var format =
            opaque
                ? SKEncodedImageFormat.Jpeg
                : SKEncodedImageFormat.Png;

        var extension =
            opaque
                ? ".jpg"
                : ".png";

        Directory.CreateDirectory(destinationDirectory);

        var baseName =
            SanitizeBaseName(
                Path.GetFileNameWithoutExtension(
                    sourceInfo.Name));

        var output =
            Path.Combine(
                destinationDirectory,
                $"{baseName}.clean-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}{extension}");

        try
        {
            using (var image =
                   SKImage.FromBitmap(oriented))
            using (var data =
                   image.Encode(
                       format,
                       opaque ? 95 : 100))
            {
                if (data is null)
                    throw new InvalidOperationException("Image encoder failed.");

                await using var destination =
                    new FileStream(
                        output,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        128 * 1024,
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan);

                data.SaveTo(destination);

                await destination.FlushAsync(
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var afterMetadata =
                await _metadata.InspectAsync(
                    output,
                    cancellationToken);

            var afterContainer =
                await _container.InspectAsync(
                    output,
                    cancellationToken);

            var afterPrivacy =
                _privacy.Analyze(
                    afterMetadata,
                    afterContainer);

            var cleanHash =
                await Sha256Async(
                    output,
                    cancellationToken);

            var sourceHashAfter =
                await Sha256Async(
                    sourcePath,
                    cancellationToken);

            var originalUntouched =
                string.Equals(
                    originalHash,
                    sourceHashAfter,
                    StringComparison.OrdinalIgnoreCase);

            using var cleanCodec =
                SKCodec.Create(output)
                ?? throw new InvalidDataException(
                    "Clean output could not be decoded during verification.");

            var cleanOrientation =
                cleanCodec.EncodedOrigin.ToString();

            var cleanWidth =
                cleanCodec.Info.Width;

            var cleanHeight =
                cleanCodec.Info.Height;

            var expectedSwap =
                codec.EncodedOrigin is
                    SKEncodedOrigin.LeftTop or
                    SKEncodedOrigin.RightTop or
                    SKEncodedOrigin.RightBottom or
                    SKEncodedOrigin.LeftBottom;

            var expectedWidth =
                expectedSwap
                    ? sourceHeight
                    : sourceWidth;

            var expectedHeight =
                expectedSwap
                    ? sourceWidth
                    : sourceHeight;

            var notes =
                new List<string>();

            if (!originalUntouched)
                notes.Add("Source hash changed during cleaning.");

            if (afterMetadata.Gps is not null)
                notes.Add("GPS metadata remains in the clean copy.");

            var residualSensitive =
                afterPrivacy.Risks
                    .Where(r =>
                        SensitiveRiskCodes.Contains(r.Code))
                    .Select(r => r.Code)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            if (residualSensitive.Length > 0)
            {
                notes.Add(
                    $"Sensitive privacy indicators remain: {string.Join(", ", residualSensitive)}.");
            }

            if (afterContainer.TrailingBytes > 0)
            {
                notes.Add(
                    $"{afterContainer.TrailingBytes} trailing byte(s) remain after the clean image container.");
            }

            if (cleanCodec.EncodedOrigin !=
                SKEncodedOrigin.TopLeft)
            {
                notes.Add(
                    $"Clean output still reports orientation {cleanOrientation} instead of TopLeft.");
            }

            if (cleanWidth != expectedWidth ||
                cleanHeight != expectedHeight)
            {
                notes.Add(
                    $"Clean dimensions {cleanWidth}x{cleanHeight} do not match expected orientation-normalized dimensions {expectedWidth}x{expectedHeight}.");
            }

            var verificationPassed =
                notes.Count == 0;

            var beforeCodes =
                beforePrivacy.Risks
                    .Select(r => r.Code)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            var afterCodes =
                afterPrivacy.Risks
                    .Select(r => r.Code)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            return new MetadataCleanResult(
                output,
                format.ToString(),
                originalHash,
                cleanHash,
                beforeMetadata.Fields.Count,
                afterMetadata.Fields.Count,
                beforeMetadata.Gps is not null &&
                    afterMetadata.Gps is null,
                originalUntouched,
                beforePrivacy.Risks.Count,
                afterPrivacy.Risks.Count,
                beforeCodes,
                afterCodes,
                beforeContainer.TrailingBytes,
                afterContainer.TrailingBytes,
                orientationApplied,
                sourceOrientation,
                cleanOrientation,
                sourceWidth,
                sourceHeight,
                cleanWidth,
                cleanHeight,
                verificationPassed,
                notes);
        }
        catch
        {
            TryDelete(output);
            throw;
        }
    }

    private static SKBitmap ApplyOrientation(
        SKBitmap source,
        SKEncodedOrigin origin,
        CancellationToken ct)
    {
        var swap =
            origin is
                SKEncodedOrigin.LeftTop or
                SKEncodedOrigin.RightTop or
                SKEncodedOrigin.RightBottom or
                SKEncodedOrigin.LeftBottom;

        var dest =
            new SKBitmap(
                swap
                    ? source.Height
                    : source.Width,
                swap
                    ? source.Width
                    : source.Height,
                source.ColorType,
                source.AlphaType);

        for (var y = 0;
             y < source.Height;
             y++)
        {
            for (var x = 0;
                 x < source.Width;
                 x++)
            {
                if ((((long)y *
                      source.Width +
                      x) &
                     0x3FFF) == 0)
                {
                    ct.ThrowIfCancellationRequested();
                }

                var mapped =
                    origin switch
                    {
                        SKEncodedOrigin.TopRight =>
                            (source.Width - 1 - x, y),
                        SKEncodedOrigin.BottomRight =>
                            (source.Width - 1 - x, source.Height - 1 - y),
                        SKEncodedOrigin.BottomLeft =>
                            (x, source.Height - 1 - y),
                        SKEncodedOrigin.LeftTop =>
                            (y, x),
                        SKEncodedOrigin.RightTop =>
                            (source.Height - 1 - y, x),
                        SKEncodedOrigin.RightBottom =>
                            (source.Height - 1 - y, source.Width - 1 - x),
                        SKEncodedOrigin.LeftBottom =>
                            (y, source.Width - 1 - x),
                        _ =>
                            (x, y)
                    };

                dest.SetPixel(
                    mapped.Item1,
                    mapped.Item2,
                    source.GetPixel(x, y));
            }
        }

        return dest;
    }

    private static async Task<string> Sha256Async(
        string path,
        CancellationToken ct)
    {
        await using var stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                128 * 1024,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

        using var hash =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

        var buffer =
            new byte[128 * 1024];

        while (true)
        {
            var read =
                await stream.ReadAsync(
                    buffer,
                    ct);

            if (read == 0)
                break;

            hash.AppendData(
                buffer,
                0,
                read);
        }

        return Convert
            .ToHexString(
                hash.GetHashAndReset())
            .ToLowerInvariant();
    }

    private static string SanitizeBaseName(
        string name)
    {
        var invalid =
            Path.GetInvalidFileNameChars();

        var chars =
            name
                .Where(c =>
                    !invalid.Contains(c) &&
                    !char.IsControl(c))
                .Take(80)
                .ToArray();

        return chars.Length == 0
            ? "image"
            : new string(chars);
    }

    private static void TryDelete(
        string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}
