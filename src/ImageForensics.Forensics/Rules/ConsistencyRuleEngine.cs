using System.Globalization;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;

namespace ImageForensics.Forensics.Rules;

public sealed class ConsistencyRuleEngine : IConsistencyRuleEngine
{
    public IReadOnlyList<EvidenceItem> Analyze(ForensicAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var findings = new List<EvidenceItem>();

        if (context.Identity.ExtensionMismatch)
        {
            findings.Add(new EvidenceItem(
                "file.extension_mismatch",
                "امتداد الملف لا يطابق التوقيع",
                $"الامتداد {context.Identity.Extension} بينما التوقيع يشير إلى {context.Identity.DetectedType}.",
                ForensicConfidence.Confirmed,
                $"Magic bytes: {context.Identity.SignatureHex}",
                "هذا يثبت عدم التطابق التقني فقط؛ لا يثبت أن الصورة مزورة أو معدلة."));
        }

        if (context.Container.TrailingBytes > 0)
        {
            findings.Add(new EvidenceItem(
                "container.trailing_bytes",
                "بيانات بعد نهاية حاوية الصورة",
                $"وُجد {context.Container.TrailingBytes} بايت بعد علامة النهاية المعروفة للحاوية.",
                ForensicConfidence.Confirmed,
                $"Container={context.Container.Format}; trailing={context.Container.TrailingBytes}",
                "قد تكون البيانات الإضافية غير ضارة أو ناتجة عن أداة حفظ؛ يلزم فحص محتواها قبل أي استنتاج."));
        }

        var software = Find(context.Metadata, "Software");
        if (!string.IsNullOrWhiteSpace(software))
        {
            findings.Add(new EvidenceItem(
                "metadata.software_present",
                "حقل Software موجود",
                $"القيمة المسجلة: {software}",
                ForensicConfidence.Possible,
                "Metadata field: Software",
                "وجود Software قد ينتج عن الهاتف أو الكاميرا أو تطبيق حفظ عادي، وليس دليلًا قطعيًا على التعديل."));
        }

        var exifWidth = TryNumber(Find(context.Metadata, "Exif Image Width"))
                        ?? TryNumber(Find(context.Metadata, "Pixel X Dimension"));
        var exifHeight = TryNumber(Find(context.Metadata, "Exif Image Height"))
                         ?? TryNumber(Find(context.Metadata, "Pixel Y Dimension"));

        if (exifWidth is > 0 && exifHeight is > 0 &&
            (exifWidth.Value != context.Technical.Width || exifHeight.Value != context.Technical.Height))
        {
            findings.Add(new EvidenceItem(
                "metadata.dimension_mismatch",
                "أبعاد Metadata تختلف عن الأبعاد المفكوكة",
                $"Metadata={exifWidth}×{exifHeight}; decoded={context.Technical.Width}×{context.Technical.Height}.",
                ForensicConfidence.Likely,
                "EXIF pixel dimensions versus decoded image dimensions",
                "قد تنتج هذه الحالة عن إعادة التحجيم مع بقاء Metadata قديمة؛ لا تحدد سبب التغيير بمفردها."));
        }

        var original = TryDate(Find(context.Metadata, "Date/Time Original"));
        var digitized = TryDate(Find(context.Metadata, "Date/Time Digitized"));
        if (original is not null && digitized is not null && digitized < original)
        {
            findings.Add(new EvidenceItem(
                "metadata.time_order",
                "ترتيب زمني غير متوقع في Metadata",
                $"DateTimeDigitized ({digitized:O}) أقدم من DateTimeOriginal ({original:O}).",
                ForensicConfidence.Possible,
                "EXIF timestamp comparison",
                "الساعات والمناطق الزمنية وعمليات النسخ أو البرامج قد تغيّر هذه القيم؛ المؤشر احتمالي."));
        }

        return findings;
    }

    private static string? Find(MetadataInspectionResult metadata, string tag)
        => metadata.Fields.FirstOrDefault(x => x.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))?.ParsedValue;

    private static int? TryNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var token = new string(value.TakeWhile(c => char.IsDigit(c)).ToArray());
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static DateTimeOffset? TryDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string[] formats = { "yyyy:MM:dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ssK" };
        foreach (var format in formats)
            if (DateTimeOffset.TryParseExact(value, format, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out var parsed))
                return parsed;

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var fallback)
            ? fallback
            : null;
    }
}
