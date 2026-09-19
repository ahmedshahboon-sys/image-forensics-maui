using System.Buffers.Binary;
using System.Text;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;

namespace ImageForensics.Forensics.HiddenData;

public sealed class HiddenDataInspector : IHiddenDataInspector
{
    private const int BufferSize = 128 * 1024;
    private const int EntropyWindowMinimumBytes = 4096;
    private const double HighEntropyThreshold = 7.90;
    private const int PrintableMinimumLength = 20;
    private const int PrintableMaximumStoredLength = 160;
    private const int MaximumFindings = 200;
    private const int MaximumPrintableFindings = 32;
    private const int MaximumEntropyFindings = 16;

    private sealed record Signature(string Kind, byte[] Bytes);

    private static readonly Signature[] Signatures =
    {
        new("ZIP archive", new byte[] { 0x50,0x4B,0x03,0x04 }),
        new("ZIP empty archive", new byte[] { 0x50,0x4B,0x05,0x06 }),
        new("7-Zip archive", new byte[] { 0x37,0x7A,0xBC,0xAF,0x27,0x1C }),
        new("RAR archive", new byte[] { 0x52,0x61,0x72,0x21,0x1A,0x07 }),
        new("PDF document", "%PDF-"u8.ToArray()),
        new("Windows PE/MZ", new byte[] { 0x4D,0x5A }),
        new("ELF executable", new byte[] { 0x7F,(byte)'E',(byte)'L',(byte)'F' }),
        new("GZip stream", new byte[] { 0x1F,0x8B,0x08 }),
        new("SQLite database", "SQLite format 3\0"u8.ToArray()),
        new("OLE/CFB compound file", new byte[] { 0xD0,0xCF,0x11,0xE0,0xA1,0xB1,0x1A,0xE1 }),
        new("PNG image", new byte[] { 0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A }),
        new("JPEG image", new byte[] { 0xFF,0xD8,0xFF }),
        new("GIF image", "GIF8"u8.ToArray()),
        new("WebP RIFF", "RIFF"u8.ToArray())
    };

    public async Task<IReadOnlyList<HiddenDataFinding>> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var info = new FileInfo(filePath);
        if (!info.Exists)
            throw new FileNotFoundException("Input file not found.", filePath);
        if (info.Length <= 0)
            return Array.Empty<HiddenDataFinding>();

        var findings = new List<HiddenDataFinding>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var printable = new StringBuilder();
        long printableStart = 0;
        var printableFindings = 0;
        var entropyFindings = 0;

        var maxSignature = Signatures.Max(x => x.Bytes.Length);
        var carry = new byte[Math.Max(0, maxSignature - 1)];
        var carryCount = 0;
        var buffer = new byte[BufferSize];
        long fileOffset = 0;

