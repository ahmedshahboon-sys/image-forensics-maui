using System.Globalization;
using System.Text.RegularExpressions;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;

namespace ImageForensics.Forensics.Rules;

public sealed class ConsistencyRuleEngine : IConsistencyRuleEngine
{
    public IReadOnlyList<EvidenceItem> Analyze(ForensicAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var findings = new List<EvidenceItem>();

        AddFileIdentityChecks(context, findings);
        AddContainerChecks(context, findings);
        AddMetadataConsistencyChecks(context, findings);
        AddDimensionChecks(context, findings);
        AddTimestampChecks(context, findings);
        AddThumbnailChecks(context, findings);

        return findings;
    }

    private static void AddFileIdentityChecks(ForensicAnalysisContext context, List<EvidenceItem> findings)
    {
        if (!context.Identity.ExtensionMismatch) return;

        findings.Add(new EvidenceItem(
            "file.extension_mismatch",
            "امتداد الملف لا يطابق التوقيع",
            $"الامتداد {context.Identity.Extension} بينما التوقيع يشير إلى {context.Identity.DetectedType}.",
            ForensicConfidence.Confirmed,
            $"Magic bytes: {context.Identity.SignatureHex}",
            "هذا يثبت عدم التطابق التقني فقط؛ قد يكون الملف أُعيدت تسميته يدويًا ولا يثبت أن الصورة مزورة أو معدلة."));
    }

    private static void AddContainerChecks(ForensicAnalysisContext context, List<EvidenceItem> findings)
    {
        if (context.Container.TrailingBytes > 0)
        {
            findings.Add(new EvidenceItem(
                "container.trailing_bytes",
                "بيانات بعد نهاية حاوية الصورة",
                $"وُجد {context.Container.TrailingBytes} بايت بعد علامة النهاية المعروفة للحاوية.",
                ForensicConfidence.Confirmed,
                $"Container={context.Container.Format}; trailing={context.Container.TrailingBytes}",
                "قد تكون البيانات الإضافية غير ضارة أو ناتجة عن أداة حفظ؛ وجودها وحده لا يثبت إخفاء بيانات أو تعديل الصورة."));
        }

        if (context.Container.Warnings.Count > 0)
        {
            findings.Add(new EvidenceItem(
                "container.structure_warning",
                "الحاوية تحتوي تحذيرات بنيوية",
                string.Join(" | ", context.Container.Warnings.Take(5)),
                ForensicConfidence.Likely,
                $"Parser emitted {context.Container.Warnings.Count} structural warning(s).",
                "ملفات تالفة جزئيًا أو برامج ترميز غير معتادة قد تنتج تحذيرات مشابهة دون وجود تلاعب متعمد."));
        }
    }

