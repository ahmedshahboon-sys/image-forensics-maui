using ImageForensics.Core.Models;
using ImageForensics.Forensics.Rules;
using Xunit;

namespace ImageForensics.Tests;

public sealed class ConsistencyRuleEngineTests
{
    [Fact]
    public void ExtensionMismatchIsConfirmedTechnicalFactButNotForgeryVerdict()
    {
        var context = BuildContext(
            extensionMismatch: true,
            metadata: Array.Empty<MetadataField>());

        var findings = new ConsistencyRuleEngine().Analyze(context);
        var finding = Assert.Single(findings, x => x.Code == "file.extension_mismatch");

        Assert.Equal(ForensicConfidence.Confirmed, finding.Confidence);
        Assert.Contains("لا يثبت", finding.Limitation);
    }

    [Fact]
    public void ConflictingCameraMakeValuesAreSurfacedWithLimitation()
    {
        var fields = new[]
        {
            Field("IFD0", "Make", "Canon"),
            Field("Thumbnail", "Make", "Nikon")
        };

        var findings = new ConsistencyRuleEngine().Analyze(
            BuildContext(false, fields));

        var finding = Assert.Single(
            findings,
            x => x.Code == "metadata.make_conflict");

        Assert.Equal(ForensicConfidence.Likely, finding.Confidence);
        Assert.Contains("Directory", finding.Limitation);
    }

    [Fact]
    public void ExifDimensionMismatchIsLikelyButNotVerdict()
    {
        var fields = new[]
        {
            Field("Exif SubIFD", "Exif Image Width", "4000"),
            Field("Exif SubIFD", "Exif Image Height", "3000")
        };

        var context = BuildContext(false, fields) with
        {
            Technical = BuildTechnical(2000, 1500)
        };

        var findings = new ConsistencyRuleEngine().Analyze(context);
        var finding = Assert.Single(
            findings,
            x => x.Code == "metadata.dimension_mismatch");

        Assert.Equal(ForensicConfidence.Likely, finding.Confidence);
        Assert.Contains("إعادة التحجيم", finding.Limitation);
    }

    [Fact]
    public void ModifiedBeforeOriginalIsOnlyPossible()
    {
        var fields = new[]
        {
            Field("Exif SubIFD", "Date/Time Original", "2026:09:19 12:00:00"),
            Field("IFD0", "Date/Time", "2026:09:18 12:00:00")
        };

        var findings = new ConsistencyRuleEngine().Analyze(
            BuildContext(false, fields));

        var finding = Assert.Single(
            findings,
            x => x.Code == "metadata.time_modified_before_original");

        Assert.Equal(ForensicConfidence.Possible, finding.Confidence);
        Assert.Contains("لا يعتبر دليل", finding.Limitation);
    }

    [Fact]
    public void ExtremeAspectRatioIsConfirmedFactWithNeutralLimitation()
    {
        var context = BuildContext(
            false,
            Array.Empty<MetadataField>()) with
        {
            Technical = BuildTechnical(10000, 100)
        };

        var findings = new ConsistencyRuleEngine().Analyze(context);
        var finding = Assert.Single(
            findings,
            x => x.Code == "image.extreme_aspect_ratio");

        Assert.Equal(ForensicConfidence.Confirmed, finding.Confidence);
        Assert.Contains("لا يثبت", finding.Limitation);
    }

    private static MetadataField Field(
        string directory,
        string tag,
        string value)
        => new(
            directory,
            tag,
            value,
            value,
            "test",
            ForensicConfidence.Confirmed,
            directory);

    private static ForensicAnalysisContext BuildContext(
        bool extensionMismatch,
        IReadOnlyList<MetadataField> metadata)
        => new(
            new FileIdentityResult
            {
                FileName = "x.jpg",
                SizeBytes = 10,
                Extension = ".jpg",
                DeclaredMime = "image/jpeg",
                DetectedType = extensionMismatch ? "PNG" : "JPEG",
                DetectedMime = extensionMismatch ? "image/png" : "image/jpeg",
                ExtensionMismatch = extensionMismatch,
                EntropyBitsPerByte = 1,
                Sha256 = "a",
                Sha1 = "b",
                Md5 = "c",
                Crc32 = "00000000",
                SignatureHex = extensionMismatch ? "89504e47" : "ffd8ff"
            },
            BuildTechnical(1000, 1000),
            new MetadataInspectionResult(
                metadata,
                null,
                Array.Empty<string>()),
            new ContainerInspectionResult(
                extensionMismatch ? "PNG" : "JPEG",
                Array.Empty<ContainerSegment>(),
                0,
                Array.Empty<string>()));

    private static ImageTechnicalInfo BuildTechnical(
        int width,
        int height)
        => new()
        {
            Width = width,
            Height = height,
            AspectRatio = (double)width / height,
            EncodedFormat = "Jpeg",
            ColorType = "Rgba8888",
            AlphaType = "Opaque",
            HasAlpha = false,
            BitsPerPixel = 32,
            FrameCount = 1,
            Orientation = "TopLeft",
            Compression = "JPEG DCT"
        };
}
