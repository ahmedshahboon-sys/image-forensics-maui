using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;

namespace ImageForensics.Forensics.HiddenData;

public sealed class HiddenDataInspector : IHiddenDataInspector
{
    private sealed record Signature(string Kind, byte[] Bytes);

    private static readonly Signature[] Signatures =
    {
        new("ZIP archive", new byte[] { 0x50, 0x4B, 0x03, 0x04 }),
        new("7-Zip archive", new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }),
        new("RAR archive", new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07 }),
        new("PDF document", "%PDF-"u8.ToArray()),
        new("Windows PE/MZ", new byte[] { 0x4D, 0x5A }),
        new("GZip stream", new byte[] { 0x1F, 0x8B, 0x08 })
    };

    public async Task<IReadOnlyList<HiddenDataFinding>> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(filePath);
        if (!info.Exists) throw new FileNotFoundException("Input file not found.", filePath);
        if (info.Length <= 0) return Array.Empty<HiddenDataFinding>();

        var findings = new List<HiddenDataFinding>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);

        const int bufferSize = 128 * 1024;
        var maxSignature = Signatures.Max(x => x.Bytes.Length);
        var carry = new byte[maxSignature - 1];
        var carryCount = 0;
        var buffer = new byte[bufferSize];
        long fileOffset = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;

            var window = new byte[carryCount + read];
            if (carryCount > 0) Buffer.BlockCopy(carry, 0, window, 0, carryCount);
            Buffer.BlockCopy(buffer, 0, window, carryCount, read);

            var baseOffset = fileOffset - carryCount;
            foreach (var signature in Signatures)
            {
                var start = 0;
                while (start <= window.Length - signature.Bytes.Length)
                {
                    var index = IndexOf(window, signature.Bytes, start);
                    if (index < 0) break;

                    var absolute = baseOffset + index;
                    var key = $"{signature.Kind}:{absolute}";
                    if (seen.Add(key) && !IsExpectedPrimarySignature(signature.Kind, absolute, window, index))
                    {
                        findings.Add(new HiddenDataFinding(
                            absolute,
                            signature.Kind,
                            $"Magic signature {Convert.ToHexString(signature.Bytes)} found at offset {absolute}.",
                            ForensicConfidence.Possible,
                            "A matching byte pattern can occur coincidentally inside compressed image data. The payload is never executed or auto-extracted."));
                    }

                    start = index + 1;
                    if (findings.Count >= 200) return findings;
                }
            }

            carryCount = Math.Min(carry.Length, window.Length);
            if (carryCount > 0)
                Buffer.BlockCopy(window, window.Length - carryCount, carry, 0, carryCount);

            fileOffset += read;
        }

        return findings;
    }

    private static bool IsExpectedPrimarySignature(string kind, long absolute, byte[] window, int index)
    {
        if (absolute != 0) return false;
        return kind switch
        {
            "PDF document" or "ZIP archive" or "7-Zip archive" or "RAR archive" or "Windows PE/MZ" or "GZip stream" => true,
            _ => false
        };
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        for (var i = start; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] == needle[j]) continue;
                match = false;
                break;
            }
            if (match) return i;
        }
        return -1;
    }
}