    private static void AddMetadataConsistencyChecks(ForensicAnalysisContext context, List<EvidenceItem> findings)
    {
        AddConflictingValues(context.Metadata, findings, "Make", "metadata.make_conflict", "تعارض في الشركة/الصانع");
        AddConflictingValues(context.Metadata, findings, "Model", "metadata.model_conflict", "تعارض في موديل الجهاز");
        AddConflictingValues(context.Metadata, findings, "Lens Model", "metadata.lens_conflict", "تعارض في معلومات العدسة");
        AddConflictingValues(context.Metadata, findings, "Software", "metadata.software_conflict", "تعارض في حقل Software");

        var softwareValues = Values(context.Metadata, "Software");
        if (softwareValues.Count == 1)
        {
            findings.Add(new EvidenceItem(
                "metadata.software_present",
                "حقل Software موجود",
                $"القيمة المسجلة: {softwareValues[0]}",
                ForensicConfidence.Confirmed,
                "Metadata field Software is present in the file.",
                "وجود اسم برنامج يثبت وجود الحقل فقط؛ الهاتف أو الكاميرا أو تطبيق حفظ عادي قد يكتب هذا الحقل، ولا يثبت التزوير."));
        }

        var serialValues = context.Metadata.Fields
            .Where(f => f.Tag.Contains("Serial", StringComparison.OrdinalIgnoreCase))
            .Select(ValueOf)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (serialValues.Length > 1)
        {
            findings.Add(new EvidenceItem(
                "metadata.serial_conflict",
                "قيم Serial متعددة في Metadata",
                string.Join(" | ", serialValues),
                ForensicConfidence.Possible,
                "More than one distinct serial-like metadata value is present.",
                "قد تخص القيم جسم الكاميرا والعدسة أو مكونات مختلفة؛ لا تعتبر تعارضًا مؤكدًا إلا بعد فهم اسم كل حقل ومصدره."));
        }

        var metadataOrientation = NormalizeOrientation(Find(context.Metadata, "Orientation"));
        var decodedOrientation = NormalizeOrientation(context.Technical.Orientation);

        if (metadataOrientation is not null &&
            decodedOrientation is not null &&
            !string.Equals(metadataOrientation, decodedOrientation, StringComparison.Ordinal))
        {
            findings.Add(new EvidenceItem(
                "metadata.orientation_mismatch",
                "الاتجاه المسجل لا يتطابق مع اتجاه الـdecoder",
                $"Metadata={metadataOrientation}; decoded={decodedOrientation}.",
                ForensicConfidence.Likely,
                "EXIF orientation and decoder-reported encoded origin resolve to different orientations.",
                "قد تنتج الحالة عن برنامج طبّق الدوران على البكسلات ولم يحدّث Metadata أو العكس؛ لا تحدد سبب الاختلاف بمفردها."));
        }
    }

    private static void AddDimensionChecks(ForensicAnalysisContext context, List<EvidenceItem> findings)
    {
        var exifWidth = TryNumber(Find(context.Metadata, "Exif Image Width"))
                        ?? TryNumber(Find(context.Metadata, "Pixel X Dimension"));
        var exifHeight = TryNumber(Find(context.Metadata, "Exif Image Height"))
                         ?? TryNumber(Find(context.Metadata, "Pixel Y Dimension"));

        if (exifWidth is > 0 &&
            exifHeight is > 0 &&
            (exifWidth.Value != context.Technical.Width ||
             exifHeight.Value != context.Technical.Height))
        {
            findings.Add(new EvidenceItem(
                "metadata.dimension_mismatch",
                "أبعاد Metadata تختلف عن الأبعاد المفكوكة",
                $"Metadata={exifWidth}×{exifHeight}; decoded={context.Technical.Width}×{context.Technical.Height}.",
                ForensicConfidence.Likely,
                "EXIF pixel dimensions versus decoded image dimensions.",
                "قد تنتج الحالة عن إعادة التحجيم مع بقاء Metadata قديمة؛ لا تحدد سبب التغيير بمفردها."));
        }

        if (context.Technical.Width <= 2 || context.Technical.Height <= 2)
        {
            findings.Add(new EvidenceItem(
                "image.extreme_small_dimension",
                "أحد أبعاد الصورة صغير جدًا",
                $"{context.Technical.Width}×{context.Technical.Height}",
                ForensicConfidence.Confirmed,
                "Decoded image dimensions contain an axis of two pixels or fewer.",
                "هذا وصف بنيوي غير اعتيادي فقط، وقد يكون مشروعًا في ملفات اختبار أو أصول واجهة صغيرة."));
        }

        var ratio = context.Technical.AspectRatio;
        if (ratio > 20 || ratio < 0.05)
        {
            findings.Add(new EvidenceItem(
                "image.extreme_aspect_ratio",
                "نسبة أبعاد شديدة التطرف",
                $"Aspect ratio={ratio:F4}; dimensions={context.Technical.Width}×{context.Technical.Height}.",
                ForensicConfidence.Confirmed,
                "The decoded width/height ratio is outside the conservative 0.05–20 range.",
                "الصور البانورامية أو الشرائط أو الصور العلمية قد تكون شرعية؛ هذا لا يثبت التعديل."));
        }
    }

