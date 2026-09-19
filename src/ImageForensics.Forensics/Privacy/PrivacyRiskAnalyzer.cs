using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;

namespace ImageForensics.Forensics.Privacy;

public sealed class PrivacyRiskAnalyzer : IPrivacyRiskAnalyzer
{
    public PrivacyRiskReport Analyze(MetadataInspectionResult metadata, ContainerInspectionResult container)
    {
        var risks = new List<PrivacyRisk>();

        if (metadata.Gps is not null)
            risks.Add(new PrivacyRisk("privacy.gps", "GPS coordinates", "Explicit GPS metadata is present.", ForensicConfidence.Confirmed));

        AddIfPresent(metadata, risks, "Model", "privacy.device_model", "Device/camera model");
        AddIfPresent(metadata, risks, "Serial Number", "privacy.serial", "Serial number");
        AddIfPresent(metadata, risks, "Body Serial Number", "privacy.serial", "Camera body serial number");
        AddIfPresent(metadata, risks, "Artist", "privacy.owner", "Artist/owner field");
        AddIfPresent(metadata, risks, "Copyright", "privacy.copyright", "Copyright field");
        AddIfPresent(metadata, risks, "Date/Time Original", "privacy.timestamp", "Original timestamp");
        AddIfPresent(metadata, risks, "Software", "privacy.software", "Software history indicator");

        if (metadata.Fields.Any(x => x.Directory.Contains("XMP", StringComparison.OrdinalIgnoreCase)))
            risks.Add(new PrivacyRisk("privacy.xmp", "XMP metadata", "One or more XMP fields are present.", ForensicConfidence.Confirmed));

        if (metadata.Fields.Any(x => x.Directory.Contains("IPTC", StringComparison.OrdinalIgnoreCase)))
            risks.Add(new PrivacyRisk("privacy.iptc", "IPTC metadata", "One or more IPTC fields are present.", ForensicConfidence.Confirmed));

        if (metadata.Fields.Any(x => x.Tag.Contains("Thumbnail", StringComparison.OrdinalIgnoreCase)))
            risks.Add(new PrivacyRisk("privacy.thumbnail", "Embedded thumbnail", "Thumbnail-related metadata is present.", ForensicConfidence.Confirmed));

        if (container.TrailingBytes > 0)
            risks.Add(new PrivacyRisk("privacy.trailing", "Appended data", $"{container.TrailingBytes} trailing byte(s) follow the image end marker.", ForensicConfidence.Confirmed));

        return new PrivacyRiskReport(risks);
    }

    private static void AddIfPresent(
        MetadataInspectionResult metadata,
        ICollection<PrivacyRisk> risks,
        string tag,
        string code,
        string title)
    {
        var field = metadata.Fields.FirstOrDefault(x => x.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));
        if (field is null) return;

        risks.Add(new PrivacyRisk(code, title, $"{field.Directory}/{field.Tag} is present.", ForensicConfidence.Confirmed));
    }
}
