using System.Buffers.Binary;
using System.Text;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;

namespace ImageForensics.Forensics.Containers;

public sealed class SafeContainerInspector : IContainerInspector
{
    private const int MaxSegmentBytes = 128 * 1024 * 1024;
    private const int MaxGifCollectedMetadataBytes = 16 * 1024;

    public async Task<ContainerInspectionResult> InspectAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);

        var head = new byte[12];
        var read = await stream.ReadAsync(head, cancellationToken);
        stream.Position = 0;

        if (read >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            return await InspectJpegAsync(stream, cancellationToken);

        if (read >= 8 && head.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A }))
            return await InspectPngAsync(stream, cancellationToken);

        if (read >= 12 && head.AsSpan(0, 4).SequenceEqual("RIFF"u8) && head.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return await InspectWebPAsync(stream, cancellationToken);

        if (read >= 6 && (head.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || head.AsSpan(0, 6).SequenceEqual("GIF89a"u8)))
            return await InspectGifAsync(stream, cancellationToken);

        return new ContainerInspectionResult(
            "Unknown",
            Array.Empty<ContainerSegment>(),
            0,
            new[] { "No supported JPEG/PNG/WebP/GIF container signature was detected." });
    }

    private static async Task<ContainerInspectionResult> InspectJpegAsync(Stream stream, CancellationToken ct)
    {
        var segments = new List<ContainerSegment>();
        var warnings = new List<string>();
        var first = new byte[2];
        await ReadExactlyAsync(stream, first, ct);
        segments.Add(new ContainerSegment(0, 2, "SOI", "Start Of Image"));

        while (stream.Position < stream.Length)
        {
            ct.ThrowIfCancellationRequested();
            var markerOffset = stream.Position;
            var prefix = stream.ReadByte();
            if (prefix < 0) break;
            if (prefix != 0xFF)
            {
                warnings.Add($"Unexpected byte outside segment at offset {markerOffset}.");
                continue;
            }

            int marker;
            do { marker = stream.ReadByte(); } while (marker == 0xFF);
            if (marker < 0) break;
            if (marker == 0x00) continue;

            if (marker == 0xD9)
            {
                segments.Add(new ContainerSegment(markerOffset, 2, "EOI", "End Of Image"));
                var trailing = stream.Length - stream.Position;
                AddTrailingWarning(stream, trailing, "JPEG EOI", warnings);
                return new ContainerInspectionResult("JPEG", segments, trailing, warnings);
            }

            if (marker is >= 0xD0 and <= 0xD7 || marker == 0x01 || marker == 0xD8)
            {
                segments.Add(new ContainerSegment(markerOffset, 2, MarkerName(marker), "Standalone JPEG marker"));
                continue;
            }

            var lenBytes = new byte[2];
            if (!await TryReadExactlyAsync(stream, lenBytes, ct))
            {
                warnings.Add($"Truncated JPEG segment at offset {markerOffset}.");
                break;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(lenBytes);
            if (length < 2)
            {
                warnings.Add($"Invalid JPEG segment length {length} at offset {markerOffset}.");
                break;
            }

            var payloadLength = length - 2;
            var type = MarkerName(marker);
            segments.Add(new ContainerSegment(markerOffset, length + 2L, type, MarkerDescription(marker)));

            if (marker == 0xDA)
            {
                if (payloadLength > 0)
                    await SkipExactlyAsync(stream, payloadLength, ct);

                var entropyStart = stream.Position;
                long eoiOffset = -1;
                var previous = -1;

                while (stream.Position < stream.Length)
                {
                    ct.ThrowIfCancellationRequested();
                    var b = stream.ReadByte();
                    if (b < 0) break;

                    if (previous == 0xFF && b == 0xD9)
                    {
                        eoiOffset = stream.Position - 2;
                        break;
                    }

                    previous = b;
                }

                if (eoiOffset >= 0)
                {
                    var entropyLength = Math.Max(0, eoiOffset - entropyStart);
                    segments.Add(new ContainerSegment(entropyStart, entropyLength, "SCAN-DATA", "Entropy-coded image data"));
                    segments.Add(new ContainerSegment(eoiOffset, 2, "EOI", "End Of Image"));
                    var trailing = stream.Length - stream.Position;
                    AddTrailingWarning(stream, trailing, "JPEG EOI", warnings);
                    return new ContainerInspectionResult("JPEG", segments, trailing, warnings);
                }

                warnings.Add("JPEG scan reached EOF without an EOI marker.");
                return new ContainerInspectionResult("JPEG", segments, 0, warnings);
            }

            if (payloadLength > MaxSegmentBytes)
            {
                warnings.Add($"JPEG segment {type} exceeds safe inspection limit.");
                break;
            }

            await SkipExactlyAsync(stream, payloadLength, ct);
        }

        return new ContainerInspectionResult("JPEG", segments, 0, warnings);
    }

    private static async Task<ContainerInspectionResult> InspectPngAsync(Stream stream, CancellationToken ct)
    {
        var segments = new List<ContainerSegment>
        {
            new(0, 8, "PNG-SIGNATURE", "PNG file signature")
        };
        var warnings = new List<string>();
        stream.Position = 8;

        while (stream.Position < stream.Length)
        {
            ct.ThrowIfCancellationRequested();
            var offset = stream.Position;
            var header = new byte[8];
            if (!await TryReadExactlyAsync(stream, header, ct))
            {
                warnings.Add($"Truncated PNG chunk header at offset {offset}.");
                break;
            }

            var length = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
            var type = Encoding.ASCII.GetString(header, 4, 4);

            if (length > MaxSegmentBytes || length > stream.Length - stream.Position - 4)
            {
                warnings.Add($"Invalid or oversized PNG chunk {type} at offset {offset}.");
                break;
            }

            await SkipExactlyAsync(stream, checked((int)length), ct);

            var crc = new byte[4];
            if (!await TryReadExactlyAsync(stream, crc, ct))
            {
                warnings.Add($"PNG chunk {type} is missing CRC.");
                break;
            }

            var known = IsKnownPngChunk(type);
            var critical = type.Length == 4 && char.IsUpper(type[0]);
            var suspicious = !IsValidFourCc(type) || (critical && !known);

            if (suspicious)
                warnings.Add($"Unusual PNG chunk {type} at offset {offset}.");

            segments.Add(new ContainerSegment(offset, 12L + length, type, PngDescription(type), suspicious));

            if (type == "IEND")
            {
                var trailing = stream.Length - stream.Position;
                AddTrailingWarning(stream, trailing, "PNG IEND", warnings);
                return new ContainerInspectionResult("PNG", segments, trailing, warnings);
            }
        }

        return new ContainerInspectionResult("PNG", segments, 0, warnings);
    }

    private static async Task<ContainerInspectionResult> InspectWebPAsync(Stream stream, CancellationToken ct)
    {
        var segments = new List<ContainerSegment>();
        var warnings = new List<string>();
        var header = new byte[12];
        await ReadExactlyAsync(stream, header, ct);

        var riffSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4));
        var declaredEnd = 8L + riffSize;
        if (riffSize < 4)
            warnings.Add("WebP RIFF size is smaller than the required WEBP form type.");
        if (declaredEnd > stream.Length)
            warnings.Add($"WebP RIFF declares {declaredEnd} bytes but file has {stream.Length}.");

        segments.Add(new ContainerSegment(0, 12, "RIFF/WEBP", $"RIFF WebP header; declared size={riffSize + 8L}"));

        var parseEnd = Math.Min(stream.Length, Math.Max(12, declaredEnd));

        while (stream.Position + 8 <= parseEnd)
        {
            ct.ThrowIfCancellationRequested();
            var offset = stream.Position;
            var chunkHeader = new byte[8];
            await ReadExactlyAsync(stream, chunkHeader, ct);

            var type = Encoding.ASCII.GetString(chunkHeader, 0, 4);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader.AsSpan(4, 4));
            var padded = (long)length + (length & 1);

            if (length > MaxSegmentBytes || stream.Position + padded > parseEnd)
            {
                warnings.Add($"Invalid or oversized WebP chunk {type} at offset {offset}.");
                break;
            }

            segments.Add(new ContainerSegment(
                offset,
                8L + padded,
                type,
                WebPDescription(type),
                !IsValidFourCc(type)));

            await SkipExactlyAsync(stream, checked((int)padded), ct);
        }

        var trailing = Math.Max(0, stream.Length - Math.Min(stream.Length, declaredEnd));
        if (trailing > 0)
            AddTrailingWarningAt(stream, Math.Min(stream.Length, declaredEnd), trailing, "WebP RIFF boundary", warnings);

        if (declaredEnd < 12)
            warnings.Add("WebP RIFF boundary is invalid.");

        return new ContainerInspectionResult("WebP", segments, trailing, warnings);
    }

    private static async Task<ContainerInspectionResult> InspectGifAsync(Stream stream, CancellationToken ct)
    {
        var segments = new List<ContainerSegment>();
        var warnings = new List<string>();

        var header = new byte[13];
        await ReadExactlyAsync(stream, header, ct);
        var version = Encoding.ASCII.GetString(header, 0, 6);
        segments.Add(new ContainerSegment(0, 6, "GIF-HEADER", version));
        segments.Add(new ContainerSegment(6, 7, "LSD", "Logical Screen Descriptor"));

        var packed = header[10];
        if ((packed & 0x80) != 0)
        {
            var entries = 1 << ((packed & 0x07) + 1);
            var tableBytes = checked(entries * 3);
            if (stream.Position + tableBytes > stream.Length)
            {
                warnings.Add("GIF global color table is truncated.");
                return new ContainerInspectionResult("GIF", segments, 0, warnings);
            }
            segments.Add(new ContainerSegment(stream.Position, tableBytes, "GCT", $"Global Color Table ({entries} entries)"));
            await SkipExactlyAsync(stream, tableBytes, ct);
        }

        var frame = 0;
        var loopCount = (int?)null;

        while (stream.Position < stream.Length)
        {
            ct.ThrowIfCancellationRequested();
            var offset = stream.Position;
            var introducer = stream.ReadByte();
            if (introducer < 0) break;

            if (introducer == 0x3B)
            {
                segments.Add(new ContainerSegment(offset, 1, "TRAILER", "GIF trailer"));
                var trailing = stream.Length - stream.Position;
                AddTrailingWarning(stream, trailing, "GIF trailer", warnings);
                if (loopCount is not null)
                    warnings.Add(loopCount == 0 ? "GIF loop metadata: infinite loop." : $"GIF loop metadata: {loopCount} repetition(s).");
                return new ContainerInspectionResult("GIF", segments, trailing, warnings);
            }

            if (introducer == 0x2C)
            {
                var descriptor = new byte[9];
                if (!await TryReadExactlyAsync(stream, descriptor, ct))
                {
                    warnings.Add($"Truncated GIF image descriptor at offset {offset}.");
                    break;
                }

                var localPacked = descriptor[8];
                if ((localPacked & 0x80) != 0)
                {
                    var entries = 1 << ((localPacked & 0x07) + 1);
                    var tableBytes = checked(entries * 3);
                    if (stream.Position + tableBytes > stream.Length)
                    {
                        warnings.Add($"Truncated GIF local color table for frame {frame + 1}.");
                        break;
                    }
                    await SkipExactlyAsync(stream, tableBytes, ct);
                }

                if (stream.ReadByte() < 0)
                {
                    warnings.Add($"Missing GIF LZW minimum code size for frame {frame + 1}.");
                    break;
                }

                var block = await ReadGifSubBlocksAsync(stream, ct, collect: false);
                frame++;
                segments.Add(new ContainerSegment(
                    offset,
                    stream.Position - offset,
                    "FRAME",
                    $"GIF image frame {frame}; data bytes={block.PayloadBytes}"));
                continue;
            }

            if (introducer == 0x21)
            {
                var label = stream.ReadByte();
                if (label < 0)
                {
                    warnings.Add($"Truncated GIF extension at offset {offset}.");
                    break;
                }

                var block = await ReadGifSubBlocksAsync(stream, ct, collect: true);
                var type = label switch
                {
                    0xF9 => "GRAPHIC-CONTROL",
                    0xFE => "COMMENT",
                    0xFF => "APPLICATION",
                    0x01 => "PLAIN-TEXT",
                    _ => $"EXT-{label:X2}"
                };

                var description = type switch
                {
                    "GRAPHIC-CONTROL" => "Graphic Control Extension",
                    "COMMENT" => "Comment Extension",
                    "APPLICATION" => "Application Extension",
                    "PLAIN-TEXT" => "Plain Text Extension",
                    _ => "GIF extension"
                };

                if (label == 0xFF && block.Collected.Length >= 14)
                {
                    var app = Encoding.ASCII.GetString(block.Collected, 0, Math.Min(11, block.Collected.Length));
                    if ((app.StartsWith("NETSCAPE2.0", StringComparison.Ordinal) ||
                         app.StartsWith("ANIMEXTS1.0", StringComparison.Ordinal)) &&
                        block.Collected.Length >= 14 &&
                        block.Collected[11] == 1)
                    {
                        loopCount = block.Collected[12] | (block.Collected[13] << 8);
                        description += loopCount == 0 ? "; loop=infinite" : $"; loop={loopCount}";
                    }
                }

                segments.Add(new ContainerSegment(offset, stream.Position - offset, type, description, label is not (0xF9 or 0xFE or 0xFF or 0x01)));
                continue;
            }

            warnings.Add($"Unknown GIF block introducer 0x{introducer:X2} at offset {offset}.");
            break;
        }

        warnings.Add("GIF ended without a trailer.");
        return new ContainerInspectionResult("GIF", segments, 0, warnings);
    }

    private static async Task<GifSubBlockResult> ReadGifSubBlocksAsync(Stream stream, CancellationToken ct, bool collect)
    {
        using var collected = collect ? new MemoryStream() : null;
        long payloadBytes = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var size = stream.ReadByte();
            if (size < 0) throw new EndOfStreamException("GIF sub-block stream is truncated.");
            if (size == 0) break;

            payloadBytes += size;
            if (payloadBytes > MaxSegmentBytes)
                throw new InvalidDataException("GIF sub-block data exceeds safe inspection limit.");

            var buffer = new byte[size];
            if (!await TryReadExactlyAsync(stream, buffer, ct))
                throw new EndOfStreamException("GIF sub-block payload is truncated.");

            if (collected is not null && collected.Length < MaxGifCollectedMetadataBytes)
            {
                var keep = (int)Math.Min(buffer.Length, MaxGifCollectedMetadataBytes - collected.Length);
                if (keep > 0)
                    collected.Write(buffer, 0, keep);
            }
        }

        return new GifSubBlockResult(payloadBytes, collected?.ToArray() ?? Array.Empty<byte>());
    }

    private static void AddTrailingWarning(Stream stream, long trailing, string boundary, List<string> warnings)
        => AddTrailingWarningAt(stream, stream.Position, trailing, boundary, warnings);

    private static void AddTrailingWarningAt(Stream stream, long offset, long trailing, string boundary, List<string> warnings)
    {
        if (trailing <= 0) return;
        warnings.Add($"{trailing} trailing byte(s) found after {boundary}.");

        var old = stream.Position;
        try
        {
            stream.Position = Math.Clamp(offset, 0, stream.Length);
            Span<byte> signature = stackalloc byte[12];
            var read = stream.Read(signature);
            var kind = IdentifySignature(signature[..read]);
            if (kind is not null)
                warnings.Add($"Trailing data begins with a known {kind} signature (read-only detection; nothing was executed).");
        }
        finally
        {
            stream.Position = old;
        }
    }

    private static string? IdentifySignature(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "JPEG";
        if (b.Length >= 8 && b[..8].SequenceEqual(new byte[] { 0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A })) return "PNG";
        if (b.Length >= 4 && b[..4].SequenceEqual(new byte[] { 0x50,0x4B,0x03,0x04 })) return "ZIP";
        if (b.Length >= 4 && b[..4].SequenceEqual("%PDF"u8)) return "PDF";
        if (b.Length >= 6 && (b[..6].SequenceEqual("GIF87a"u8) || b[..6].SequenceEqual("GIF89a"u8))) return "GIF";
        return null;
    }

    private static bool IsValidFourCc(string type)
        => type.Length == 4 && type.All(ch => ch is >= ' ' and <= '~');

    private static bool IsKnownPngChunk(string type) => type is
        "IHDR" or "PLTE" or "IDAT" or "IEND" or "tEXt" or "zTXt" or "iTXt" or
        "eXIf" or "iCCP" or "pHYs" or "gAMA" or "cHRM" or "sRGB" or "bKGD" or
        "tIME" or "sBIT" or "hIST" or "tRNS" or "sPLT";

    private static string MarkerName(int marker) => marker switch
    {
        0xD8 => "SOI",
        0xD9 => "EOI",
        0xDA => "SOS",
        0xDB => "DQT",
        0xC4 => "DHT",
        0xFE => "COM",
        >= 0xE0 and <= 0xEF => $"APP{marker - 0xE0}",
        0xC0 => "SOF0",
        0xC1 => "SOF1",
        0xC2 => "SOF2",
        >= 0xD0 and <= 0xD7 => $"RST{marker - 0xD0}",
        _ => $"FF{marker:X2}"
    };

    private static string MarkerDescription(int marker) => marker switch
    {
        0xDA => "Start Of Scan",
        0xDB => "Define Quantization Table",
        0xC4 => "Define Huffman Table",
        0xFE => "Comment",
        >= 0xE0 and <= 0xEF => "Application-specific segment",
        0xC0 or 0xC1 or 0xC2 => "Start Of Frame",
        _ => "JPEG segment"
    };

    private static string PngDescription(string type) => type switch
    {
        "IHDR" => "Image header",
        "IDAT" => "Image data",
        "IEND" => "Image end",
        "PLTE" => "Palette",
        "pHYs" => "Physical pixel dimensions / pixel density",
        "tEXt" => "Text metadata",
        "zTXt" => "Compressed text metadata",
        "iTXt" => "International text metadata",
        "eXIf" => "EXIF metadata",
        "iCCP" => "ICC color profile",
        _ => "PNG chunk"
    };

    private static string WebPDescription(string type) => type switch
    {
        "VP8 " => "Lossy VP8 image payload",
        "VP8L" => "Lossless VP8L image payload",
        "VP8X" => "Extended WebP header",
        "ALPH" => "Alpha data",
        "ANIM" => "Animation parameters",
        "ANMF" => "Animation frame",
        "EXIF" => "EXIF metadata",
        "XMP " => "XMP metadata",
        "ICCP" => "ICC color profile",
        _ => "WebP RIFF chunk"
    };

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        if (!await TryReadExactlyAsync(stream, buffer, ct))
            throw new EndOfStreamException();
    }

    private static async Task<bool> TryReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), ct);
            if (read == 0) return false;
            total += read;
        }
        return true;
    }

    private static async Task SkipExactlyAsync(Stream stream, int count, CancellationToken ct)
    {
        if (count <= 0) return;

        if (stream.CanSeek)
        {
            if (stream.Position + count > stream.Length)
                throw new EndOfStreamException();

            stream.Seek(count, SeekOrigin.Current);
            return;
        }

        var buffer = new byte[Math.Min(64 * 1024, count)];
        var remaining = count;

        while (remaining > 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), ct);
            if (read == 0)
                throw new EndOfStreamException();

            remaining -= read;
        }
    }

    private sealed record GifSubBlockResult(long PayloadBytes, byte[] Collected);
}
