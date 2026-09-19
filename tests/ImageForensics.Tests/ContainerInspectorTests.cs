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
}
