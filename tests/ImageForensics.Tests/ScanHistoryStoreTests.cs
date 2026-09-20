using ImageForensics.Core.Models;
using ImageForensics.Storage;
using Xunit;

namespace ImageForensics.Tests;

public sealed class ScanHistoryStoreTests
{
    [Fact]
    public async Task HistoryRoundTripsAndClearsWithoutSensitivePayloadFields()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "image-forensics-history-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "history.json");

        try
        {
            var store = new ScanHistoryStore(path);

            await store.AddAsync(
                new ScanHistoryEntry(
                    DateTimeOffset.UtcNow,
                    "photo.jpg",
                    "JPEG",
                    1234,
                    "abc",
                    true,
                    2,
                    1));

            var rows = await store.GetRecentAsync();

            var row = Assert.Single(rows);
            Assert.Equal("photo.jpg", row.FileName);
            Assert.Equal("abc", row.Sha256);

            var json = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("Latitude", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Ocr", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SourcePath", json, StringComparison.OrdinalIgnoreCase);

            await store.ClearAsync();
            Assert.Empty(await store.GetRecentAsync());
        }
        finally
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
    }
}
