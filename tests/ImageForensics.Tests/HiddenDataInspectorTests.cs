using ImageForensics.Forensics.HiddenData;
using Xunit;

namespace ImageForensics.Tests;

public sealed class HiddenDataInspectorTests
{
    [Fact]
    public async Task FindsEmbeddedZipSignatureWithoutExtractingIt()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid() + ".bin");

        await File.WriteAllBytesAsync(
            path,
            new byte[]
            {
                1,2,3,4,5,
                0x50,0x4B,0x03,0x04,
                8,9
            });

        try
        {
            var hits =
                await new HiddenDataInspector()
                    .InspectAsync(path);

            var hit =
                Assert.Single(
                    hits,
                    h =>
                        h.Kind == "ZIP archive" &&
                        h.Offset == 5);

            Assert.Contains(
                "never",
                hit.Limitation,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FindsLongPrintableStringAsUnknownNotConfirmedThreat()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid() + ".bin");

        var bytes =
            new byte[] { 0,1,2 }
                .Concat(
                    System.Text.Encoding.ASCII.GetBytes(
                        "THIS_IS_A_LONG_PRINTABLE_FORENSIC_TEST_STRING"))
                .Concat(
                    new byte[] { 0,3,4 })
                .ToArray();

        await File.WriteAllBytesAsync(
            path,
            bytes);

        try
        {
            var hits =
                await new HiddenDataInspector()
                    .InspectAsync(path);

            var hit =
                Assert.Single(
                    hits,
                    h =>
                        h.Kind ==
                        "Printable string");

            Assert.Equal(
                ImageForensics.Core.Models.ForensicConfidence.Unknown,
                hit.Confidence);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DetectsTrailingRegionAfterJpegEoi()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid() + ".jpg");

        await File.WriteAllBytesAsync(
            path,
            new byte[]
            {
                0xFF,0xD8,
                0xFF,0xD9,
                1,2,3,4,5,6
            });

        try
        {
            var hits =
                await new HiddenDataInspector()
                    .InspectAsync(path);

            var hit =
                Assert.Single(
                    hits,
                    h =>
                        h.Kind ==
                        "Trailing payload region");

            Assert.Equal(4, hit.Offset);
            Assert.Equal(
                ImageForensics.Core.Models.ForensicConfidence.Confirmed,
                hit.Confidence);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReportsHighEntropyWindowAsUnknownOnly()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid() + ".bin");

        var bytes =
            new byte[128 * 1024];

        new Random(1234567)
            .NextBytes(bytes);

        await File.WriteAllBytesAsync(
            path,
            bytes);

        try
        {
            var hits =
                await new HiddenDataInspector()
                    .InspectAsync(path);

            var hit =
                Assert.Single(
                    hits,
                    h =>
                        h.Kind ==
                        "High entropy region");

            Assert.Equal(
                ImageForensics.Core.Models.ForensicConfidence.Unknown,
                hit.Confidence);

            Assert.Contains(
                "normally high entropy",
                hit.Limitation,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task PrimaryJpegSignatureAtOffsetZeroIsNotReportedAsEmbeddedImage()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid() + ".jpg");

        await File.WriteAllBytesAsync(
            path,
            new byte[]
            {
                0xFF,0xD8,0xFF,
                0xFF,0xD9
            });

        try
        {
            var hits =
                await new HiddenDataInspector()
                    .InspectAsync(path);

            Assert.DoesNotContain(
                hits,
                h =>
                    h.Kind == "JPEG image" &&
                    h.Offset == 0);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
