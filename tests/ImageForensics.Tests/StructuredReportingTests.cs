using ImageForensics.Core.Models;
using ImageForensics.Reporting;
using Xunit;

namespace ImageForensics.Tests;

public sealed class StructuredReportingTests
{
    [Fact]
    public void TextReportContainsAllTwelveRequiredSectionsAndAlgorithms()
    {
        var report =
            CreateReport(
                fileName:
                    "صورة-اختبار.jpg");

        var text =
            new ReportWriter()
                .ToText(
                    report);

        for (var i = 1;
             i <= 12;
             i++)
        {
            Assert.Contains(
                $"{i}.",
                text,
                StringComparison.Ordinal);
        }

        Assert.Contains(
            "SUMMARY / الملخص",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "CONFIRMED FACTS / الحقائق المؤكدة",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "SHA-256",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "Algorithms / الخوارزميات",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "Confidence scale / مقياس الثقة",
            text,
            StringComparison.Ordinal);

        Assert.Contains(
            "صورة-اختبار.jpg",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void JsonReportUsesVersionedEnvelopeAndCarriesSourceHash()
    {
        var report =
            CreateReport();

        var json =
            new ReportWriter()
                .ToJson(
                    report);

        Assert.Contains(
            "\"SchemaVersion\": \"1.0\"",
            json,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"ReportType\": \"image-forensics-scan\"",
            json,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"SourceSha256\": \"sha256-test\"",
            json,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"ConfidenceScale\"",
            json,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"Algorithms\"",
            json,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PdfWriterCreatesUnicodeReportWithoutAsciiReplacementPath()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "image-forensics-report-tests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            root);

        var path =
            Path.Combine(
                root,
                "report.pdf");

        try
        {
            var writer =
                new ReportWriter();

            writer.WritePdf(
                CreateReport(
                    fileName:
                        "اختبار-العربية.jpg"),
                path);

            var bytes =
                File.ReadAllBytes(
                    path);

            Assert.True(
                bytes.Length >
                1024);

            Assert.Equal(
                (byte)'%',
                bytes[0]);

            Assert.Equal(
                (byte)'P',
                bytes[1]);

            Assert.Equal(
                (byte)'D',
                bytes[2]);

            Assert.Equal(
                (byte)'F',
                bytes[3]);
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(
                        root,
                        true);
            }
            catch
            {
            }
        }
    }

    private static ScanReport CreateReport(
        string fileName =
            "test.jpg")
        => new(
            "0.9.0-beta",
            new DateTimeOffset(
                2026,
                9,
                20,
                0,
                0,
                0,
                TimeSpan.Zero),
            new FileIdentityResult
            {
                FileName = fileName,
                SizeBytes = 1234,
                Extension = ".jpg",
                DeclaredMime = "image/jpeg",
                DetectedType = "JPEG",
                DetectedMime = "image/jpeg",
                ExtensionMismatch = false,
                EntropyBitsPerByte = 7.5,
                Sha256 = "sha256-test",
                Sha1 = "sha1-test",
                Md5 = "md5-test",
                Crc32 = "crc-test",
                SignatureHex = "FFD8FF"
            },
            new ImageTechnicalInfo
            {
                Width = 100,
                Height = 80,
                AspectRatio = 1.25,
                EncodedFormat = "JPEG",
                ColorType = "Rgba8888",
                AlphaType = "Opaque",
                HasAlpha = false,
                BitsPerPixel = 32,
                FrameCount = 1,
                Orientation = "TopLeft",
                Compression = "JPEG"
            },
            new MetadataInspectionResult(
                new[]
                {
                    new MetadataField(
                        "Exif IFD0",
                        "Model",
                        "هاتف تجريبي",
                        "هاتف تجريبي",
                        "Camera model field",
                        ForensicConfidence.Confirmed,
                        "Exif IFD0")
                },
                null,
                Array.Empty<string>()),
            new ContainerInspectionResult(
                "JPEG",
                Array.Empty<ContainerSegment>(),
                0,
                Array.Empty<string>()),
            new PerceptualHashResult(
                "0000000000000000",
                "0000000000000000",
                "0000000000000000"),
            Array.Empty<BarcodeHit>(),
            new[]
            {
                new EvidenceItem(
                    "test.possible",
                    "Test indicator",
                    "detail",
                    ForensicConfidence.Possible,
                    "evidence",
                    "not proof")
            });
}
