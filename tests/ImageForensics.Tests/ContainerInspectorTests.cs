using ImageForensics.Forensics.Containers;
using Xunit;

namespace ImageForensics.Tests;

public sealed class ContainerInspectorTests
{
    [Fact]
    public async Task DetectsTrailingBytesAfterJpegEoi()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpg");
        await File.WriteAllBytesAsync(path, new byte[] { 0xFF, 0xD8, 0xFF, 0xD9, 1, 2, 3, 4 });
        try
        {
            var result = await new SafeContainerInspector().InspectAsync(path);
            Assert.Equal("JPEG", result.Format);
            Assert.Equal(4, result.TrailingBytes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task DetectsKnownSignatureAfterJpegEoi()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpg");
        await File.WriteAllBytesAsync(path, new byte[]
        {
            0xFF,0xD8,0xFF,0xD9,
            0x50,0x4B,0x03,0x04,0,0,0,0
        });
        try
        {
            var result = await new SafeContainerInspector().InspectAsync(path);
            Assert.Contains(result.Warnings, w => w.Contains("ZIP", StringComparison.OrdinalIgnoreCase));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ReadsMinimalPngChunks()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        var bytes = new byte[]
        {
            0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,
            0,0,0,0, 0x49,0x45,0x4E,0x44, 0,0,0,0
        };
        await File.WriteAllBytesAsync(path, bytes);
        try
        {
            var result = await new SafeContainerInspector().InspectAsync(path);
            Assert.Equal("PNG", result.Format);
            Assert.Contains(result.Segments, s => s.Type == "IEND");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ReadsWebpRiffChunks()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".webp");
        var bytes = new byte[]
        {
            (byte)'R',(byte)'I',(byte)'F',(byte)'F',
            16,0,0,0,
            (byte)'W',(byte)'E',(byte)'B',(byte)'P',
            (byte)'X',(byte)'M',(byte)'P',(byte)' ',
            4,0,0,0,
            (byte)'t',(byte)'e',(byte)'s',(byte)'t'
        };
        await File.WriteAllBytesAsync(path, bytes);

        try
        {
            var result = await new SafeContainerInspector().InspectAsync(path);
            Assert.Equal("WebP", result.Format);
            Assert.Contains(result.Segments, s => s.Type == "XMP ");
            Assert.Equal(0, result.TrailingBytes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ReadsGifLogicalStructure()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gif");
        var bytes = new byte[]
        {
            (byte)'G',(byte)'I',(byte)'F',(byte)'8',(byte)'9',(byte)'a',
            1,0,1,0,
            0,0,0,
            0x3B
        };
        await File.WriteAllBytesAsync(path, bytes);

        try
        {
            var result = await new SafeContainerInspector().InspectAsync(path);
            Assert.Equal("GIF", result.Format);
            Assert.Contains(result.Segments, s => s.Type == "LSD");
            Assert.Contains(result.Segments, s => s.Type == "TRAILER");
        }
        finally { File.Delete(path); }
    }
}
