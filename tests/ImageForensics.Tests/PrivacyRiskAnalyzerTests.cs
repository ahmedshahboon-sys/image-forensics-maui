using ImageForensics.Core.Models;
using ImageForensics.Forensics.Privacy;
using Xunit;

namespace ImageForensics.Tests;

public sealed class PrivacyRiskAnalyzerTests
{
    [Fact]
    public void ReportsExplicitGpsAndTrailingData()
    {
        var metadata = new MetadataInspectionResult(
            new[] { new MetadataField("GPS", "GPS Latitude", "1", "1", "Recorded latitude", ForensicConfidence.Confirmed, "GPS") },
            new GpsInfo(32.8, 13.2),
            Array.Empty<string>());
        var container = new ContainerInspectionResult("JPEG", Array.Empty<ContainerSegment>(), 12, Array.Empty<string>());

        var report = new PrivacyRiskAnalyzer().Analyze(metadata, container);
        Assert.Contains(report.Risks, x => x.Code == "privacy.gps");
        Assert.Contains(report.Risks, x => x.Code == "privacy.trailing");
    }
}
