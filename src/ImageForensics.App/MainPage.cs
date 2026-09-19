using System.Text;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using Microsoft.Maui.Storage;

namespace ImageForensics.App;

public sealed class MainPage : ContentPage
{
    private readonly IFileIdentityInspector _identity;
    private readonly IImageTechnicalInspector _technical;
    private readonly IMetadataInspector _metadata;
    private readonly IContainerInspector _container;
    private readonly IPerceptualHashService _hashes;
    private readonly IBarcodeInspector _barcode;

    private readonly Label _status;
    private readonly Editor _result;
    private readonly ProgressBar _progress;
    private CancellationTokenSource? _cts;

    public MainPage(
        IFileIdentityInspector identity,
        IImageTechnicalInspector technical,
        IMetadataInspector metadata,
        IContainerInspector container,
        IPerceptualHashService hashes,
        IBarcodeInspector barcode)
    {
        _identity = identity;
        _technical = technical;
        _metadata = metadata;
        _container = container;
        _hashes = hashes;
        _barcode = barcode;

        Title = "فاحص الصور";
        FlowDirection = FlowDirection.RightToLeft;

        var title = new Label { Text = "Image Forensics — فاحص الصور", FontSize = 26, FontAttributes = FontAttributes.Bold };
        var subtitle = new Label { Text = "التحليل محلي افتراضيًا، والنتائج الاحتمالية لا تُعرض كحكم قطعي.", FontSize = 14 };
        var quick = new Button { Text = "فحص سريع" };
        var deep = new Button { Text = "فحص عميق" };
        var cancel = new Button { Text = "إلغاء الفحص", IsEnabled = false };
        _status = new Label { Text = "جاهز" };
        _progress = new ProgressBar { Progress = 0 };
        _result = new Editor { IsReadOnly = true, AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 420, FlowDirection = FlowDirection.LeftToRight };

        quick.Clicked += async (_, _) => await RunScanAsync(false, quick, deep, cancel);
        deep.Clicked += async (_, _) => await RunScanAsync(true, quick, deep, cancel);
        cancel.Clicked += (_, _) => _cts?.Cancel();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children = { title, subtitle, quick, deep, cancel, _progress, _status, _result }
            }
        };
    }

    private async Task RunScanAsync(bool deep, Button quick, Button deepButton, Button cancel)
    {
        string? tempPath = null;
        try
        {
            var selected = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "اختر صورة للفحص" });
            if (selected is null) return;

            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            cancel.IsEnabled = true;
            quick.IsEnabled = false;
            deepButton.IsEnabled = false;
            _result.Text = string.Empty;

            tempPath = await CopyToPrivateCacheAsync(selected, _cts.Token);

            var progress = new Progress<AnalysisProgress>(p =>
            {
                _status.Text = $"{p.Stage}: {p.Detail}";
                _progress.Progress = p.Percent;
            });

            var identity = await _identity.InspectAsync(tempPath, selected.ContentType, progress, _cts.Token);
            _status.Text = "قراءة الخصائص التقنية";
            var tech = await _technical.InspectAsync(tempPath, _cts.Token);

            MetadataInspectionResult? metadata = null;
            ContainerInspectionResult? container = null;
            PerceptualHashResult? hashes = null;
            IReadOnlyList<BarcodeHit> barcodes = Array.Empty<BarcodeHit>();

            if (deep)
            {
                _status.Text = "استخراج Metadata";
                metadata = await _metadata.InspectAsync(tempPath, _cts.Token);
                _status.Text = "تحليل بنية الملف";
                container = await _container.InspectAsync(tempPath, _cts.Token);
                _status.Text = "حساب البصمات البصرية";
                hashes = await _hashes.ComputeAsync(tempPath, _cts.Token);
                _status.Text = "فحص QR / Barcode";
                barcodes = await _barcode.InspectAsync(tempPath, _cts.Token);
            }

            _result.Text = Format(identity, tech, metadata, container, hashes, barcodes);
            _status.Text = deep ? "اكتمل الفحص العميق" : "اكتمل الفحص السريع";
            _progress.Progress = 1;
        }
        catch (OperationCanceledException)
        {
            _status.Text = "تم إلغاء الفحص";
        }
        catch (Exception ex)
        {
            _status.Text = "تعذر فحص الملف";
            _result.Text = $"نوع الخطأ: {ex.GetType().Name}\nالتفاصيل: {ex.Message}";
        }
        finally
        {
            cancel.IsEnabled = false;
            quick.IsEnabled = true;
            deepButton.IsEnabled = true;
            if (tempPath is not null)
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    private static async Task<string> CopyToPrivateCacheAsync(FileResult selected, CancellationToken ct)
    {
        var ext = Path.GetExtension(selected.FileName);
        if (ext.Length > 12 || ext.Any(ch => !char.IsLetterOrDigit(ch) && ch != '.')) ext = ".img";

        var folder = Path.Combine(FileSystem.CacheDirectory, "forensics");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ext);

        await using var source = await selected.OpenReadAsync();
        await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous);
        var buffer = new byte[128 * 1024];
        long total = 0;
        const long maxBytes = 512L * 1024 * 1024;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var read = await source.ReadAsync(buffer, ct);
            if (read == 0) break;
            total += read;
            if (total > maxBytes) throw new InvalidDataException("الملف أكبر من الحد الآمن المسموح.");
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return path;
    }

    private static string Format(
        FileIdentityResult r,
        ImageTechnicalInfo t,
        MetadataInspectionResult? m,
        ContainerInspectionResult? c,
        PerceptualHashResult? h,
        IReadOnlyList<BarcodeHit> barcodes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"الاسم: {r.FileName}");
        sb.AppendLine($"الحجم: {r.SizeBytes:N0} bytes");
        sb.AppendLine($"النوع الحقيقي: {r.DetectedType} ({r.DetectedMime})");
        sb.AppendLine($"تعارض الامتداد: {(r.ExtensionMismatch ? "نعم" : "لا")}");
        sb.AppendLine($"الأبعاد: {t.Width}×{t.Height}  Aspect={t.AspectRatio:F4}");
        sb.AppendLine($"الترميز: {t.EncodedFormat}; Color={t.ColorType}; Alpha={t.AlphaType}; Frames={t.FrameCount}");
        sb.AppendLine($"Entropy: {r.EntropyBitsPerByte:F4} bits/byte");
        sb.AppendLine($"SHA-256: {r.Sha256}");
        sb.AppendLine($"SHA-1: {r.Sha1}");
        sb.AppendLine($"MD5: {r.Md5}  [للمقارنة فقط]");
        sb.AppendLine($"Magic bytes: {r.SignatureHex}");

        if (m is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"Metadata fields: {m.Fields.Count}");
            if (m.Gps is not null)
                sb.AppendLine($"GPS: {m.Gps.Latitude:F8}, {m.Gps.Longitude:F8}");
            foreach (var field in m.Fields.Take(40))
                sb.AppendLine($"[{field.Directory}] {field.Tag}: {field.ParsedValue}");
            if (m.Fields.Count > 40) sb.AppendLine($"... +{m.Fields.Count - 40} more fields");
        }

        if (c is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"Container: {c.Format}; segments={c.Segments.Count}; trailing={c.TrailingBytes}");
            foreach (var warning in c.Warnings) sb.AppendLine($"WARNING: {warning}");
        }

        if (h is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"aHash: {h.AHash}");
            sb.AppendLine($"dHash: {h.DHash}");
            sb.AppendLine($"pHash: {h.PHash}");
        }

        foreach (var hit in barcodes)
            sb.AppendLine($"QR/Barcode [{hit.Format}]: {hit.Text}");

        sb.AppendLine();
        sb.AppendLine("هذه حقائق تقنية وقرائن فقط؛ لا يتم الحكم قطعيًا على أن الصورة أصلية أو مزورة.");
        return sb.ToString();
    }
}
