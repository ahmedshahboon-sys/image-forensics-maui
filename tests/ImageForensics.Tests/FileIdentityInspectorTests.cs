using ImageForensics.Forensics.FileIdentity;

namespace ImageForensics.Tests;

public sealed class FileIdentityInspectorTests
{
    [Fact]
    public async Task DetectsPngMagicBytesEvenWithWrongExtension()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpg");
        await File.WriteAllBytesAsync(path, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 });
        try
        {
            var result = await new SafeFileIdentityInspector().InspectAsync(path);
            Assert.Equal("PNG", result.DetectedType);
            Assert.True(result.ExtensionMismatch);
            Assert.Equal(64, result.Sha256.Length);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task RejectsEmptyFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpg");
        await File.WriteAllBytesAsync(path, Array.Empty<byte>());
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => new SafeFileIdentityInspector().InspectAsync(path));
        }
        finally { File.Delete(path); }
    }
}
