using ImageForensics.Forensics.FileIdentity;
using Xunit;

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
            Assert.Equal(8, result.Crc32.Length);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ComputesKnownCrc32()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".bin");
        await File.WriteAllBytesAsync(path, "123456789"u8.ToArray());
        try
        {
            var result = await new SafeFileIdentityInspector().InspectAsync(path);
            Assert.Equal("cbf43926", result.Crc32);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task DoesNotMislabelGenericIsoBmffAsHeif()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".heic");
        await File.WriteAllBytesAsync(path, new byte[]
        {
            0,0,0,24, (byte)'f',(byte)'t',(byte)'y',(byte)'p',
            (byte)'i',(byte)'s',(byte)'o',(byte)'m',
            0,0,0,0, (byte)'i',(byte)'s',(byte)'o',(byte)'m'
        });

        try
        {
            var result = await new SafeFileIdentityInspector().InspectAsync(path);
            Assert.Equal("ISO-BMFF", result.DetectedType);
            Assert.Equal("application/octet-stream", result.DetectedMime);
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
