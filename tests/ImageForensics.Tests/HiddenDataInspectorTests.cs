using ImageForensics.Forensics.HiddenData;
using Xunit;

namespace ImageForensics.Tests;

public sealed class HiddenDataInspectorTests
{
    [Fact]
    public async Task FindsEmbeddedZipSignatureWithoutExtractingIt()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".bin");
        await File.WriteAllBytesAsync(path, new byte[] { 1,2,3,4,5, 0x50,0x4B,0x03,0x04, 8,9 });
        try
        {
            var hits = await new HiddenDataInspector().InspectAsync(path);
            Assert.Contains(hits, h => h.Kind == "ZIP archive" && h.Offset == 5);
        }
        finally { File.Delete(path); }
    }
}