    private static void AddTimestampChecks(ForensicAnalysisContext context, List<EvidenceItem> findings)
    {
        var original = TryDate(Find(context.Metadata, "Date/Time Original")
                               ?? Find(context.Metadata, "DateTimeOriginal"));
        var digitized = TryDate(Find(context.Metadata, "Date/Time Digitized")
                                ?? Find(context.Metadata, "DateTimeDigitized"));
        var modified = TryDate(Find(context.Metadata, "Date/Time")
                               ?? Find(context.Metadata, "Modify Date")
                               ?? Find(context.Metadata, "ModifyDate"));

        if (original is not null && digitized is not null && digitized < original)
        {
            findings.Add(new EvidenceItem(
                "metadata.time_digitized_before_original",
                "ترتيب زمني غير متوقع في Metadata",
                $"Digitized={digitized:O}; Original={original:O}.",
                ForensicConfidence.Possible,
                "Digitized timestamp parses earlier than original-capture timestamp.",
                "اختلاف المنطقة الزمنية أو ساعة الجهاز أو برامج النقل والتحرير قد يفسر الفرق؛ لا يعتبر دليل تزوير."));
        }

        if (original is not null && modified is not null && modified < original)
        {
            findings.Add(new EvidenceItem(
                "metadata.time_modified_before_original",
                "وقت التعديل أقدم من وقت الالتقاط المسجل",
                $"Modified={modified:O}; Original={original:O}.",
                ForensicConfidence.Possible,
                "Metadata modification timestamp parses earlier than the recorded original timestamp.",
                "الحقول قد تُكتب بمناطق زمنية أو ساعات جهاز مختلفة، وقد تُنسخ من ملف آخر؛ هذا المؤشر لا يعتبر دليل تزوير."));
        }

        AddConflictingTimestampText(
            context.Metadata,
            findings,
            new[] { "Date/Time Original", "DateTimeOriginal" },
            "metadata.original_time_conflict",
            "قيم متعددة لوقت الالتقاط الأصلي");
    }

