using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;

namespace ImageForensics.Forensics.Privacy;

public sealed class PrivacyRiskAnalyzer : IPrivacyRiskAnalyzer
{
    public PrivacyRiskReport Analyze(
        MetadataInspectionResult metadata,
        ContainerInspectionResult container)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(container);

        var risks = new List<PrivacyRisk>();

        var gpsFields = metadata.Fields
            .Where(f =>
                f.Directory.Contains("GPS", StringComparison.OrdinalIgnoreCase) ||
                f.Tag.StartsWith("GPS ", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (metadata.Gps is not null || gpsFields.Length > 0)
        {
            AddRisk(
                risks,
                "privacy.gps",
                "GPS metadata",
                metadata.Gps is not null
                    ? "Explicit GPS coordinates were parsed from image metadata."
                    : EvidenceFor(gpsFields, "GPS-related metadata fields are present."));
        }

        AddFields(
            metadata,
            risks,
            "privacy.device_model",
            "Device/camera model",
            f =>
                EqualsTag(f, "Model") ||
                EqualsTag(f, "Make"));

        AddFields(
            metadata,
            risks,
            "privacy.serial",
            "Serial number",
            f => f.Tag.Contains(
                "Serial",
                StringComparison.OrdinalIgnoreCase));

        AddFields(
            metadata,
            risks,
            "privacy.owner",
            "Owner/artist/author metadata",
            f =>
                EqualsTag(f, "Artist") ||
                EqualsTag(f, "Copyright") ||
                EqualsTag(f, "XP Author") ||
                EqualsTag(f, "Owner Name") ||
                EqualsTag(f, "Author") ||
                f.Tag.Contains("Creator", StringComparison.OrdinalIgnoreCase) ||
                f.Tag.Contains("By-line", StringComparison.OrdinalIgnoreCase));

        AddFields(
            metadata,
            risks,
            "privacy.timestamp",
            "Capture/edit timestamp metadata",
            IsTimestampField);

        AddFields(
            metadata,
            risks,
            "privacy.software",
            "Software/history metadata",
            f =>
                EqualsTag(f, "Software") ||
                f.Tag.Contains("Processing Software", StringComparison.OrdinalIgnoreCase) ||
                f.Tag.Contains("Creator Tool", StringComparison.OrdinalIgnoreCase) ||
                f.Tag.Contains("History", StringComparison.OrdinalIgnoreCase) ||
                f.Tag.Contains("Host Computer", StringComparison.OrdinalIgnoreCase));

        AddFields(
            metadata,
            risks,
            "privacy.thumbnail",
            "Embedded thumbnail/preview metadata",
            f =>
                f.Directory.Contains("Thumbnail", StringComparison.OrdinalIgnoreCase) ||
                f.Tag.Contains("Thumbnail", StringComparison.OrdinalIgnoreCase) ||
                f.Tag.Contains("Preview Image", StringComparison.OrdinalIgnoreCase));

        var xmpFields = metadata.Fields
            .Where(f =>
                f.Directory.Contains("XMP", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (xmpFields.Length > 0 ||
            container.Segments.Any(s =>
                s.Type.Equals("XMP ", StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains("XMP", StringComparison.OrdinalIgnoreCase)))
        {
            AddRisk(
                risks,
                "privacy.xmp",
                "XMP metadata",
                xmpFields.Length > 0
                    ? EvidenceFor(xmpFields, "XMP metadata is present.")
                    : "The image container includes an XMP metadata segment/chunk.");
        }

        var iptcFields = metadata.Fields
            .Where(f =>
                f.Directory.Contains("IPTC", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (iptcFields.Length > 0)
        {
            AddRisk(
                risks,
                "privacy.iptc",
                "IPTC metadata",
                EvidenceFor(iptcFields, "IPTC metadata is present."));
        }

        var commentFields = metadata.Fields
            .Where(IsCommentLikeField)
            .ToArray();

        var commentContainer =
            container.Segments.Any(s =>
                s.Type.Equals("COM", StringComparison.OrdinalIgnoreCase) ||
                s.Type.Equals("COMMENT", StringComparison.OrdinalIgnoreCase) ||
                s.Type is "tEXt" or "zTXt" or "iTXt" or "PLAIN-TEXT");

        if (commentFields.Length > 0 || commentContainer)
        {
            AddRisk(
                risks,
                "privacy.comments",
                "Comments/descriptions/keywords",
                commentFields.Length > 0
                    ? EvidenceFor(commentFields, "Comment-like textual metadata is present.")
                    : "The image container includes a textual/comment segment or chunk.");
        }

        if (container.TrailingBytes > 0)
        {
            AddRisk(
                risks,
                "privacy.trailing",
                "Hidden/appended data",
                $"{container.TrailingBytes} byte(s) exist after the expected end of the primary image container.");
        }

        return new PrivacyRiskReport(risks);
    }

    private static void AddFields(
        MetadataInspectionResult metadata,
        ICollection<PrivacyRisk> risks,
        string code,
        string title,
        Func<MetadataField, bool> predicate)
    {
        var fields = metadata.Fields
            .Where(predicate)
            .ToArray();

        if (fields.Length == 0)
            return;

        AddRisk(
            risks,
            code,
            title,
            EvidenceFor(fields, $"{fields.Length} matching metadata field(s) are present."));
    }

    private static void AddRisk(
        ICollection<PrivacyRisk> risks,
        string code,
        string title,
        string evidence)
    {
        if (risks.Any(r =>
                r.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            return;

        risks.Add(
            new PrivacyRisk(
                code,
                title,
                evidence,
                ForensicConfidence.Confirmed));
    }

    private static string EvidenceFor(
        IReadOnlyList<MetadataField> fields,
        string prefix)
    {
        var examples = fields
            .Select(f => $"[{f.Directory}] {f.Tag}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();

        return examples.Length == 0
            ? prefix
            : $"{prefix} Examples: {string.Join("; ", examples)}.";
    }

    private static bool EqualsTag(
        MetadataField field,
        string tag)
        => field.Tag.Equals(
            tag,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsTimestampField(
        MetadataField field)
    {
        var tag = field.Tag;

        return
            tag.Equals("Date/Time Original", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("Date/Time Digitized", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("Date/Time", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("Create Date", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("Modify Date", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("Metadata Date", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("GPS Date Stamp", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("GPS Time-Stamp", StringComparison.OrdinalIgnoreCase) ||
            tag.StartsWith("Offset Time", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCommentLikeField(
        MetadataField field)
    {
        var tag = field.Tag;

        return
            tag.Contains("Comment", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Description", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Caption", StringComparison.OrdinalIgnoreCase) ||
            tag.Contains("Keyword", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("XP Title", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("XP Subject", StringComparison.OrdinalIgnoreCase);
    }
}
