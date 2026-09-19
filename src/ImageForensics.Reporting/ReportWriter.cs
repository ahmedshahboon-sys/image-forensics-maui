using System.Globalization;
using System.Text;
using System.Text.Json;
using ImageForensics.Core.Models;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace ImageForensics.Reporting;

public sealed class ReportWriter
{
    private const string SchemaVersion = "1.0";
    private const string ReportType = "image-forensics-scan";

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            WriteIndented = true
        };

    private static readonly IReadOnlyDictionary<string, string> ConfidenceScale =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(ForensicConfidence.Confirmed)] =
                "Directly observed or deterministically parsed from this file. It does not automatically prove authenticity or intent.",
            [nameof(ForensicConfidence.Likely)] =
                "Strong supporting evidence is present, but alternative explanations remain possible.",
            [nameof(ForensicConfidence.Possible)] =
                "A heuristic indicator is present. It requires corroboration and is not proof by itself.",
            [nameof(ForensicConfidence.Unknown)] =
                "Descriptive or incomplete result whose forensic significance cannot be established from this check alone."
        };

    private static readonly IReadOnlyList<string> GlobalLimitations =
        new[]
        {
            "Probabilistic forensic indicators are not final proof of authenticity, manipulation, source, person identity or location.",
            "Metadata can be added, removed or rewritten by ordinary software and transport services.",
            "Missing metadata does not identify which application or service processed the image.",
            "ELA, compression, resampling, noise, LSB and copy-move signals are investigative indicators and require corroboration.",
            "Precise location is reported only when explicit GPS metadata is present; it is not inferred from faces or backgrounds."
        };

    public string ToJson(ScanReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var envelope =
            new ReportEnvelope(
                SchemaVersion,
                ReportType,
                DateTimeOffset.UtcNow,
                report.AppVersion,
                report.Identity.Sha256,
                GetAlgorithms(report),
                ConfidenceScale,
                GlobalLimitations,
                report);

        return JsonSerializer.Serialize(
            envelope,
            JsonOptions);
    }

    public string ToText(ScanReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb =
            new StringBuilder();

        AppendTitle(sb);
        AppendSummary(sb, report);
        AppendConfirmedFacts(sb, report);
        AppendMetadata(sb, report);
        AppendGps(sb, report);
        AppendCameraDevice(sb, report);
        AppendStructure(sb, report);
        AppendCompression(sb, report);
        AppendForensicIndicators(sb, report);
        AppendHiddenData(sb, report);
        AppendOcrQr(sb, report);
        AppendPrivacy(sb, report);
        AppendConfidenceAndLimitations(sb);

        return sb.ToString();
    }

    public string ComparisonToJson(
        ImageComparisonResult result)
        => JsonSerializer.Serialize(
            result,
            JsonOptions);

    public string ComparisonToText(
        ImageComparisonResult result)
    {
        var sb =
            new StringBuilder();

        sb.AppendLine(
            "IMAGE COMPARISON LAB / مختبر مقارنة الصور");
        sb.AppendLine(
            $"Exact SHA-256 match: {result.ExactMatch}");
        sb.AppendLine(
            $"Left SHA-256: {result.LeftSha256}");
        sb.AppendLine(
            $"Right SHA-256: {result.RightSha256}");
        sb.AppendLine(
            $"Dimensions: {result.LeftWidth}x{result.LeftHeight} vs {result.RightWidth}x{result.RightHeight}");
        sb.AppendLine(
            $"Aspect ratios: {result.LeftAspectRatio:F6} vs {result.RightAspectRatio:F6}");
        sb.AppendLine(
            $"Scale X/Y: {result.ScaleX:F6} / {result.ScaleY:F6}");
        sb.AppendLine(
            $"Dimension relation: {result.DimensionRelation}");
        sb.AppendLine(
            $"Uniform resize candidate: {result.UniformResizeCandidate}");
        sb.AppendLine(
            $"Center-crop candidate: {result.CenterCropCandidate}");
        sb.AppendLine(
            $"aHash similarity: {result.AHashSimilarity:P2}");
        sb.AppendLine(
            $"dHash similarity: {result.DHashSimilarity:P2}");
        sb.AppendLine(
            $"pHash similarity: {result.PHashSimilarity:P2}");
        sb.AppendLine(
            $"Normalized RGB similarity: {result.PixelMetrics.NormalizedRgbSimilarity:P2}");
        sb.AppendLine(
            $"Center-crop similarity: {result.PixelMetrics.CenterCropSimilarity:P2}");
        sb.AppendLine(
            $"Pixel MAE: {result.PixelMetrics.MeanAbsoluteError:F4}");
        sb.AppendLine(
            $"Pixel RMSE: {result.PixelMetrics.RootMeanSquareError:F4}");
        sb.AppendLine(
            $"PSNR: {(result.PixelMetrics.PsnrDb?.ToString("F2", CultureInfo.InvariantCulture) ?? "identical/infinite")} dB");
        sb.AppendLine(
            $"JPEG quality estimate: {result.LeftEstimatedJpegQuality?.ToString("F1", CultureInfo.InvariantCulture) ?? "n/a"} vs {result.RightEstimatedJpegQuality?.ToString("F1", CultureInfo.InvariantCulture) ?? "n/a"}");
        sb.AppendLine(
            $"Chroma subsampling: {result.LeftChromaSubsampling ?? "n/a"} vs {result.RightChromaSubsampling ?? "n/a"}");
        sb.AppendLine(
            $"Metadata differences: {result.MetadataDifferences.Count}");
        sb.AppendLine(
            $"ICC differences: {result.IccDifferences.Count}");

        if (result.MetadataDifferences.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(
                "METADATA DIFFERENCES / اختلافات Metadata");

            foreach (var diff in result.MetadataDifferences.Take(250))
            {
                sb.AppendLine(
                    $"- [{diff.Directory}] {diff.Tag}: left={diff.LeftValue ?? "<missing>"} | right={diff.RightValue ?? "<missing>"}");
            }
        }

        sb.AppendLine();
        sb.AppendLine(
            "COMPARISON INDICATORS / مؤشرات المقارنة");

        foreach (var indicator in result.Indicators)
            AppendEvidenceItem(sb, indicator);

        sb.AppendLine();
        sb.AppendLine(
            "LIMITATION / القيد: similarity, resize, crop and compression indicators are comparative heuristics. They do not prove authenticity, editing history, common source, or direction of derivation.");

        return sb.ToString();
    }

    public string BatchToJson(
        IEnumerable<BatchReportRow> rows)
        => JsonSerializer.Serialize(
            rows,
            JsonOptions);

    public string BatchToCsv(
        IEnumerable<BatchReportRow> rows)
    {
        static string Q(string? s)
            => "\"" +
               (s ?? string.Empty)
                   .Replace(
                       "\"",
                       "\"\"") +
               "\"";

        var sb =
            new StringBuilder(
                "FileName,SHA256,DetectedType,SizeBytes,Width,Height,AspectRatio,HasGps,PrivacyRiskCount,IndicatorCount,BarcodeCount,OcrCharacterCount,AHash,DHash,PHash,DuplicateOf,NearDuplicateOf,Error\n");

        foreach (var r in rows)
        {
            sb.AppendLine(
                string.Join(
                    ",",
                    Q(r.FileName),
                    Q(r.Sha256),
                    Q(r.DetectedType),
                    r.SizeBytes,
                    r.Width,
                    r.Height,
                    r.AspectRatio.ToString(
                        "F6",
                        CultureInfo.InvariantCulture),
                    r.HasGps,
                    r.PrivacyRiskCount,
                    r.IndicatorCount,
                    r.BarcodeCount,
                    r.OcrCharacterCount,
                    Q(r.AHash),
                    Q(r.DHash),
                    Q(r.PHash),
                    Q(r.DuplicateOf),
                    Q(r.NearDuplicateOf),
                    Q(r.Error)));
        }

        return sb.ToString();
    }

    public void WritePdf(
        ScanReport report,
        string outputPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                outputPath) ??
            ".");

        using var stream =
            File.Create(
                outputPath);

        using var document =
            SKDocument.CreatePdf(
                stream)
            ?? throw new InvalidOperationException(
                "Unable to create PDF.");

        using var typeface =
            LoadPdfTypeface();

        using var shaper =
            new SKShaper(
                typeface);

        using var bodyFont =
            new SKFont(
                typeface,
                9.5f);

        using var titleFont =
            new SKFont(
                typeface,
                15f);

        using var bodyPaint =
            new SKPaint
            {
                IsAntialias = true,
                Color = SKColors.Black
            };

        using var mutedPaint =
            new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(
                    80,
                    80,
                    80)
            };

        var lines =
            ToText(report)
                .Replace(
                    "\r",
                    string.Empty,
                    StringComparison.Ordinal)
                .Split(
                    '\n');

        const float pageWidth = 595;
        const float pageHeight = 842;
        const float margin = 36;
        const float bodyLineHeight = 14;
        const float titleLineHeight = 22;
        const float contentWidth =
            pageWidth -
            margin * 2;

        SKCanvas? canvas = null;
        float y = margin;
        var pageNumber = 0;

        void BeginPage()
        {
            if (canvas is not null)
                document.EndPage();

            canvas =
                document.BeginPage(
                    pageWidth,
                    pageHeight);

            pageNumber++;
            y = margin;

            DrawHeader(
                canvas,
                shaper,
                bodyFont,
                mutedPaint,
                report,
                pageNumber,
                pageWidth,
                margin);

            y += 18;
        }

        BeginPage();

        for (var lineIndex = 0;
             lineIndex < lines.Length;
             lineIndex++)
        {
            var raw =
                lines[lineIndex];

            var isReportTitle =
                lineIndex == 0;

            var isSection =
                IsSectionHeading(
                    raw);

            var font =
                isReportTitle ||
                isSection
                    ? titleFont
                    : bodyFont;

            var paint =
                isReportTitle ||
                isSection
                    ? bodyPaint
                    : bodyPaint;

            var lineHeight =
                isReportTitle ||
                isSection
                    ? titleLineHeight
                    : bodyLineHeight;

            if (string.IsNullOrWhiteSpace(raw))
            {
                y +=
                    lineHeight *
                    0.55f;

                continue;
            }

            foreach (var piece in WrapShaped(
                         raw,
                         shaper,
                         font,
                         contentWidth))
            {
                if (y >
                    pageHeight -
                    margin -
                    20)
                {
                    BeginPage();
                }

                var rtl =
                    ContainsArabic(
                        piece);

                var x =
                    rtl
                        ? pageWidth - margin
                        : margin;

                var align =
                    rtl
                        ? SKTextAlign.Right
                        : SKTextAlign.Left;

                canvas!.DrawShapedText(
                    shaper,
                    piece,
                    x,
                    y,
                    align,
                    font,
                    paint);

                y +=
                    lineHeight;
            }
        }

        if (canvas is not null)
            document.EndPage();

        document.Close();
    }

    private static SKTypeface LoadPdfTypeface()
    {
        var assembly =
            typeof(ReportWriter)
                .Assembly;

        var resourceName =
            assembly
                .GetManifestResourceNames()
                .SingleOrDefault(
                    name =>
                        name.EndsWith(
                            "NotoSansArabic-Regular.ttf",
                            StringComparison.Ordinal));

        if (resourceName is null)
        {
            throw new InvalidOperationException(
                "Embedded Arabic report font is missing.");
        }

        using var stream =
            assembly
                .GetManifestResourceStream(
                    resourceName)
            ?? throw new InvalidOperationException(
                "Unable to open embedded Arabic report font.");

        return SKTypeface.FromStream(
                   stream)
               ?? throw new InvalidOperationException(
                   "Unable to load embedded Arabic report font.");
    }

    private static void AppendTitle(
        StringBuilder sb)
    {
        sb.AppendLine(
            "IMAGE FORENSICS / PHOTO INSPECTOR — تقرير تحليل الصور");
    }

    private static void AppendSummary(
        StringBuilder sb,
        ScanReport report)
    {
        Section(
            sb,
            "1. SUMMARY / الملخص");

        sb.AppendLine(
            $"App version / إصدار التطبيق: {report.AppVersion}");
        sb.AppendLine(
            $"Scan time UTC / وقت الفحص: {report.ScannedAtUtc:O}");
        sb.AppendLine(
            $"File / الملف: {report.Identity.FileName}");
        sb.AppendLine(
            $"Size / الحجم: {report.Identity.SizeBytes} bytes");
        sb.AppendLine(
            $"Detected type / النوع المكتشف: {report.Identity.DetectedType} ({report.Identity.DetectedMime})");
        sb.AppendLine(
            $"SHA-256: {report.Identity.Sha256}");

        sb.AppendLine(
            "Algorithms / الخوارزميات:");

        foreach (var algorithm in GetAlgorithms(report))
            sb.AppendLine(
                $"- {algorithm}");
    }

    private static void AppendConfirmedFacts(
        StringBuilder sb,
        ScanReport report)
    {
        Section(
            sb,
            "2. CONFIRMED FACTS / الحقائق المؤكدة");

        Confirmed(
            sb,
            "file.signature",
            $"Magic/signature type: {report.Identity.DetectedType}; signature={report.Identity.SignatureHex}",
            "Direct byte signature inspection.");

        Confirmed(
            sb,
            "file.hash.sha256",
            $"SHA-256: {report.Identity.Sha256}",
            "Cryptographic hash of the complete file bytes.");

        Confirmed(
            sb,
            "file.hash.sha1",
            $"SHA-1: {report.Identity.Sha1}",
            "Comparison hash only; weaker than SHA-256.");

        Confirmed(
            sb,
            "file.hash.md5",
            $"MD5: {report.Identity.Md5}",
            "Comparison hash only; not suitable as a modern collision-resistant authenticity proof.");

        Confirmed(
            sb,
            "file.crc32",
            $"CRC32: {report.Identity.Crc32}",
            "Integrity/checksum helper, not a cryptographic authenticity proof.");

        if (report.Technical is not null)
        {
            Confirmed(
                sb,
                "image.dimensions",
                $"Decoded dimensions: {report.Technical.Width}x{report.Technical.Height}; frames={report.Technical.FrameCount}; orientation={report.Technical.Orientation}",
                "Reported by the image decoder for this file.");
        }

        foreach (var indicator in report.Indicators
                     .Where(i =>
                         i.Confidence ==
                         ForensicConfidence.Confirmed))
        {
            AppendEvidenceItem(
                sb,
                indicator);
        }
    }

    private static void AppendMetadata(
        StringBuilder sb,
        ScanReport report)
    {
        Section(
            sb,
            "3. METADATA");

        if (report.Metadata is null)
        {
            sb.AppendLine(
                "No deep metadata result / لا توجد نتيجة Metadata عميقة.");
            return;
        }

        sb.AppendLine(
            $"Fields / عدد الحقول: {report.Metadata.Fields.Count}");

        foreach (var field in report.Metadata.Fields)
        {
            sb.AppendLine(
                $"[{field.Directory}] {field.Tag}");
            sb.AppendLine(
                $"  Parsed: {field.ParsedValue}");
            sb.AppendLine(
                $"  Raw: {field.RawValue}");
            sb.AppendLine(
                $"  Meaning: {field.Meaning}");
            sb.AppendLine(
                $"  Confidence: {field.Confidence}");
            sb.AppendLine(
                $"  Source: {field.Source}");
        }

        foreach (var error in report.Metadata.Errors)
        {
            sb.AppendLine(
                $"Metadata warning | Confidence={ForensicConfidence.Unknown}: {error}");
        }
    }

    private static void AppendGps(
        StringBuilder sb,
        ScanReport report)
    {
        Section(
            sb,
            "4. GPS");

        var gps =
            report.Metadata?.Gps;

        if (gps is null)
        {
            sb.AppendLine(
                "No explicit parsed GPS coordinates / لا توجد إحداثيات GPS صريحة قابلة للقراءة.");
            sb.AppendLine(
                $"Confidence: {ForensicConfidence.Confirmed}");
            sb.AppendLine(
                "Limitation: absence of parsed GPS does not prove that the file was processed by any particular app or service.");
            return;
        }

        sb.AppendLine(
            $"Coordinates: {gps.Latitude:F8}, {gps.Longitude:F8}");
        sb.AppendLine(
            $"Confidence: {ForensicConfidence.Confirmed}");
        sb.AppendLine(
            "Evidence: explicit GPS metadata parsed from this file.");

        if (gps.Altitude is not null)
            sb.AppendLine(
                $"Altitude: {gps.Altitude:F2}");

        if (gps.Speed is not null)
            sb.AppendLine(
                $"Speed: {gps.Speed:F2}");

        if (gps.Direction is not null)
            sb.AppendLine(
                $"Direction: {gps.Direction:F2}");

        if (!string.IsNullOrWhiteSpace(
                gps.Timestamp))
        {
            sb.AppendLine(
                $"GPS timestamp: {gps.Timestamp}");
        }

        sb.AppendLine(
            "Limitation: coordinates report what is stored in metadata; this report does not independently verify where or when the image was captured.");
    }

    private static void AppendCameraDevice(
        StringBuilder sb,
        ScanReport report)
    {
        Section(
            sb,
            "5. CAMERA / DEVICE — الكاميرا والجهاز");

        if (report.Metadata is null)
        {
            sb.AppendLine(
                "No metadata result.");
            return;
        }

        var cameraFields =
            report.Metadata.Fields
                .Where(f =>
                    f.Tag.Equals(
                        "Make",
                        StringComparison.OrdinalIgnoreCase) ||
                    f.Tag.Equals(
                        "Model",
                        StringComparison.OrdinalIgnoreCase) ||
                    f.Tag.Contains(
                        "Lens",
                        StringComparison.OrdinalIgnoreCase) ||
                    f.Tag.Contains(
                        "Serial",
                        StringComparison.OrdinalIgnoreCase) ||
                    f.Tag.Equals(
                        "Software",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (cameraFields.Length == 0)
        {
            sb.AppendLine(
                "No camera/device fields were parsed.");
            return;
        }

        foreach (var field in cameraFields)
        {
            sb.AppendLine(
                $"- [{field.Confidence}] {field.Tag}: {field.ParsedValue ?? field.RawValue}");
            sb.AppendLine(
                $"  Source: [{field.Directory}] {field.Source}");
        }

        sb.AppendLine(
            "Limitation: camera/device/software metadata can be rewritten and is not proof of physical source ownership or authenticity.");
    }

    private static void AppendStructure(
        StringBuilder sb,
        ScanReport report)
    {
        Section(
            sb,
            "6. FILE STRUCTURE / بنية الملف");

        if (report.Container is null)
        {
            sb.AppendLine(
                "No container structure result.");
            return;
        }

        sb.AppendLine(
            $"Format: {report.Container.Format}");
        sb.AppendLine(
            $"Trailing bytes: {report.Container.TrailingBytes}");
        sb.AppendLine(
            $"Confidence: {ForensicConfidence.Confirmed}");

        foreach (var segment in report.Container.Segments)
        {
            sb.AppendLine(
                $"- offset={segment.Offset}; size={segment.Length}; type={segment.Type}; suspicious={segment.Suspicious}; {segment.Description}");
        }

        foreach (var warning in report.Container.Warnings)
        {
            sb.AppendLine(
                $"Structure warning | Confidence={ForensicConfidence.Likely}: {warning}");
        }
    }

    private static void AppendCompression(
        StringBuilder sb,
        ScanReport report)
    {
        Section(
            sb,
            "7. COMPRESSION / الضغط");

        if (report.Technical is not null)
        {
            sb.AppendLine(
                $"Encoded format: {report.Technical.EncodedFormat}");
            sb.AppendLine(
                $"Compression: {report.Technical.Compression}");
            sb.AppendLine(
                $"Confidence: {ForensicConfidence.Confirmed}");
        }

        if (report.ImageHeuristics is null)
        {
            sb.AppendLine(
                "No deep pixel/JPEG heuristic metrics.");
            return;
        }

        var h =
            report.ImageHeuristics;

        sb.AppendLine(
            $"JPEG quality estimate: {h.EstimatedJpegQuality?.ToString("F1", CultureInfo.InvariantCulture) ?? "n/a"} | Confidence={ForensicConfidence.Possible}");
        sb.AppendLine(
            $"Chroma subsampling: {h.ChromaSubsampling ?? "n/a"} | Confidence={ForensicConfidence.Confirmed}");
        sb.AppendLine(
            $"JPEG quantization tables: {h.JpegQuantizationTableCount}");
        sb.AppendLine(
            $"JPEG quantization mean: {h.JpegQuantizationMean?.ToString("F2", CultureInfo.InvariantCulture) ?? "n/a"}");
        sb.AppendLine(
            $"8x8 block-boundary ratio: {h.BlockBoundaryRatio:F4}");
        sb.AppendLine(
            $"Grid phase dominance: {h.GridPhaseDominance:F4}");
        sb.AppendLine(
            $"Resampling periodicity score: {h.ResamplingPeriodicityScore:F4}");
        sb.AppendLine(
            $"Noise regional CV: {h.NoiseCoefficientOfVariation:F4}");
        sb.AppendLine(
            $"ELA mean: {h.ElaMeanDifference:F4}");
        sb.AppendLine(
            $"ELA regional CV: {h.ElaRegionalCoefficientOfVariation:F4}");
        sb.AppendLine(
            $"Edge density: {h.EdgeDensity:F4}");
        sb.AppendLine(
            $"Histogram peakiness: {h.HistogramPeakiness:F4}");
        sb.AppendLine(
            $"Clone/copy-move tile candidates: {h.CloneCandidatePairs}");
        sb.AppendLine(
            "Limitation: JPEG-quality, ELA, resampling, noise and copy-move metrics are heuristic unless separately reported as deterministic container facts.");
    }

    private static void AppendForensicIndicators(
        StringBuilder sb,
        ScanReport report)
    {
        Section(
            sb,
            "8. FORENSIC INDICATORS / المؤشرات الجنائية الرقمية");

        if (report.Indicators.Count == 0)
        {
            sb.AppendLine(
                "No forensic indicators were emitted.");
            return;
        }

        foreach (var indicator in report.Indicators)
            AppendEvidenceItem(
                sb,
                indicator);
    }

    private static void AppendHiddenData(
        StringBuilder sb,
        ScanReport report)
    {
        Section(
            sb,
            "9. HIDDEN-DATA INDICATORS / مؤشرات البيانات المخفية");

        if (report.HiddenData is null ||
            report.HiddenData.Count == 0)
        {
            sb.AppendLine(
                "No hidden-data findings were emitted.");
        }
        else
        {
            foreach (var finding in report.HiddenData.Take(100))
            {
                sb.AppendLine(
                    $"- [{finding.Confidence}] offset={finding.Offset}; {finding.Kind}");
                sb.AppendLine(
                    $"  Evidence: {finding.Evidence}");
                sb.AppendLine(
                    $"  Limitation: {finding.Limitation}");
            }
        }

        if (report.Steganography is not null)
        {
            var s =
                report.Steganography;

            sb.AppendLine(
                $"LSB sampled pixels: {s.SampledPixels}");
            sb.AppendLine(
                $"LSB one-ratio R/G/B: {s.RedLsbOneRatio:F5} / {s.GreenLsbOneRatio:F5} / {s.BlueLsbOneRatio:F5}");
            sb.AppendLine(
                $"LSB transition rate: {s.LsbTransitionRate:F5}");
            sb.AppendLine(
                $"Bit-plane binary entropy 0..7: {string.Join(", ", s.BitPlaneEntropies.Select(x => x.ToString("F5", CultureInfo.InvariantCulture)))}");
            sb.AppendLine(
                $"Confidence: {ForensicConfidence.Unknown}");
            sb.AppendLine(
                "Limitation: random-looking LSBs and high entropy can arise naturally from noise, compression and image processing.");
        }
    }

    private static void AppendOcrQr(
        StringBuilder sb,
        ScanReport report)
    {
        Section(
            sb,
            "10. OCR / QR / BARCODE");

        if (report.Ocr is null)
        {
            sb.AppendLine(
                "No OCR result.");
        }
        else
        {
            sb.AppendLine(
                $"OCR languages: {report.Ocr.Languages}");
            sb.AppendLine(
                $"OCR succeeded: {report.Ocr.Succeeded}");
            sb.AppendLine(
                $"OCR engine confidence: {report.Ocr.Confidence:F2}");
            sb.AppendLine(
                "Forensic confidence: Unknown — OCR confidence is recognition confidence, not authenticity confidence.");

            if (!string.IsNullOrWhiteSpace(
                    report.Ocr.Error))
            {
                sb.AppendLine(
                    $"OCR error: {report.Ocr.Error}");
            }

            if (!string.IsNullOrWhiteSpace(
                    report.Ocr.Text))
            {
                sb.AppendLine(
                    "Recognized text / النص المقروء:");
                sb.AppendLine(
                    report.Ocr.Text);
            }
        }

        foreach (var hit in report.Barcodes)
        {
            sb.AppendLine(
                $"Barcode/QR [{hit.Format}] | Confidence={ForensicConfidence.Likely}: {hit.Text}");
            sb.AppendLine(
                "Limitation: decoded content is displayed as text only and is not automatically opened or trusted.");
        }

        if (report.VisibleTextEntities is { Count: > 0 })
        {
            sb.AppendLine(
                "Visible text entities:");

            foreach (var entity in report.VisibleTextEntities)
            {
                sb.AppendLine(
                    $"- {entity.Kind} [{entity.Source}]: {entity.Value}");
            }
        }
    }

    private static void AppendPrivacy(
        StringBuilder sb,
        ScanReport report)
    {
        Section(
            sb,
            "11. PRIVACY RISKS / مخاطر الخصوصية");

        if (report.Privacy is null ||
            report.Privacy.Risks.Count == 0)
        {
            sb.AppendLine(
                "No configured privacy-risk fields were detected.");
            return;
        }

        foreach (var risk in report.Privacy.Risks)
        {
            sb.AppendLine(
                $"- [{risk.Confidence}] {risk.Title}");
            sb.AppendLine(
                $"  Evidence: {risk.Evidence}");
            sb.AppendLine(
                "  Limitation: presence of sensitive metadata is a privacy observation, not evidence of malicious intent or forgery.");
        }
    }

    private static void AppendConfidenceAndLimitations(
        StringBuilder sb)
    {
        Section(
            sb,
            "12. CONFIDENCE & LIMITATIONS / الثقة والقيود");

        sb.AppendLine(
            "Confidence scale / مقياس الثقة:");

        foreach (var item in ConfidenceScale)
        {
            sb.AppendLine(
                $"- {item.Key}: {item.Value}");
        }

        sb.AppendLine(
            "Global limitations / القيود العامة:");

        foreach (var limitation in GlobalLimitations)
        {
            sb.AppendLine(
                $"- {limitation}");
        }
    }

    private static IReadOnlyList<string> GetAlgorithms(
        ScanReport report)
    {
        var algorithms =
            new List<string>
            {
                "Magic-byte / file-signature inspection",
                "SHA-256",
                "SHA-1",
                "MD5 (comparison only)",
                "CRC32",
                "Shannon byte entropy",
                "Image decoder technical inspection"
            };

        if (report.Metadata is not null)
            algorithms.Add(
                "EXIF / IPTC / XMP / ICC metadata parsing");

        if (report.Container is not null)
            algorithms.Add(
                "Read-only image-container structure parser");

        if (report.PerceptualHashes is not null)
        {
            algorithms.Add(
                "aHash");
            algorithms.Add(
                "dHash");
            algorithms.Add(
                "pHash");
        }

        if (report.ImageHeuristics is not null)
        {
            algorithms.Add(
                "JPEG quantization / quality estimation");
            algorithms.Add(
                "8x8 block/grid analysis");
            algorithms.Add(
                "resampling periodicity heuristic");
            algorithms.Add(
                "regional noise / histogram / edge heuristics");
            algorithms.Add(
                "ELA regional heuristic");
            algorithms.Add(
                "tile-based copy-move candidate heuristic");
        }

        if (report.Steganography is not null)
        {
            algorithms.Add(
                "RGB LSB balance / transition analysis");
            algorithms.Add(
                "bit-plane binary entropy");
        }

        if (report.HiddenData is not null)
        {
            algorithms.Add(
                "embedded file-signature scan");
            algorithms.Add(
                "printable-string scan");
            algorithms.Add(
                "windowed entropy scan");
        }

        if (report.Ocr is not null)
            algorithms.Add(
                $"Offline OCR ({report.Ocr.Languages})");

        if (report.Barcodes.Count > 0)
            algorithms.Add(
                "Offline QR / barcode decoding");

        return algorithms
            .Distinct(
                StringComparer.Ordinal)
            .ToArray();
    }

    private static void AppendEvidenceItem(
        StringBuilder sb,
        EvidenceItem indicator)
    {
        sb.AppendLine(
            $"- [{indicator.Confidence}] {indicator.Title} ({indicator.Code})");
        sb.AppendLine(
            $"  Detail: {indicator.Detail}");
        sb.AppendLine(
            $"  Evidence: {indicator.Evidence}");
        sb.AppendLine(
            $"  Limitation: {indicator.Limitation}");
    }

    private static void Confirmed(
        StringBuilder sb,
        string code,
        string evidence,
        string limitation)
    {
        sb.AppendLine(
            $"- [{ForensicConfidence.Confirmed}] {code}");
        sb.AppendLine(
            $"  Evidence: {evidence}");
        sb.AppendLine(
            $"  Limitation: {limitation}");
    }

    private static void Section(
        StringBuilder sb,
        string title)
    {
        sb.AppendLine();
        sb.AppendLine(
            title);
        sb.AppendLine(
            new string(
                '-',
                Math.Min(
                    72,
                    title.Length)));
    }

    private static bool IsSectionHeading(
        string line)
    {
        var trimmed =
            line.TrimStart();

        if (trimmed.Length < 3)
            return false;

        var index = 0;

        while (index < trimmed.Length &&
               char.IsDigit(
                   trimmed[index]))
        {
            index++;
        }

        return index > 0 &&
               index < trimmed.Length &&
               trimmed[index] == '.';
    }

    private static IEnumerable<string> WrapShaped(
        string text,
        SKShaper shaper,
        SKFont font,
        float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return string.Empty;
            yield break;
        }

        if (MeasureShaped(
                text,
                shaper,
                font) <=
            maxWidth)
        {
            yield return text;
            yield break;
        }

        var words =
            text.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        if (words.Length <= 1)
        {
            foreach (var piece in HardWrap(
                         text,
                         shaper,
                         font,
                         maxWidth))
            {
                yield return piece;
            }

            yield break;
        }

        var current =
            new StringBuilder();

        foreach (var word in words)
        {
            var candidate =
                current.Length == 0
                    ? word
                    : $"{current} {word}";

            if (MeasureShaped(
                    candidate,
                    shaper,
                    font) <=
                maxWidth)
            {
                current.Clear();
                current.Append(
                    candidate);
                continue;
            }

            if (current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }

            if (MeasureShaped(
                    word,
                    shaper,
                    font) <=
                maxWidth)
            {
                current.Append(
                    word);
            }
            else
            {
                foreach (var piece in HardWrap(
                             word,
                             shaper,
                             font,
                             maxWidth))
                {
                    yield return piece;
                }
            }
        }

        if (current.Length > 0)
            yield return current.ToString();
    }

    private static IEnumerable<string> HardWrap(
        string text,
        SKShaper shaper,
        SKFont font,
        float maxWidth)
    {
        var start = 0;

        while (start < text.Length)
        {
            var low = 1;
            var high =
                text.Length -
                start;
            var best = 1;

            while (low <= high)
            {
                var mid =
                    low +
                    (high - low) /
                    2;

                var candidate =
                    text.Substring(
                        start,
                        mid);

                if (MeasureShaped(
                        candidate,
                        shaper,
                        font) <=
                    maxWidth)
                {
                    best = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            yield return text.Substring(
                start,
                best);

            start +=
                best;
        }
    }

    private static float MeasureShaped(
        string text,
        SKShaper shaper,
        SKFont font)
        => shaper
            .Shape(
                text,
                font)
            .Width;

    private static bool ContainsArabic(
        string text)
        => text.Any(
            ch =>
                ch is >= '\u0600' and <= '\u06FF' ||
                ch is >= '\u0750' and <= '\u077F' ||
                ch is >= '\u08A0' and <= '\u08FF' ||
                ch is >= '\uFB50' and <= '\uFDFF' ||
                ch is >= '\uFE70' and <= '\uFEFF');

    private static void DrawHeader(
        SKCanvas canvas,
        SKShaper shaper,
        SKFont font,
        SKPaint paint,
        ScanReport report,
        int pageNumber,
        float pageWidth,
        float margin)
    {
        var left =
            $"Photo Inspector {report.AppVersion}";

        canvas.DrawShapedText(
            shaper,
            left,
            margin,
            margin,
            SKTextAlign.Left,
            font,
            paint);

        var right =
            $"Page {pageNumber}";

        canvas.DrawShapedText(
            shaper,
            right,
            pageWidth - margin,
            margin,
            SKTextAlign.Right,
            font,
            paint);
    }
}