    private static void AddThumbnailChecks(ForensicAnalysisContext context, List<EvidenceItem> findings)
    {
        var thumbnailFields = context.Metadata.Fields
            .Where(f => f.Directory.Contains("Thumbnail", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (thumbnailFields.Length == 0) return;

        var thumbWidth = thumbnailFields
            .Where(f => f.Tag.Contains("Width", StringComparison.OrdinalIgnoreCase))
            .Select(f => TryNumber(ValueOf(f)))
            .FirstOrDefault(v => v is > 0);

        var thumbHeight = thumbnailFields
            .Where(f => f.Tag.Contains("Height", StringComparison.OrdinalIgnoreCase))
            .Select(f => TryNumber(ValueOf(f)))
            .FirstOrDefault(v => v is > 0);

        if (thumbWidth is > 0 && thumbHeight is > 0)
        {
            var thumbRatio = (double)thumbWidth.Value / thumbHeight.Value;
            var mainRatio = context.Technical.AspectRatio;
            var relativeDifference = Math.Abs(thumbRatio - mainRatio) / Math.Max(0.0001, mainRatio);

            if (relativeDifference > 0.15)
            {
                findings.Add(new EvidenceItem(
                    "metadata.thumbnail_aspect_mismatch",
                    "نسبة أبعاد الـthumbnail تختلف عن الصورة الرئيسية",
                    $"Thumbnail={thumbWidth}×{thumbHeight} ({thumbRatio:F3}); main={context.Technical.Width}×{context.Technical.Height} ({mainRatio:F3}).",
                    ForensicConfidence.Possible,
                    $"Relative aspect-ratio difference={relativeDifference:P1}.",
                    "بعض الكاميرات والبرامج تستخدم thumbnails مقصوصة أو مختلفة عمدًا؛ يلزم فحص الصورة المصغرة نفسها قبل أي استنتاج أقوى."));
            }
        }
    }

    private static void AddConflictingValues(
        MetadataInspectionResult metadata,
        List<EvidenceItem> findings,
        string tag,
        string code,
        string title)
    {
        var values = Values(metadata, tag);
        if (values.Count <= 1) return;

        findings.Add(new EvidenceItem(
            code,
            title,
            string.Join(" | ", values),
            ForensicConfidence.Likely,
            $"The metadata contains {values.Count} distinct values for tag '{tag}'.",
            "قد تأتي القيم من IFD رئيسي وthumbnail أو مسارات Metadata متعددة؛ يجب مراجعة Directory لكل قيمة قبل تفسير التعارض."));
    }

    private static void AddConflictingTimestampText(
        MetadataInspectionResult metadata,
        List<EvidenceItem> findings,
        IReadOnlyList<string> tags,
        string code,
        string title)
    {
        var values = metadata.Fields
            .Where(f => tags.Any(t => f.Tag.Equals(t, StringComparison.OrdinalIgnoreCase)))
            .Select(ValueOf)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (values.Length <= 1) return;

        findings.Add(new EvidenceItem(
            code,
            title,
            string.Join(" | ", values),
            ForensicConfidence.Possible,
            "The same logical capture-time field appears with multiple distinct text values.",
            "اختلاف الصيغ أو المناطق الزمنية أو نسخ Metadata بين IFDs قد يسبب اختلافًا مشروعًا."));
    }

    private static List<string> Values(MetadataInspectionResult metadata, string tag)
        => metadata.Fields
            .Where(f => f.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
            .Select(ValueOf)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string? Find(MetadataInspectionResult metadata, string tag)
        => metadata.Fields
            .FirstOrDefault(x => x.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
            is { } field
                ? ValueOf(field)
                : null;

    private static string? ValueOf(MetadataField field)
        => !string.IsNullOrWhiteSpace(field.ParsedValue)
            ? field.ParsedValue
            : field.RawValue;

    private static int? TryNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var match = Regex.Match(value, @"[-+]?\d+");
        return match.Success &&
               int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;
    }

    private static DateTimeOffset? TryDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        string[] formats =
        {
            "yyyy:MM:dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssK",
            "yyyy:MM:dd HH:mm:ssK"
        };

        foreach (var format in formats)
        {
            if (DateTimeOffset.TryParseExact(
                    value.Trim(),
                    format,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var parsed))
                return parsed;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var fallback)
            ? fallback
            : null;
    }

    private static string? NormalizeOrientation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var v = value.Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace(",", string.Empty)
            .ToLowerInvariant();

        if (v.Contains("topleft") || v.Contains("horizontal/normal") || v.Contains("normal"))
            return "TopLeft";
        if (v.Contains("topright") || v.Contains("mirrorhorizontal"))
            return "TopRight";
        if (v.Contains("bottomright") || v.Contains("rotate180"))
            return "BottomRight";
        if (v.Contains("bottomleft") || v.Contains("mirrorvertical"))
            return "BottomLeft";
        if (v.Contains("lefttop") || v.Contains("mirrorhorizontalandrotate270"))
            return "LeftTop";
        if (v.Contains("righttop") || v.Contains("rotate90cw"))
            return "RightTop";
        if (v.Contains("rightbottom") || v.Contains("mirrorhorizontalandrotate90"))
            return "RightBottom";
        if (v.Contains("leftbottom") || v.Contains("rotate270cw") || v.Contains("rotate90ccw"))
            return "LeftBottom";

        return value switch
        {
            "1" => "TopLeft",
            "2" => "TopRight",
            "3" => "BottomRight",
            "4" => "BottomLeft",
            "5" => "LeftTop",
            "6" => "RightTop",
            "7" => "RightBottom",
            "8" => "LeftBottom",
            _ => null
        };
    }
}
