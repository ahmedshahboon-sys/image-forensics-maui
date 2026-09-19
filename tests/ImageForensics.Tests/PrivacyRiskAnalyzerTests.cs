using ImageForensics.Core.Models;
using ImageForensics.Forensics.Privacy;
using Xunit;

namespace ImageForensics.Tests;

public sealed class PrivacyRiskAnalyzerTests
{
    [Fact]
    public void ReportsAllMasterPrivacyCategories()
    {
        var metadata =
            new MetadataInspectionResult(
                new[]
                {
                    Field("GPS", "GPS Latitude", "32.8"),
                    Field("Exif IFD0", "Model", "Phone X"),
                    Field("Exif SubIFD", "Body Serial Number", "ABC123"),
                    Field("Exif IFD0", "Artist", "Owner"),
                    Field("Exif SubIFD", "Date/Time Original", "2026:09:19 12:00:00"),
                    Field("Exif IFD0", "Software", "Editor"),
                    Field("Exif Thumbnail", "Thumbnail Offset", "128"),
                    Field("XMP", "Creator Tool", "Tool"),
                    Field("IPTC", "Keywords", "private"),
                    Field("Exif SubIFD", "User Comment", "note")
                },
                new GpsInfo(32.8, 13.2),
                Array.Empty<string>());

        var container =
            new ContainerInspectionResult(
                "JPEG",
                new[]
                {
                    new ContainerSegment(
                        100,
                        12,
                        "COM",
                        "Comment")
                },
                42,
                Array.Empty<string>());

        var report =
            new PrivacyRiskAnalyzer()
                .Analyze(
                    metadata,
                    container);

        var codes =
            report.Risks
                .Select(r => r.Code)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        Assert.Contains("privacy.gps", codes);
        Assert.Contains("privacy.device_model", codes);
        Assert.Contains("privacy.serial", codes);
        Assert.Contains("privacy.owner", codes);
        Assert.Contains("privacy.timestamp", codes);
        Assert.Contains("privacy.software", codes);
        Assert.Contains("privacy.thumbnail", codes);
        Assert.Contains("privacy.xmp", codes);
        Assert.Contains("privacy.iptc", codes);
        Assert.Contains("privacy.comments", codes);
        Assert.Contains("privacy.trailing", codes);
        Assert.All(
            report.Risks,
            r =>
                Assert.Equal(
                    ForensicConfidence.Confirmed,
                    r.Confidence));
    }

    [Fact]
    public void GpsFieldsAreReportedEvenIfCoordinatesCannotBeParsed()
    {
        var metadata =
            new MetadataInspectionResult(
                new[]
                {
                    Field(
                        "GPS",
                        "GPS Latitude Ref",
                        "North")
                },
                null,
                Array.Empty<string>());

        var report =
            new PrivacyRiskAnalyzer()
                .Analyze(
                    metadata,
                    EmptyContainer());

        Assert.Contains(
            report.Risks,
            r => r.Code == "privacy.gps");
    }

    [Fact]
    public void EmptyMetadataAndContainerDoNotInventPrivacyRisks()
    {
        var report =
            new PrivacyRiskAnalyzer()
                .Analyze(
                    new MetadataInspectionResult(
                        Array.Empty<MetadataField>(),
                        null,
                        Array.Empty<string>()),
                    EmptyContainer());

        Assert.Empty(report.Risks);
        Assert.False(report.HasSensitiveData);
    }

    [Fact]
    public void ExposureTimeIsNotMisclassifiedAsPrivacyTimestamp()
    {
        var metadata =
            new MetadataInspectionResult(
                new[]
                {
                    Field(
                        "Exif SubIFD",
                        "Exposure Time",
                        "1/100 sec")
                },
                null,
                Array.Empty<string>());

        var report =
            new PrivacyRiskAnalyzer()
                .Analyze(
                    metadata,
                    EmptyContainer());

        Assert.DoesNotContain(
            report.Risks,
            r => r.Code == "privacy.timestamp");
    }

    [Fact]
    public void ZeroSizedJfifThumbnailFieldsAreNotReportedAsEmbeddedThumbnail()
    {
        var metadata =
            new MetadataInspectionResult(
                new[]
                {
                    Field(
                        "JFIF",
                        "Thumbnail Width",
                        "0"),
                    Field(
                        "JFIF",
                        "Thumbnail Height",
                        "0")
                },
                null,
                Array.Empty<string>());

        var report =
            new PrivacyRiskAnalyzer()
                .Analyze(
                    metadata,
                    EmptyContainer());

        Assert.DoesNotContain(
            report.Risks,
            r => r.Code == "privacy.thumbnail");
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

    private static ContainerInspectionResult EmptyContainer()
        => new(
            "JPEG",
            Array.Empty<ContainerSegment>(),
            0,
            Array.Empty<string>());
}
