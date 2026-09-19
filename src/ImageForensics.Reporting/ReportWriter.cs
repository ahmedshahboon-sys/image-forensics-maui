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
        var sb=new StringBuilder();
        sb.AppendLine("IMAGE FORENSICS / PHOTO INSPECTOR REPORT");
        sb.AppendLine($"App version: {report.AppVersion}");
        sb.AppendLine($"Scan time (UTC): {report.ScannedAtUtc:O}");
        sb.AppendLine($"File: {report.Identity.FileName}");
        sb.AppendLine($"SHA-256: {report.Identity.Sha256}");
        sb.AppendLine($"Detected type: {report.Identity.DetectedType}");
        sb.AppendLine($"Size: {report.Identity.SizeBytes} bytes");
        if(report.Technical is not null)
            sb.AppendLine($"Dimensions: {report.Technical.Width}x{report.Technical.Height}; Frames={report.Technical.FrameCount}; Color={report.Technical.ColorType}");
        if(report.Metadata?.Gps is not null)
            sb.AppendLine($"GPS: {report.Metadata.Gps.Latitude:F8}, {report.Metadata.Gps.Longitude:F8}");
        if(report.Container is not null)
            sb.AppendLine($"Container: {report.Container.Format}; trailing={report.Container.TrailingBytes}");
        if(report.PerceptualHashes is not null)
            sb.AppendLine($"aHash={report.PerceptualHashes.AHash}; dHash={report.PerceptualHashes.DHash}; pHash={report.PerceptualHashes.PHash}");
        if(report.ImageHeuristics is not null)
        {
            sb.AppendLine($"JPEG quality estimate: {report.ImageHeuristics.EstimatedJpegQuality?.ToString("F1") ?? "n/a"}");
            sb.AppendLine($"Chroma subsampling: {report.ImageHeuristics.ChromaSubsampling ?? "n/a"}");
            sb.AppendLine($"Block ratio: {report.ImageHeuristics.BlockBoundaryRatio:F4}");
            sb.AppendLine($"Noise CV: {report.ImageHeuristics.NoiseCoefficientOfVariation:F4}");
            sb.AppendLine($"ELA mean: {report.ImageHeuristics.ElaMeanDifference:F4}");
            sb.AppendLine($"Clone tile candidates: {report.ImageHeuristics.CloneCandidatePairs}");
        }
        foreach(var hit in report.Barcodes) sb.AppendLine($"Barcode/QR [{hit.Format}]: {hit.Text}");
        if(report.Privacy is not null)
        {
            sb.AppendLine();
            sb.AppendLine("PRIVACY RISKS");
            foreach(var risk in report.Privacy.Risks) sb.AppendLine($"- [{risk.Confidence}] {risk.Title}: {risk.Evidence}");
        }
        if(report.HiddenData is not null)
        {
            sb.AppendLine();
            sb.AppendLine("HIDDEN-DATA INDICATORS");
            foreach(var h in report.HiddenData.Take(50)) sb.AppendLine($"- [{h.Confidence}] offset={h.Offset} {h.Kind}: {h.Evidence}");
        }
        sb.AppendLine();
        sb.AppendLine("FORENSIC INDICATORS");
        foreach(var i in report.Indicators) sb.AppendLine($"- [{i.Confidence}] {i.Title}: {i.Detail} | Evidence: {i.Evidence} | Limitation: {i.Limitation}");
        sb.AppendLine();
        sb.AppendLine("LIMITATION: probabilistic indicators are not final proof of authenticity, manipulation, source, person identity or location.");
        return sb.ToString();
    }

    public string BatchToCsv(IEnumerable<BatchReportRow> rows)
    {
        static string Q(string? s) => "\"" + (s ?? string.Empty).Replace("\"", "\"\"") + "\"";
        var sb=new StringBuilder("FileName,SHA256,DetectedType,SizeBytes,Width,Height,HasGps,PrivacyRiskCount,IndicatorCount\n");
        foreach(var r in rows)
            sb.AppendLine(string.Join(",",Q(r.FileName),Q(r.Sha256),Q(r.DetectedType),r.SizeBytes,r.Width,r.Height,r.HasGps,r.PrivacyRiskCount,r.IndicatorCount));
        return sb.ToString();
    }

    public void WritePdf(ScanReport report,string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)??".");
        using var stream=File.Create(outputPath);
        using var document=SKDocument.CreatePdf(stream)??throw new InvalidOperationException("Unable to create PDF.");
        using var paint=new SKPaint { IsAntialias=true };
        using var font=new SKFont(SKTypeface.Default,11);
        var lines=ToText(report).Replace("\r","").Split('\n');
        const float pageW=595,pageH=842,margin=36,lineH=15;
        SKCanvas? canvas=null; float y=margin;
        foreach(var raw in lines)
        {
            if(canvas is null || y>pageH-margin)
            {
                if(canvas is not null) document.EndPage();
                canvas=document.BeginPage(pageW,pageH);
                y=margin;
            }
            var line=ToPdfSafe(raw);
            foreach(var piece in Wrap(line,92))
            {
                canvas.DrawText(piece,margin,y,font,paint);
                y+=lineH;
                if(y>pageH-margin) break;
            }
        }
        if(canvas is not null) document.EndPage();
        document.Close();
    }

    private static IEnumerable<string> Wrap(string text,int width)
    {
        if(text.Length<=width){yield return text;yield break;}
        for(var i=0;i<text.Length;i+=width) yield return text.Substring(i,Math.Min(width,text.Length-i));
    }

    private static string ToPdfSafe(string text)
        => new string(text.Select(ch=>ch>=32&&ch<=126?ch:'?').ToArray());
}
