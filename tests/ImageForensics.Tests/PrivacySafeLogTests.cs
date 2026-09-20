using ImageForensics.Storage.Logging;
using Xunit;

namespace ImageForensics.Tests;

public sealed class PrivacySafeLogTests
{
    [Fact]
    public void SensitiveKeysAreNotPersisted()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "image-forensics-log-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "events.jsonl");

        try
        {
            var log = new JsonLinePrivacySafeLog(path);

            log.Info(
                "scan.completed unsafe/name",
                new Dictionary<string, object?>
                {
                    ["duration_ms"] = 25,
                    ["gps"] = "32,13",
                    ["ocr_text"] = "secret",
                    ["source_path"] = "/tmp/private.jpg",
                    ["image_bytes"] = "abcdef"
                });

            var text = File.ReadAllText(path);

            Assert.Contains("duration_ms", text, StringComparison.Ordinal);
            Assert.DoesNotContain("32,13", text, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
            Assert.DoesNotContain("/tmp/private.jpg", text, StringComparison.Ordinal);
            Assert.DoesNotContain("abcdef", text, StringComparison.Ordinal);
            Assert.Contains("scan.completedunsafename", text, StringComparison.Ordinal);
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
