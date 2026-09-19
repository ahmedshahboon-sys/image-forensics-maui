using System.Text;
using System.Text.Json;

namespace ImageForensics.Reporting;

public sealed class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string ToJson(ScanReport report) => JsonSerializer.Serialize(report, JsonOptions);

    public string ToText(ScanReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("IMAGE FORENSICS REPORT");
        sb.AppendLine($"App version: {report.AppVersion}");
        sb.AppendLine($"Scan time (UTC): {report.ScannedAtUtc:O}");
        sb.AppendLine($"File: {report.Identity.FileName}");
        sb.AppendLine($"SHA-256: {report.Identity.Sha256}");
        sb.AppendLine($"Detected type: {report.Identity.DetectedType}");
        sb.AppendLine($"Size: {report.Identity.SizeBytes} bytes");

        if (report.Technical is not null)
        {
            sb.AppendLine($"Dimensions: {report.Technical.Width}x{report.Technical.Height}");
            sb.AppendLine($"Frames: {report.Technical.FrameCount}");
            sb.AppendLine($"Color: {report.Technical.ColorType} / {report.Technical.AlphaType}");
        }

        if (report.Metadata?.Gps is not null)
            sb.AppendLine($"GPS: {report.Metadata.Gps.Latitude:F8}, {report.Metadata.Gps.Longitude:F8}");

        if (report.Container is not null)
            sb.AppendLine($"Container: {report.Container.Format}; trailing bytes: {report.Container.TrailingBytes}");

        if (report.PerceptualHashes is not null)
            sb.AppendLine($"pHash: {report.PerceptualHashes.PHash}");

        foreach (var hit in report.Barcodes)
            sb.AppendLine($"Barcode/QR [{hit.Format}]: {hit.Text}");

        sb.AppendLine();
        sb.AppendLine("Limitations: probabilistic forensic indicators are not final proof of authenticity or manipulation.");
        return sb.ToString();
    }
}
