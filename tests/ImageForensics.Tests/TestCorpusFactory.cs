using System.Buffers.Binary;
using System.Text;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;

namespace ImageForensics.Tests;

internal static class TestCorpusFactory
{
    public static string CreateRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "image-forensics-corpus",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        return root;
    }

    public static async Task<string> CreateJpegAsync(
        string root,
        string name = "plain.jpg",
        int width = 160,
        int height = 120,
        int quality = 92)
    {
        var path = Path.Combine(root, name);

        using var bitmap = new SKBitmap(
            width,
            height,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var checker = ((x / 16) + (y / 16)) % 2 == 0;
                bitmap.SetPixel(
                    x,
                    y,
                    checker
                        ? new SKColor(
                            (byte)(40 + x % 170),
                            (byte)(30 + y % 180),
                            170)
                        : new SKColor(
                            220,
                            (byte)(50 + x % 130),
                            (byte)(40 + y % 120)));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality)
            ?? throw new InvalidOperationException("JPEG encode failed.");

        await using var stream = File.Create(path);
        data.SaveTo(stream);
        await stream.FlushAsync();
        return path;
    }

    public static async Task<string> CreateScreenshotPngAsync(
        string root,
        string name = "screenshot.png")
    {
        var path = Path.Combine(root, name);

        using var bitmap = new SKBitmap(
            360,
            640,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var dark = new SKPaint { Color = new SKColor(28, 28, 32) };
        using var light = new SKPaint { Color = new SKColor(235, 235, 240) };
        using var accent = new SKPaint { Color = new SKColor(30, 120, 220) };

        canvas.DrawRect(0, 0, 360, 64, dark);
        canvas.DrawRect(20, 100, 320, 48, light);
        canvas.DrawRect(20, 170, 260, 48, light);
        canvas.DrawRect(20, 250, 140, 44, accent);
        canvas.DrawRect(20, 330, 320, 220, light);

        await SavePngAsync(bitmap, path);
        return path;
    }

    public static async Task<string> CreateJpegWithExifGpsAsync(
        string root,
        string name = "gps-exif.jpg")
    {
        var plain = await CreateJpegAsync(root, "gps-base.jpg");
        var output = Path.Combine(root, name);

        var jpeg = await File.ReadAllBytesAsync(plain);
        var app1 = BuildExifGpsApp1();

        var combined = new byte[
            jpeg.Length +
            app1.Length];

        Buffer.BlockCopy(jpeg, 0, combined, 0, 2);
        Buffer.BlockCopy(app1, 0, combined, 2, app1.Length);
        Buffer.BlockCopy(jpeg, 2, combined, 2 + app1.Length, jpeg.Length - 2);

        await File.WriteAllBytesAsync(output, combined);
        return output;
    }

    public static async Task<string> CreateJpegWithEmbeddedThumbnailAsync(
        string root,
        string name = "embedded-thumbnail.jpg")
    {
        var plain = await CreateJpegAsync(root, "thumb-base.jpg");
        var thumbnailPath = await CreateJpegAsync(
            root,
            "thumb-small.jpg",
            24,
            18,
            80);

        var jpeg = await File.ReadAllBytesAsync(plain);
        var thumbnail = await File.ReadAllBytesAsync(thumbnailPath);
        var app1 = BuildExifThumbnailApp1(thumbnail);
        var output = Path.Combine(root, name);

        var combined = new byte[
            jpeg.Length +
            app1.Length];

        Buffer.BlockCopy(jpeg, 0, combined, 0, 2);
        Buffer.BlockCopy(app1, 0, combined, 2, app1.Length);
        Buffer.BlockCopy(jpeg, 2, combined, 2 + app1.Length, jpeg.Length - 2);

        await File.WriteAllBytesAsync(output, combined);
        return output;
    }

    public static async Task<string> CreatePngWithTextChunkAsync(
        string root,
        string name = "png-metadata.png")
    {
        var plain = await CreateScreenshotPngAsync(root, "png-base.png");
        var bytes = await File.ReadAllBytesAsync(plain);

        var iendStart = FindPngChunkStart(bytes, "IEND");
        if (iendStart < 0)
            throw new InvalidDataException("IEND not found.");

        var data = Encoding.Latin1.GetBytes(
            "Comment\0Synthetic local corpus metadata");

        var chunk = BuildPngChunk(
            "tEXt",
            data);

        var outputBytes = new byte[
            bytes.Length +
            chunk.Length];

        Buffer.BlockCopy(bytes, 0, outputBytes, 0, iendStart);
        Buffer.BlockCopy(chunk, 0, outputBytes, iendStart, chunk.Length);
        Buffer.BlockCopy(
            bytes,
            iendStart,
            outputBytes,
            iendStart + chunk.Length,
            bytes.Length - iendStart);

        var output = Path.Combine(root, name);
        await File.WriteAllBytesAsync(output, outputBytes);
        return output;
    }

    public static async Task<string> CreateDoubleCompressedJpegAsync(
        string root,
        string name = "double-compressed.jpg")
    {
        var first = await CreateJpegAsync(
            root,
            "first-pass.jpg",
            320,
            240,
            92);

        using var bitmap = SKBitmap.Decode(first)
            ?? throw new InvalidDataException("Could not decode first pass.");

        var output = Path.Combine(root, name);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 58)
            ?? throw new InvalidOperationException("Second JPEG encode failed.");

        await using var stream = File.Create(output);
        data.SaveTo(stream);
        await stream.FlushAsync();
        return output;
    }

    public static async Task<string> CreateCorruptJpegAsync(
        string root,
        string name = "corrupt.jpg")
    {
        var path = Path.Combine(root, name);
        await File.WriteAllBytesAsync(
            path,
            new byte[]
            {
                0xFF, 0xD8, 0xFF, 0xE1,
                0x00, 0x20,
                (byte)'E', (byte)'x', (byte)'i', (byte)'f',
                0x00, 0x00,
                0x49, 0x49, 0x2A, 0x00,
                0x08, 0x00
            });

        return path;
    }

    public static async Task<string> CreateJpegWithAppendedBytesAsync(
        string root,
        string name = "appended.jpg")
    {
        var path = await CreateJpegAsync(root, name);
        await File.AppendAllBytesAsync(
            path,
            new byte[]
            {
                0x50, 0x4B, 0x03, 0x04,
                0xDE, 0xAD, 0xBE, 0xEF
            });

        return path;
    }

    public static async Task<string> CreateQrPngAsync(
        string root,
        string text = "https://example.com/forensics",
        string name = "qr.png")
    {
        var writer = new BarcodeWriter
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions
            {
                Height = 320,
                Width = 320,
                Margin = 2
            }
        };

        using var bitmap = writer.Write(text);
        var path = Path.Combine(root, name);
        await SavePngAsync(bitmap, path);
        return path;
    }

    public static async Task<string> CreateArabicOcrImageAsync(
        string root,
        string name = "arabic-ocr.png")
    {
        var path = Path.Combine(root, name);

        using var bitmap = new SKBitmap(
            900,
            260,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true
        };

        using var typeface = LoadNotoArabicTypeface();
        using var font = new SKFont(typeface, 72);

        canvas.DrawText(
            "اختبار عربي 123",
            60,
            150,
            SKTextAlign.Left,
            font,
            paint);

        await SavePngAsync(bitmap, path);
        return path;
    }

    public static async Task<string> CreateHugeHeaderPngAsync(
        string root,
        string name = "huge-header.png")
    {
        var signature = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47,
            0x0D, 0x0A, 0x1A, 0x0A
        };

        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), 100_000);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), 100_000);
        ihdr[8] = 8;
        ihdr[9] = 2;

        var bytes = signature
            .Concat(BuildPngChunk("IHDR", ihdr))
            .Concat(BuildPngChunk("IEND", Array.Empty<byte>()))
            .ToArray();

        var path = Path.Combine(root, name);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    public static void DeleteRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
        catch
        {
        }
    }

    private static byte[] BuildExifGpsApp1()
    {
        using var tiff = new MemoryStream();
        using var writer = new BinaryWriter(tiff, Encoding.ASCII, true);

        writer.Write((byte)'I');
        writer.Write((byte)'I');
        writer.Write((ushort)42);
        writer.Write((uint)8);

        writer.Write((ushort)3);
        WriteIfdEntry(writer, 0x010F, 2, 8, 50);
        WriteIfdEntry(writer, 0x0110, 2, 7, 58);
        WriteIfdEntry(writer, 0x8825, 4, 1, 66);
        writer.Write((uint)0);

        writer.Write(Encoding.ASCII.GetBytes("TestCam\0"));
        writer.Write(Encoding.ASCII.GetBytes("Model1\0"));
        writer.Write((byte)0);

        writer.Write((ushort)4);
        WriteIfdEntry(writer, 0x0001, 2, 2, 0x0000004E);
        WriteIfdEntry(writer, 0x0002, 5, 3, 120);
        WriteIfdEntry(writer, 0x0003, 2, 2, 0x00000045);
        WriteIfdEntry(writer, 0x0004, 5, 3, 144);
        writer.Write((uint)0);

        WriteRational(writer, 32, 1);
        WriteRational(writer, 53, 1);
        WriteRational(writer, 0, 1);
        WriteRational(writer, 13, 1);
        WriteRational(writer, 11, 1);
        WriteRational(writer, 0, 1);

        return WrapExifApp1(tiff.ToArray());
    }

    private static byte[] BuildExifThumbnailApp1(byte[] thumbnail)
    {
        using var tiff = new MemoryStream();
        using var writer = new BinaryWriter(tiff, Encoding.ASCII, true);

        writer.Write((byte)'I');
        writer.Write((byte)'I');
        writer.Write((ushort)42);
        writer.Write((uint)8);

        writer.Write((ushort)0);
        writer.Write((uint)14);

        writer.Write((ushort)2);
        WriteIfdEntry(writer, 0x0201, 4, 1, 44);
        WriteIfdEntry(
            writer,
            0x0202,
            4,
            1,
            checked((uint)thumbnail.Length));
        writer.Write((uint)0);
        writer.Write(thumbnail);

        return WrapExifApp1(tiff.ToArray());
    }

    private static byte[] WrapExifApp1(byte[] tiff)
    {
        var payload = Encoding.ASCII
            .GetBytes("Exif\0\0")
            .Concat(tiff)
            .ToArray();

        var length = checked((ushort)(payload.Length + 2));
        var app1 = new byte[payload.Length + 4];
        app1[0] = 0xFF;
        app1[1] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(
            app1.AsSpan(2, 2),
            length);

        Buffer.BlockCopy(payload, 0, app1, 4, payload.Length);
        return app1;
    }

    private static void WriteIfdEntry(
        BinaryWriter writer,
        ushort tag,
        ushort type,
        uint count,
        uint valueOrOffset)
    {
        writer.Write(tag);
        writer.Write(type);
        writer.Write(count);
        writer.Write(valueOrOffset);
    }

    private static void WriteRational(
        BinaryWriter writer,
        uint numerator,
        uint denominator)
    {
        writer.Write(numerator);
        writer.Write(denominator);
    }

    private static int FindPngChunkStart(
        byte[] bytes,
        string chunkType)
    {
        var type = Encoding.ASCII.GetBytes(chunkType);

        for (var i = 8;
             i <= bytes.Length - 8;
             i++)
        {
            if (bytes.AsSpan(i, 4).SequenceEqual(type))
                return i - 4;
        }

        return -1;
    }

    private static byte[] BuildPngChunk(
        string type,
        byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var chunk = new byte[
            4 +
            4 +
            data.Length +
            4];

        BinaryPrimitives.WriteUInt32BigEndian(
            chunk.AsSpan(0, 4),
            checked((uint)data.Length));

        Buffer.BlockCopy(typeBytes, 0, chunk, 4, 4);
        Buffer.BlockCopy(data, 0, chunk, 8, data.Length);

        var crcInput = typeBytes
            .Concat(data)
            .ToArray();

        BinaryPrimitives.WriteUInt32BigEndian(
            chunk.AsSpan(8 + data.Length, 4),
            PngCrc32(crcInput));

        return chunk;
    }

    private static uint PngCrc32(
        ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var value in bytes)
        {
            crc ^= value;

            for (var bit = 0;
                 bit < 8;
                 bit++)
            {
                crc =
                    (crc & 1) != 0
                        ? (crc >> 1) ^ 0xEDB88320u
                        : crc >> 1;
            }
        }

        return ~crc;
    }

    private static async Task SavePngAsync(
        SKBitmap bitmap,
        string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("PNG encode failed.");

        await using var stream = File.Create(path);
        data.SaveTo(stream);
        await stream.FlushAsync();
    }

    private static SKTypeface LoadNotoArabicTypeface()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ImageForensics.Reporting",
                "Resources",
                "NotoSansArabic-Regular.ttf");

            if (File.Exists(candidate))
                return SKTypeface.FromFile(candidate)
                    ?? throw new InvalidOperationException(
                        "Unable to load Noto Sans Arabic.");

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Pinned Noto Sans Arabic test font was not found.");
    }
}
