using System.Text;
using System.Text.Json;
using SkiaSharp;

namespace ImageForensics.Reporting;

public sealed class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string ToJson(ScanReport report) => JsonSerializer.Serialize(report, JsonOptions);

    public string ToText(ScanReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("IMAGE FORENSICS / PHOTO INSPECTOR REPORT");
        sb.AppendLine($"App version: {report.AppVersion}");
        sb.AppendLine($"Scan time (UTC): {report.ScannedAtUtc:O}");
        sb.AppendLine($"File: {report.Identity.FileName}");
        sb.AppendLine($"Size: {report.Identity.SizeBytes} bytes");
        sb.AppendLine($"Declared MIME: {report.Identity.DeclaredMime}");
        sb.AppendLine($"Detected type: {report.Identity.DetectedType} ({report.Identity.DetectedMime})");
        sb.AppendLine($"Extension mismatch: {report.Identity.ExtensionMismatch}");
        sb.AppendLine($"Entropy: {report.Identity.EntropyBitsPerByte:F4} bits/byte");
        sb.AppendLine($"SHA-256: {report.Identity.Sha256}");
        sb.AppendLine($"SHA-1: {report.Identity.Sha1}");
        sb.AppendLine($"MD5 (comparison only): {report.Identity.Md5}");
        sb.AppendLine($"CRC32: {report.Identity.Crc32}");

        if (report.Technical is not null)
        {
            sb.AppendLine();
            sb.AppendLine("TECHNICAL IMAGE");
            sb.AppendLine($"Dimensions: {report.Technical.Width}x{report.Technical.Height}; Aspect={report.Technical.AspectRatio:F4}");
            sb.AppendLine($"Format/Compression: {report.Technical.EncodedFormat} / {report.Technical.Compression}");
            sb.AppendLine($"Color: {report.Technical.ColorType}; Alpha={report.Technical.HasAlpha}; ColorSpace={report.Technical.ColorSpace ?? "n/a"}");
            sb.AppendLine($"Decoded bits/pixel: {report.Technical.BitsPerPixel}; Encoded bit depth: {report.Technical.EncodedBitDepth?.ToString() ?? "n/a"}");
            sb.AppendLine($"Frames: {report.Technical.FrameCount}; Orientation={report.Technical.Orientation}");
            sb.AppendLine($"DPI: X={report.Technical.HorizontalDpi?.ToString("F2") ?? "n/a"}; Y={report.Technical.VerticalDpi?.ToString("F2") ?? "n/a"}");
            sb.AppendLine($"Palette: {report.Technical.PaletteInfo ?? "n/a"}");
        }

        if (report.Metadata?.Gps is not null)
        {
            var gps = report.Metadata.Gps;
            sb.AppendLine();
            sb.AppendLine("GPS");
            sb.AppendLine($"Coordinates: {gps.Latitude:F8}, {gps.Longitude:F8}");
            if (gps.Altitude is not null) sb.AppendLine($"Altitude: {gps.Altitude:F2}");
            if (gps.Speed is not null) sb.AppendLine($"Speed: {gps.Speed:F2}");
            if (gps.Direction is not null) sb.AppendLine($"Direction: {gps.Direction:F2}");
            if (!string.IsNullOrWhiteSpace(gps.Timestamp)) sb.AppendLine($"Timestamp: {gps.Timestamp}");
        }

        if (report.Metadata is not null)
        {
            sb.AppendLine();
            sb.AppendLine("METADATA");

            foreach (var field in report.Metadata.Fields)
            {
                sb.AppendLine($"[{field.Directory}] {field.Tag}");
                sb.AppendLine($"  Parsed: {field.ParsedValue}");
                sb.AppendLine($"  Raw: {field.RawValue}");
                sb.AppendLine($"  Meaning: {field.Meaning}");
                sb.AppendLine($"  Confidence: {field.Confidence}; Source: {field.Source}");
            }

            foreach (var error in report.Metadata.Errors)
                sb.AppendLine($"Metadata warning: {error}");
        }

        if (report.Container is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"BINARY STRUCTURE — {report.Container.Format}; trailing={report.Container.TrailingBytes}");

            foreach (var segment in report.Container.Segments)
                sb.AppendLine($"- offset={segment.Offset} size={segment.Length} type={segment.Type} suspicious={segment.Suspicious} — {segment.Description}");

            foreach (var warning in report.Container.Warnings)
                sb.AppendLine($"Structure warning: {warning}");
        }

        if (report.PerceptualHashes is not null)
        {
            sb.AppendLine();
            sb.AppendLine("PERCEPTUAL HASHES");
            sb.AppendLine($"aHash={report.PerceptualHashes.AHash}");
            sb.AppendLine($"dHash={report.PerceptualHashes.DHash}");
            sb.AppendLine($"pHash={report.PerceptualHashes.PHash}");
        }

        if (report.ImageHeuristics is not null)
        {
            var h = report.ImageHeuristics;
            sb.AppendLine();
            sb.AppendLine("PIXEL / JPEG FORENSIC METRICS");
            sb.AppendLine($"JPEG quality estimate: {h.EstimatedJpegQuality?.ToString("F1") ?? "n/a"}");
            sb.AppendLine($"Chroma subsampling: {h.ChromaSubsampling ?? "n/a"}");
            sb.AppendLine($"JPEG quantization tables: {h.JpegQuantizationTableCount}");
            sb.AppendLine($"JPEG quantization mean: {h.JpegQuantizationMean?.ToString("F2") ?? "n/a"}");
            sb.AppendLine($"8x8 block-boundary ratio: {h.BlockBoundaryRatio:F4}");
            sb.AppendLine($"Grid phase dominance: {h.GridPhaseDominance:F4}");
            sb.AppendLine($"Resampling periodicity score: {h.ResamplingPeriodicityScore:F4}");
            sb.AppendLine($"Noise regional CV: {h.NoiseCoefficientOfVariation:F4}");
            sb.AppendLine($"ELA mean: {h.ElaMeanDifference:F4}");
            sb.AppendLine($"ELA regional CV: {h.ElaRegionalCoefficientOfVariation:F4}");
            sb.AppendLine($"Edge density: {h.EdgeDensity:F4}");
            sb.AppendLine($"Histogram peakiness: {h.HistogramPeakiness:F4}");
            sb.AppendLine($"Clone/copy-move tile candidates: {h.CloneCandidatePairs}");
        }

        foreach (var hit in report.Barcodes)
            sb.AppendLine($"Barcode/QR [{hit.Format}]: {hit.Text}");

        if (report.Privacy is not null)
        {
            sb.AppendLine();
            sb.AppendLine("PRIVACY RISKS");

            foreach (var risk in report.Privacy.Risks)
                sb.AppendLine($"- [{risk.Confidence}] {risk.Title}: {risk.Evidence}");
        }

        if (report.HiddenData is not null)
        {
            sb.AppendLine();
            sb.AppendLine("HIDDEN-DATA INDICATORS");

            foreach (var finding in report.HiddenData.Take(50))
                sb.AppendLine($"- [{finding.Confidence}] offset={finding.Offset} {finding.Kind}: {finding.Evidence}");
        }

        sb.AppendLine();
        sb.AppendLine("FORENSIC INDICATORS");

        foreach (var indicator in report.Indicators)
        {
            sb.AppendLine(
                $"- [{indicator.Confidence}] {indicator.Title}: {indicator.Detail} | " +
                $"Evidence: {indicator.Evidence} | Limitation: {indicator.Limitation}");
        }

        sb.AppendLine();
        sb.AppendLine(
            "LIMITATION: probabilistic forensic indicators are not final proof of authenticity, manipulation, source, person identity or location.");

        return sb.ToString();
    }

    public string BatchToCsv(IEnumerable<BatchReportRow> rows)
    {
        static string Q(string? s)
            => """ + (s ?? string.Empty).Replace(""", """") + """;

        var sb = new StringBuilder(
            "FileName,SHA256,DetectedType,SizeBytes,Width,Height,HasGps,PrivacyRiskCount,IndicatorCount\n");

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(
                ",",
                Q(r.FileName),
                Q(r.Sha256),
                Q(r.DetectedType),
                r.SizeBytes,
                r.Width,
                r.Height,
                r.HasGps,
                r.PrivacyRiskCount,
                r.IndicatorCount));
        }

        return sb.ToString();
    }

    public void WritePdf(ScanReport report, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        using var stream = File.Create(outputPath);
        using var document = SKDocument.CreatePdf(stream)
            ?? throw new InvalidOperationException("Unable to create PDF.");
        using var paint = new SKPaint { IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 11);

        var lines = ToText(report).Replace("\r", "").Split('\n');

        const float pageW = 595;
        const float pageH = 842;
        const float margin = 36;
        const float lineH = 15;

        SKCanvas? canvas = null;
        float y = margin;

        foreach (var raw in lines)
        {
            if (canvas is null || y > pageH - margin)
            {
                if (canvas is not null)
                    document.EndPage();

                canvas = document.BeginPage(pageW, pageH);
                y = margin;
            }

            var line = ToPdfSafe(raw);

            foreach (var piece in Wrap(line, 92))
            {
                canvas.DrawText(piece, margin, y, SKTextAlign.Left, font, paint);
                y += lineH;

                if (y > pageH - margin)
                    break;
            }
        }

        if (canvas is not null)
            document.EndPage();

        document.Close();
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        if (text.Length <= width)
        {
            yield return text;
            yield break;
        }

        for (var i = 0; i < text.Length; i += width)
            yield return text.Substring(i, Math.Min(width, text.Length - i));
    }

    private static string ToPdfSafe(string text)
        => new(text.Select(ch => ch >= 32 && ch <= 126 ? ch : '?').ToArray());
}
