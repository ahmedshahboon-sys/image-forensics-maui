using System.Security.Cryptography;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using ImageForensics.Forensics.Hashing;

namespace ImageForensics.Forensics.FileIdentity;

public sealed class SafeFileIdentityInspector : IFileIdentityInspector
{
    private readonly AnalysisLimits _limits;

    public SafeFileIdentityInspector(AnalysisLimits? limits = null)
        => _limits = limits ?? new AnalysisLimits();

    public async Task<FileIdentityResult> InspectAsync(
        string filePath,
        string? declaredMime = null,
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var info = new FileInfo(filePath);
        if (!info.Exists)
            throw new FileNotFoundException("Input image was not found.", filePath);
        if (info.Length <= 0)
            throw new InvalidDataException("Input file is empty.");
        if (info.Length > _limits.MaxFileBytes)
            throw new InvalidDataException($"File exceeds configured limit of {_limits.MaxFileBytes} bytes.");

        progress?.Report(new AnalysisProgress("identity", 0.02, "Opening file safely"));

        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            _limits.BufferBytes, FileOptions.Asynchronous | FileOptions.SequentialScan);

        var signature = new byte[Math.Min(32, (int)Math.Min(info.Length, 32))];
        var signatureRead = await stream.ReadAsync(signature, cancellationToken);
        if (signatureRead != signature.Length)
            Array.Resize(ref signature, signatureRead);

        var detected = Detect(signature);
        var extension = info.Extension.ToLowerInvariant();
        var declared = string.IsNullOrWhiteSpace(declaredMime)
            ? MimeFromExtension(extension)
            : declaredMime.Trim().ToLowerInvariant();

        stream.Position = 0;
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);

        var frequencies = new long[256];
        var buffer = new byte[_limits.BufferBytes];
        var crcState = Crc32.InitialState;
        long processed = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;

            var span = buffer.AsSpan(0, read);
            sha256.AppendData(span);
            sha1.AppendData(span);
            md5.AppendData(span);
            crcState = Crc32.Update(crcState, span);

            for (var i = 0; i < read; i++)
                frequencies[buffer[i]]++;

            processed += read;
            progress?.Report(new AnalysisProgress(
                "hashing",
                Math.Clamp((double)processed / info.Length, 0.05, 0.95),
                $"{processed}/{info.Length} bytes"));
        }

        var entropy = CalculateEntropy(frequencies, info.Length);
        var mismatch = detected.Extensions.Count > 0 && !detected.Extensions.Contains(extension);

        progress?.Report(new AnalysisProgress("identity", 1.0, "Completed"));

        return new FileIdentityResult
        {
            FileName = info.Name,
            SafeSource = null,
            SizeBytes = info.Length,
            Extension = extension,
            DeclaredMime = declared,
            DetectedType = detected.Type,
            DetectedMime = detected.Mime,
            ExtensionMismatch = mismatch,
            CreatedUtc = ToNullableUtc(info.CreationTimeUtc),
            ModifiedUtc = ToNullableUtc(info.LastWriteTimeUtc),
            EntropyBitsPerByte = entropy,
            Sha256 = Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant(),
            Sha1 = Convert.ToHexString(sha1.GetHashAndReset()).ToLowerInvariant(),
            Md5 = Convert.ToHexString(md5.GetHashAndReset()).ToLowerInvariant(),
            Crc32 = Crc32.FinalizeHash(crcState).ToString("x8"),
            SignatureHex = Convert.ToHexString(signature).ToLowerInvariant()
        };
    }

    private static DateTimeOffset? ToNullableUtc(DateTime value)
        => value == DateTime.MinValue ? null : new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static double CalculateEntropy(long[] counts, long total)
    {
        if (total <= 0) return 0;
        double entropy = 0;
        foreach (var count in counts)
        {
            if (count == 0) continue;
            var p = (double)count / total;
            entropy -= p * Math.Log2(p);
        }
        return entropy;
    }

    private static string MimeFromExtension(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".tif" or ".tiff" => "image/tiff",
        ".heic" or ".heif" => "image/heif",
        ".avif" => "image/avif",
        _ => "application/octet-stream"
    };

    private static (string Type, string Mime, HashSet<string> Extensions) Detect(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return ("JPEG", "image/jpeg", new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg" });

        if (b.Length >= 8 && b[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ("PNG", "image/png", new(StringComparer.OrdinalIgnoreCase) { ".png" });

        if (b.Length >= 6 && (b[..6].SequenceEqual("GIF87a"u8) || b[..6].SequenceEqual("GIF89a"u8)))
            return ("GIF", "image/gif", new(StringComparer.OrdinalIgnoreCase) { ".gif" });

        if (b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b.Slice(8, 4).SequenceEqual("WEBP"u8))
            return ("WebP", "image/webp", new(StringComparer.OrdinalIgnoreCase) { ".webp" });

        if (b.Length >= 2 && b[..2].SequenceEqual("BM"u8))
            return ("BMP", "image/bmp", new(StringComparer.OrdinalIgnoreCase) { ".bmp" });

        if (b.Length >= 4 &&
            (b[..4].SequenceEqual(new byte[] { 0x49, 0x49, 0x2A, 0x00 }) ||
             b[..4].SequenceEqual(new byte[] { 0x4D, 0x4D, 0x00, 0x2A })))
            return ("TIFF", "image/tiff", new(StringComparer.OrdinalIgnoreCase) { ".tif", ".tiff", ".dng" });

        if (b.Length >= 12 && b.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var brand = System.Text.Encoding.ASCII.GetString(b.Slice(8, 4));
            if (brand is "avif" or "avis")
                return ("AVIF", "image/avif", new(StringComparer.OrdinalIgnoreCase) { ".avif" });

            if (brand is "heic" or "heix" or "hevc" or "hevx" or "heim" or "heis" or "hevm" or "hevs" or "mif1" or "msf1")
                return ("HEIF/HEIC", "image/heif", new(StringComparer.OrdinalIgnoreCase) { ".heic", ".heif", ".hif" });

            return ("ISO-BMFF", "application/octet-stream", new(StringComparer.OrdinalIgnoreCase));
        }

        return ("Unknown", "application/octet-stream", new(StringComparer.OrdinalIgnoreCase));
    }
}
