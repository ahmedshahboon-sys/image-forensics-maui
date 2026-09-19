using System.Buffers.Binary;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using SkiaSharp;

namespace ImageForensics.Imaging;

public sealed class ImageTechnicalInspector : IImageTechnicalInspector
{
    public Task<ImageTechnicalInfo> InspectAsync(string filePath, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var codec = SKCodec.Create(filePath) ?? throw new InvalidDataException("Unsupported or corrupt image.");
            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0)
                throw new InvalidDataException("Image dimensions are invalid.");

            var aux = ReadAuxiliary(filePath, codec.EncodedFormat.ToString(), cancellationToken);

            return new ImageTechnicalInfo
            {
                Width = info.Width,
                Height = info.Height,
                AspectRatio = (double)info.Width / info.Height,
                EncodedFormat = codec.EncodedFormat.ToString(),
                ColorType = info.ColorType.ToString(),
                AlphaType = info.AlphaType.ToString(),
                HasAlpha = info.AlphaType != SKAlphaType.Opaque,
                BitsPerPixel = info.BytesPerPixel * 8,
                FrameCount = Math.Max(1, codec.FrameCount),
                Orientation = codec.EncodedOrigin.ToString(),
                EncodedBitDepth = aux.BitDepth,
                HorizontalDpi = aux.HorizontalDpi,
                VerticalDpi = aux.VerticalDpi,
                PaletteEntries = aux.PaletteEntries,
                PaletteInfo = aux.PaletteInfo,
                ColorSpace = info.ColorSpace?.ToString(),
                Compression = CompressionName(codec.EncodedFormat.ToString())
            };
        }, cancellationToken);

    private static string CompressionName(string format) => format.ToUpperInvariant() switch
    {
        "JPEG" => "JPEG DCT",
        "PNG" => "Deflate/zlib",
        "GIF" => "LZW",
        "WEBP" => "VP8 / VP8L / VP8X",
        "BMP" => "Bitmap header-defined",
        "ICO" => "ICO embedded image",
        "WBMP" => "Wireless bitmap",
        "HEIF" => "HEIF/HEIC codec in ISO-BMFF",
        _ => $"{format} codec/container"
    };

    private static AuxInfo ReadAuxiliary(string path, string encodedFormat, CancellationToken ct)
    {
        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> head = stackalloc byte[32];
            var read = fs.Read(head);
            fs.Position = 0;
            ct.ThrowIfCancellationRequested();

            if (read >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
                return ReadJpegAux(fs, ct);

            if (read >= 8 && head[..8].SequenceEqual(new byte[] { 0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A }))
                return ReadPngAux(fs, ct);

            if (read >= 6 && (head[..6].SequenceEqual("GIF87a"u8) || head[..6].SequenceEqual("GIF89a"u8)))
                return ReadGifAux(fs);

            if (read >= 2 && head[..2].SequenceEqual("BM"u8))
                return ReadBmpAux(fs);

            if (encodedFormat.Equals("WEBP", StringComparison.OrdinalIgnoreCase))
                return new AuxInfo(8, null, null, null, null);
        }
        catch (IOException) { }
        catch (EndOfStreamException) { }

        return new AuxInfo(null, null, null, null, null);
    }

    private static AuxInfo ReadJpegAux(Stream fs, CancellationToken ct)
    {
        fs.Position = 2;
        double? xDpi = null, yDpi = null;
        int? bitDepth = null;

        while (fs.Position + 4 <= fs.Length)
        {
            ct.ThrowIfCancellationRequested();
            var prefix = fs.ReadByte();
            if (prefix < 0) break;
            if (prefix != 0xFF) continue;

            int marker;
            do { marker = fs.ReadByte(); } while (marker == 0xFF);
            if (marker < 0 || marker == 0xD9 || marker == 0xDA) break;
            if (marker is >= 0xD0 and <= 0xD7 || marker == 0x01) continue;

            Span<byte> lenBytes = stackalloc byte[2];
            if (fs.Read(lenBytes) != 2) break;
            var len = BinaryPrimitives.ReadUInt16BigEndian(lenBytes);
            if (len < 2 || fs.Position + len - 2 > fs.Length) break;

            var payloadLen = len - 2;
            if (marker == 0xE0 && payloadLen >= 12)
            {
                var payload = new byte[Math.Min(payloadLen, 16)];
                if (fs.Read(payload, 0, payload.Length) != payload.Length) break;
                if (payload.AsSpan(0, 5).SequenceEqual(new byte[] { (byte)'J',(byte)'F',(byte)'I',(byte)'F',0 }))
                {
                    var units = payload[7];
                    var xd = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(8, 2));
                    var yd = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(10, 2));
                    if (units == 1) { xDpi = xd; yDpi = yd; }
                    if (units == 2) { xDpi = xd * 2.54; yDpi = yd * 2.54; }
                }
                fs.Position += payloadLen - payload.Length;
                continue;
            }

            if (marker is 0xC0 or 0xC1 or 0xC2 && payloadLen >= 1)
            {
                var precision = fs.ReadByte();
                if (precision >= 0) bitDepth = precision;
                fs.Position += payloadLen - 1;
                continue;
            }

            fs.Position += payloadLen;
        }

        return new AuxInfo(bitDepth, xDpi, yDpi, null, null);
    }

    private static AuxInfo ReadPngAux(Stream fs, CancellationToken ct)
    {
        fs.Position = 8;
        int? bitDepth = null;
        double? xDpi = null, yDpi = null;
        int? paletteEntries = null;

        while (fs.Position + 12 <= fs.Length)
        {
            ct.ThrowIfCancellationRequested();
            Span<byte> header = stackalloc byte[8];
            if (fs.Read(header) != 8) break;
            var len = BinaryPrimitives.ReadUInt32BigEndian(header[..4]);
            if (len > int.MaxValue || fs.Position + len + 4 > fs.Length) break;
            var type = System.Text.Encoding.ASCII.GetString(header[4..8]);

            if (type == "IHDR" && len >= 13)
            {
                var data = new byte[13];
                if (fs.Read(data, 0, 13) != 13) break;
                bitDepth = data[8];
                fs.Position += (long)len - 13;
            }
            else if (type == "pHYs" && len == 9)
            {
                Span<byte> data = stackalloc byte[9];
                if (fs.Read(data) != 9) break;
                if (data[8] == 1)
                {
                    var xPpm = BinaryPrimitives.ReadUInt32BigEndian(data[..4]);
                    var yPpm = BinaryPrimitives.ReadUInt32BigEndian(data[4..8]);
                    xDpi = xPpm * 0.0254;
                    yDpi = yPpm * 0.0254;
                }
            }
            else if (type == "PLTE")
            {
                paletteEntries = (int)(len / 3);
                fs.Position += len;
            }
            else
            {
                fs.Position += len;
            }

            fs.Position += 4;
            if (type == "IEND") break;
        }

        var paletteInfo = paletteEntries is null ? null : $"PNG palette with {paletteEntries} entr{(paletteEntries == 1 ? "y" : "ies")}";
        return new AuxInfo(bitDepth, xDpi, yDpi, paletteEntries, paletteInfo);
    }

    private static AuxInfo ReadGifAux(Stream fs)
    {
        Span<byte> header = stackalloc byte[13];
        if (fs.Read(header) != 13)
            return new AuxInfo(null, null, null, null, null);

        var packed = header[10];
        if ((packed & 0x80) == 0)
            return new AuxInfo(8, null, null, null, "No global color table");

        var entries = 1 << ((packed & 0x07) + 1);
        return new AuxInfo(8, null, null, entries, $"GIF global color table with {entries} entries");
    }

    private static AuxInfo ReadBmpAux(Stream fs)
    {
        Span<byte> header = stackalloc byte[46];
        if (fs.Read(header) < 46)
            return new AuxInfo(null, null, null, null, null);

        var bitCount = BinaryPrimitives.ReadUInt16LittleEndian(header[28..30]);
        var xPpm = BinaryPrimitives.ReadInt32LittleEndian(header[38..42]);
        var yPpm = BinaryPrimitives.ReadInt32LittleEndian(header[42..46]);
        var xDpi = xPpm > 0 ? xPpm * 0.0254 : null;
        var yDpi = yPpm > 0 ? yPpm * 0.0254 : null;
        var paletteEntries = bitCount <= 8 ? 1 << bitCount : (int?)null;
        return new AuxInfo(bitCount, xDpi, yDpi, paletteEntries,
            paletteEntries is null ? null : $"BMP palette capacity up to {paletteEntries} entries");
    }

    private sealed record AuxInfo(
        int? BitDepth,
        double? HorizontalDpi,
        double? VerticalDpi,
        int? PaletteEntries,
        string? PaletteInfo);
}
