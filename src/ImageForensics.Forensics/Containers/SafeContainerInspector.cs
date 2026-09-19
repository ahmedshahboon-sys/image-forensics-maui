using System.Buffers.Binary;
using System.Text;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;

namespace ImageForensics.Forensics.Containers;

public sealed class SafeContainerInspector : IContainerInspector
{
    private const int MaxSegmentBytes = 128 * 1024 * 1024;

    public async Task<ContainerInspectionResult> InspectAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var head = new byte[12];
        var read = await stream.ReadAsync(head, cancellationToken);
        stream.Position = 0;

        if (read >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            return await InspectJpegAsync(stream, cancellationToken);

        if (read >= 8 && head.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return await InspectPngAsync(stream, cancellationToken);

        return new ContainerInspectionResult("Unknown", Array.Empty<ContainerSegment>(), 0,
            new[] { "No supported JPEG/PNG container signature was detected." });
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
                if (trailing > 0)
                    warnings.Add($"{trailing} trailing byte(s) found after JPEG EOI.");
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
                    if (trailing > 0)
                        warnings.Add($"{trailing} trailing byte(s) found after JPEG EOI.");
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

            var suspicious = type.Any(ch => ch < 'A' || ch > 'z');
            segments.Add(new ContainerSegment(offset, 12L + length, type, PngDescription(type), suspicious));

            if (type == "IEND")
            {
                var trailing = stream.Length - stream.Position;
                if (trailing > 0)
                    warnings.Add($"{trailing} trailing byte(s) found after PNG IEND.");
                return new ContainerInspectionResult("PNG", segments, trailing, warnings);
            }
        }

        return new ContainerInspectionResult("PNG", segments, 0, warnings);
    }

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
        "tEXt" => "Text metadata",
        "zTXt" => "Compressed text metadata",
        "iTXt" => "International text metadata",
        "eXIf" => "EXIF metadata",
        "iCCP" => "ICC color profile",
        _ => "PNG chunk"
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
            if (stream.Position + count > stream.Length) throw new EndOfStreamException();
            stream.Seek(count, SeekOrigin.Current);
            return;
        }

        var buffer = new byte[Math.Min(64 * 1024, count)];
        var remaining = count;
        while (remaining > 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), ct);
            if (read == 0) throw new EndOfStreamException();
            remaining -= read;
        }
    }
}