        await using (var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var read = await stream.ReadAsync(
                    buffer.AsMemory(0, buffer.Length),
                    cancellationToken);

                if (read == 0)
                    break;

                var window = new byte[carryCount + read];
                if (carryCount > 0)
                    Buffer.BlockCopy(carry, 0, window, 0, carryCount);
                Buffer.BlockCopy(buffer, 0, window, carryCount, read);

                var baseOffset = fileOffset - carryCount;
                ScanKnownSignatures(window, baseOffset, findings, seen);
                ScanWebpSignatures(window, baseOffset, findings, seen);

                ScanPrintableStrings(
                    buffer.AsSpan(0, read),
                    fileOffset,
                    printable,
                    ref printableStart,
                    findings,
                    ref printableFindings);

                if (read >= EntropyWindowMinimumBytes &&
                    entropyFindings < MaximumEntropyFindings)
                {
                    var entropy = CalculateEntropy(buffer.AsSpan(0, read));
                    if (entropy >= HighEntropyThreshold)
                    {
                        findings.Add(new HiddenDataFinding(
                            fileOffset,
                            "High entropy region",
                            $"Window offset={fileOffset}; length={read}; Shannon entropy={entropy:F4} bits/byte.",
                            ForensicConfidence.Unknown,
                            "Compressed JPEG/PNG/WebP image data is normally high entropy. This is a locator for manual inspection, not evidence of steganography."));
                        entropyFindings++;
                    }
                }

                carryCount = Math.Min(carry.Length, window.Length);
                if (carryCount > 0)
                {
                    Buffer.BlockCopy(
                        window,
                        window.Length - carryCount,
                        carry,
                        0,
                        carryCount);
                }

                fileOffset += read;

                if (findings.Count >= MaximumFindings)
                    break;
            }
        }

        FlushPrintable(
            printable,
            printableStart,
            findings,
            ref printableFindings);

        var expectedEnd = await FindExpectedImageEndAsync(
            filePath,
            cancellationToken);

        if (expectedEnd is not null &&
            expectedEnd.Value >= 0 &&
            expectedEnd.Value < info.Length &&
            findings.Count < MaximumFindings)
        {
            findings.Add(new HiddenDataFinding(
                expectedEnd.Value,
                "Trailing payload region",
                $"{info.Length - expectedEnd.Value} byte(s) exist after the expected end of the primary image container.",
                ForensicConfidence.Confirmed,
                "Trailing bytes can be benign application data or an intentional append. Their presence alone does not identify their purpose, and the app never executes or auto-extracts them."));
        }

        if (findings.Count > MaximumFindings)
            return findings.Take(MaximumFindings).ToArray();

        return findings;
    }

    private static void ScanKnownSignatures(
        byte[] window,
        long baseOffset,
        List<HiddenDataFinding> findings,
        HashSet<string> seen)
    {
        foreach (var signature in Signatures)
        {
            var start = 0;

            while (start <= window.Length - signature.Bytes.Length)
            {
                var index = IndexOf(
                    window,
                    signature.Bytes,
                    start);

                if (index < 0)
                    break;

                var absolute = baseOffset + index;
                var key = $"{signature.Kind}:{absolute}";

                if (absolute >= 0 &&
                    seen.Add(key) &&
                    !IsExpectedPrimarySignature(
                        signature.Kind,
                        absolute,
                        window,
                        index))
                {
                    findings.Add(new HiddenDataFinding(
                        absolute,
                        signature.Kind,
                        $"Magic signature {Convert.ToHexString(signature.Bytes)} found at offset {absolute}.",
                        ForensicConfidence.Possible,
                        "A matching byte pattern can occur coincidentally inside compressed image data. Detection is read-only; embedded content is never executed or automatically extracted."));

                    if (findings.Count >= MaximumFindings)
                        return;
                }

                start = index + 1;
            }
        }
    }

    private static void ScanWebpSignatures(
        byte[] window,
        long baseOffset,
        List<HiddenDataFinding> findings,
        HashSet<string> seen)
    {
        var riff = "RIFF"u8.ToArray();
        var start = 0;

        while (start <= window.Length - 12)
        {
            var index = IndexOf(
                window,
                riff,
                start);

            if (index < 0 ||
                index + 12 > window.Length)
                break;

            if (window.AsSpan(
                    index + 8,
                    4)
                .SequenceEqual("WEBP"u8))
            {
                var absolute =
                    baseOffset + index;
                var key =
                    $"WebP image:{absolute}";

                if (absolute >= 0 &&
                    seen.Add(key) &&
                    absolute != 0)
                {
                    findings.Add(
                        new HiddenDataFinding(
                            absolute,
                            "WebP image",
                            $"RIFF/WEBP signature found at offset {absolute}.",
                            ForensicConfidence.Possible,
                            "A valid-looking WebP header inside compressed data can still be coincidental. Detection is read-only and nothing is extracted or executed."));

                    if (findings.Count >=
                        MaximumFindings)
                        return;
                }
            }

            start =
                index + 1;
        }
    }

    private static void ScanPrintableStrings(
        ReadOnlySpan<byte> data,
        long chunkOffset,
        StringBuilder current,
        ref long currentStart,
        List<HiddenDataFinding> findings,
        ref int printableFindings)
    {
        for (var i = 0; i < data.Length; i++)
        {
            var b = data[i];

            if (b is >= 32 and <= 126)
            {
                if (current.Length == 0)
                    currentStart = chunkOffset + i;

                if (current.Length < PrintableMaximumStoredLength)
                    current.Append((char)b);

                continue;
            }

            FlushPrintable(
                current,
                currentStart,
                findings,
                ref printableFindings);
        }
    }

    private static void FlushPrintable(
        StringBuilder current,
        long start,
        List<HiddenDataFinding> findings,
        ref int printableFindings)
    {
        if (current.Length >= PrintableMinimumLength &&
            printableFindings < MaximumPrintableFindings &&
            findings.Count < MaximumFindings)
        {
            var text = current.ToString();

            findings.Add(new HiddenDataFinding(
                start,
                "Printable string",
                $"ASCII text at offset {start}: {text}",
                ForensicConfidence.Unknown,
                "Readable strings commonly come from EXIF/XMP/comments/container metadata and are not inherently hidden or malicious."));

            printableFindings++;
        }

        current.Clear();
    }

    private static double CalculateEntropy(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
            return 0;

        Span<int> counts = stackalloc int[256];

        foreach (var b in data)
            counts[b]++;

        double entropy = 0;

        for (var i = 0; i < counts.Length; i++)
        {
            if (counts[i] == 0)
                continue;

            var p = (double)counts[i] / data.Length;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }

    private static async Task<long?> FindExpectedImageEndAsync(
        string filePath,
        CancellationToken ct)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var head = new byte[12];
        var read = await stream.ReadAsync(
            head.AsMemory(0, head.Length),
            ct);

        stream.Position = 0;

        if (read >= 3 &&
            head[0] == 0xFF &&
            head[1] == 0xD8 &&
            head[2] == 0xFF)
            return await FindJpegEndAsync(stream, ct);

        if (read >= 8 &&
            head.AsSpan(0, 8).SequenceEqual(
                new byte[] { 0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A }))
            return await FindPngEndAsync(stream, ct);

        if (read >= 12 &&
            head.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            head.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            var declared = 8L +
                BinaryPrimitives.ReadUInt32LittleEndian(
                    head.AsSpan(4, 4));

            return declared <= stream.Length
                ? declared
                : null;
        }

        return null;
    }

    private static async Task<long?> FindJpegEndAsync(
        Stream stream,
        CancellationToken ct)
    {
        stream.Position = 2;
        var lengthBytes = new byte[2];

        while (stream.Position < stream.Length)
        {
            ct.ThrowIfCancellationRequested();

            var prefix = stream.ReadByte();
            if (prefix < 0)
                return null;

            if (prefix != 0xFF)
                continue;

            int marker;
            do
            {
                marker = stream.ReadByte();
            }
            while (marker == 0xFF);

            if (marker < 0)
                return null;

            if (marker == 0x00)
                continue;

            if (marker == 0xD9)
                return stream.Position;

            if (marker is >= 0xD0 and <= 0xD7 ||
                marker is 0x01 or 0xD8)
                continue;

            if (!await TryReadExactlyAsync(
                    stream,
                    lengthBytes,
                    ct))
                return null;

            var length =
                BinaryPrimitives.ReadUInt16BigEndian(
                    lengthBytes);

            if (length < 2 ||
                stream.Position + length - 2 >
                stream.Length)
                return null;

            var payloadLength =
                length - 2;

            if (marker != 0xDA)
            {
                stream.Position +=
                    payloadLength;
                continue;
            }

            stream.Position +=
                payloadLength;

            var previous =
                -1;

            while (stream.Position <
                   stream.Length)
            {
                ct.ThrowIfCancellationRequested();

                var current =
                    stream.ReadByte();

                if (current < 0)
                    return null;

                if (previous != 0xFF)
                {
                    previous = current;
                    continue;
                }

                if (current == 0x00)
                {
                    previous = -1;
                    continue;
                }

                if (current is >= 0xD0 and <= 0xD7)
                {
                    previous = -1;
                    continue;
                }

                if (current == 0xD9)
                    return stream.Position;

                if (current == 0xFF)
                {
                    previous = 0xFF;
                    continue;
                }

                if (current == 0xDA)
                {
                    if (!await TryReadExactlyAsync(
                            stream,
                            lengthBytes,
                            ct))
                        return null;

                    var scanLength =
                        BinaryPrimitives.ReadUInt16BigEndian(
                            lengthBytes);

                    if (scanLength < 2 ||
                        stream.Position + scanLength - 2 >
                        stream.Length)
                        return null;

                    stream.Position +=
                        scanLength - 2;
                    previous = -1;
                    continue;
                }

                previous = -1;
            }

            return null;
        }

        return null;
    }

    private static async Task<long?> FindPngEndAsync(
        Stream stream,
        CancellationToken ct)
    {
        stream.Position = 8;
        var header = new byte[8];

        while (stream.Position + 12 <= stream.Length)
        {
            ct.ThrowIfCancellationRequested();

            if (!await TryReadExactlyAsync(
                    stream,
                    header,
                    ct))
                return null;

            var length =
                BinaryPrimitives.ReadUInt32BigEndian(
                    header.AsSpan(0, 4));

            if (length > int.MaxValue ||
                stream.Position + length + 4 > stream.Length)
                return null;

            var type = Encoding.ASCII.GetString(
                header,
                4,
                4);

            stream.Position += length + 4;

            if (type == "IEND")
                return stream.Position;
        }

        return null;
    }

    private static async Task<bool> TryReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken ct)
    {
        var total = 0;

        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(total),
                ct);

            if (read == 0)
                return false;

            total += read;
        }

        return true;
    }

    private static bool IsExpectedPrimarySignature(
        string kind,
        long absolute,
        byte[] window,
        int index)
    {
        if (absolute != 0)
            return false;

        return kind switch
        {
            "PNG image" or
            "JPEG image" or
            "GIF image" or
            "WebP RIFF" or
            "PDF document" or
            "ZIP archive" or
            "ZIP empty archive" or
            "7-Zip archive" or
            "RAR archive" or
            "Windows PE/MZ" or
            "ELF executable" or
            "GZip stream" or
            "SQLite database" or
            "OLE/CFB compound file" => true,
            _ => false
        };
    }

    private static int IndexOf(
        byte[] haystack,
        byte[] needle,
        int start)
    {
        for (var i = start;
             i <= haystack.Length - needle.Length;
             i++)
        {
            var match = true;

            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] == needle[j])
                    continue;

                match = false;
                break;
            }

            if (match)
                return i;
        }

        return -1;
    }
}
