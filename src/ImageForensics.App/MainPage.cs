using System.Text;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using Microsoft.Maui.ApplicationModel.DataTransfer;
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
    private readonly IConsistencyRuleEngine _rules;
    private readonly IHiddenDataInspector _hidden;
    private readonly IPrivacyRiskAnalyzer _privacy;
    private readonly IMetadataCleaner _cleaner;
    private readonly IImageComparisonService _comparison;

    private readonly Label _status;
    private readonly Editor _result;
    private readonly ProgressBar _progress;
    private readonly Button _quick;
    private readonly Button _deep;
    private readonly Button _compare;
    private readonly Button _clean;
    private readonly Button _export;
    private CancellationTokenSource? _cts;
    private bool _handlingShare;

    public MainPage(
        IFileIdentityInspector identity,
        IImageTechnicalInspector technical,
        IMetadataInspector metadata,
        IContainerInspector container,
        IPerceptualHashService hashes,
        IBarcodeInspector barcode,
        IConsistencyRuleEngine rules,
        IHiddenDataInspector hidden,
        IPrivacyRiskAnalyzer privacy,
        IMetadataCleaner cleaner,
        IImageComparisonService comparison)
    {
        _identity = identity;
        _technical = technical;
        _metadata = metadata;
        _container = container;
        _hashes = hashes;
        _barcode = barcode;
        _rules = rules;
        _hidden = hidden;
        _privacy = privacy;
        _cleaner = cleaner;
        _comparison = comparison;

        Title = "فاحص الصور";
        FlowDirection = FlowDirection.RightToLeft;

        var title = new Label
        {
            Text = "Image Forensics — فاحص الصور",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold
        };

        var subtitle = new Label
        {
            Text = "تحليل محلي افتراضيًا. الحقائق منفصلة عن المؤشرات الاحتمالية.",
            FontSize = 14
        };

        _quick = new Button { Text = "فحص سريع" };
        _deep = new Button { Text = "فحص عميق" };
        _compare = new Button { Text = "مقارنة صورتين" };
        _clean = new Button { Text = "إنشاء نسخة نظيفة من Metadata" };
        _export = new Button { Text = "تصدير نتيجة TXT", IsEnabled = false };
        var cancel = new Button { Text = "إلغاء العملية", IsEnabled = false };

        _status = new Label { Text = "جاهز" };
        _progress = new ProgressBar { Progress = 0 };
        _result = new Editor
        {
            IsReadOnly = true,
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 440,
            FlowDirection = FlowDirection.LeftToRight
        };

        _quick.Clicked += async (_, _) => await PickAndScanAsync(false, cancel);
        _deep.Clicked += async (_, _) => await PickAndScanAsync(true, cancel);
        _compare.Clicked += async (_, _) => await CompareAsync(cancel);
        _clean.Clicked += async (_, _) => await CleanAsync(cancel);
        _export.Clicked += async (_, _) => await ExportTextAsync();
        cancel.Clicked += (_, _) => _cts?.Cancel();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    title, subtitle,
                    _quick, _deep, _compare, _clean, _export, cancel,
                    _progress, _status, _result
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_handlingShare) return;

        var shared = SharedImageInbox.Take();
        if (shared is null) return;

        _handlingShare = true;
        try
        {
            await AnalyzePathAsync(shared.Path, shared.DisplayName, shared.ContentType, true, null);
        }
        finally
        {
            try { File.Delete(shared.Path); } catch { }
            _handlingShare = false;
        }
    }

    private async Task PickAndScanAsync(bool deep, Button cancel)
    {
        string? temp = null;
        try
        {
            var selected = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "اختر صورة للفحص" });
            if (selected is null) return;

            BeginOperation(cancel);
            temp = await CopyToPrivateCacheAsync(selected, _cts!.Token);
            await AnalyzePathAsync(temp, selected.FileName, selected.ContentType ?? "application/octet-stream", deep, cancel);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "تم إلغاء العملية";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            EndOperation(cancel);
            DeleteQuietly(temp);
        }
    }

    private async Task AnalyzePathAsync(
        string path,
        string displayName,
        string contentType,
        bool deep,
        Button? cancel)
    {
        if (_cts is null || _cts.IsCancellationRequested)
            _cts = new CancellationTokenSource();

        SetBusy(true);
        if (cancel is not null) cancel.IsEnabled = true;
        _result.Text = string.Empty;
        _export.IsEnabled = false;

        try
        {
            var ct = _cts.Token;
            var progress = new Progress<AnalysisProgress>(p =>
            {
                _status.Text = $"{p.Stage}: {p.Detail}";
                _progress.Progress = p.Percent;
            });

            var identity = await _identity.InspectAsync(path, contentType, progress, ct);
            identity = identity with { FileName = displayName, SafeSource = "user-selected/shared content" };

            _status.Text = "قراءة الخصائص التقنية";
            var technical = await _technical.InspectAsync(path, ct);

            MetadataInspectionResult? metadata = null;
            ContainerInspectionResult? container = null;
            PerceptualHashResult? hashes = null;
            IReadOnlyList<BarcodeHit> barcodes = Array.Empty<BarcodeHit>();
            IReadOnlyList<EvidenceItem> indicators = Array.Empty<EvidenceItem>();
            IReadOnlyList<HiddenDataFinding> hidden = Array.Empty<HiddenDataFinding>();
            PrivacyRiskReport? privacy = null;

            if (deep)
            {
                _status.Text = "استخراج Metadata";
                metadata = await _metadata.InspectAsync(path, ct);

                _status.Text = "تحليل بنية الملف";
                container = await _container.InspectAsync(path, ct);

                _status.Text = "حساب البصمات البصرية";
                hashes = await _hashes.ComputeAsync(path, ct);

                _status.Text = "فحص QR / Barcode";
                barcodes = await _barcode.InspectAsync(path, ct);

                _status.Text = "فحص الاتساق";
                indicators = _rules.Analyze(new ForensicAnalysisContext(identity, technical, metadata, container));

                _status.Text = "البحث عن بيانات مضمنة";
                hidden = await _hidden.InspectAsync(path, ct);

                _status.Text = "تقييم مخاطر الخصوصية";
                privacy = _privacy.Analyze(metadata, container);
            }

            _result.Text = Format(identity, technical, metadata, container, hashes, barcodes, indicators, hidden, privacy);
            _status.Text = deep ? "اكتمل الفحص العميق" : "اكتمل الفحص السريع";
            _progress.Progress = 1;
            _export.IsEnabled = true;
        }
        finally
        {
            SetBusy(false);
            if (cancel is not null) cancel.IsEnabled = false;
        }
    }

    private async Task CompareAsync(Button cancel)
    {
        string? leftTemp = null;
        string? rightTemp = null;
        try
        {
            var left = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "اختر الصورة الأولى" });
            if (left is null) return;
            var right = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "اختر الصورة الثانية" });
            if (right is null) return;

            BeginOperation(cancel);
            leftTemp = await CopyToPrivateCacheAsync(left, _cts!.Token);
            rightTemp = await CopyToPrivateCacheAsync(right, _cts.Token);

            _status.Text = "جاري مقارنة الصورتين";
            var r = await _comparison.CompareAsync(leftTemp, rightTemp, _cts.Token);

            var sb = new StringBuilder();
            sb.AppendLine("IMAGE COMPARISON");
            sb.AppendLine($"Left: {left.FileName}");
            sb.AppendLine($"Right: {right.FileName}");
            sb.AppendLine($"Exact SHA-256 match: {r.ExactMatch}");
            sb.AppendLine($"Dimensions: {r.LeftWidth}×{r.LeftHeight} vs {r.RightWidth}×{r.RightHeight}");
            sb.AppendLine($"aHash similarity: {r.AHashSimilarity:P2}");
            sb.AppendLine($"dHash similarity: {r.DHashSimilarity:P2}");
            sb.AppendLine($"pHash similarity: {r.PHashSimilarity:P2}");
            sb.AppendLine($"Metadata differences: {r.MetadataDifferences.Count}");
            foreach (var diff in r.MetadataDifferences.Take(60))
                sb.AppendLine($"[{diff.Directory}] {diff.Tag}: {diff.LeftValue ?? "(none)"} => {diff.RightValue ?? "(none)"}");
            sb.AppendLine();
            sb.AppendLine("Perceptual similarity is approximate and is not proof that two images have the same origin.");

            _result.Text = sb.ToString();
            _status.Text = "اكتملت المقارنة";
            _export.IsEnabled = true;
        }
        catch (OperationCanceledException)
        {
            _status.Text = "تم إلغاء المقارنة";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            EndOperation(cancel);
            DeleteQuietly(leftTemp);
            DeleteQuietly(rightTemp);
        }
    }

    private async Task CleanAsync(Button cancel)
    {
        string? temp = null;
        try
        {
            var selected = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "اختر صورة لإنشاء نسخة نظيفة" });
            if (selected is null) return;

            BeginOperation(cancel);
            temp = await CopyToPrivateCacheAsync(selected, _cts!.Token);
            _status.Text = "إنشاء نسخة جديدة بدون Metadata";

            var outputDir = Path.Combine(FileSystem.AppDataDirectory, "Cleaned");
            var cleaned = await _cleaner.CreateCleanCopyAsync(temp, outputDir, _cts.Token);

            _result.Text =
                $"Clean copy created\n" +
                $"Format: {cleaned.OutputFormat}\n" +
                $"Metadata before: {cleaned.MetadataFieldsBefore}\n" +
                $"Metadata after: {cleaned.MetadataFieldsAfter}\n" +
                $"GPS removed: {cleaned.GpsRemoved}\n" +
                $"Source unchanged: {cleaned.OriginalUntouched}\n" +
                $"SHA-256 before: {cleaned.OriginalSha256}\n" +
                $"SHA-256 clean: {cleaned.CleanSha256}\n";
            _status.Text = "تم إنشاء النسخة النظيفة";

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "مشاركة النسخة النظيفة",
                File = new ShareFile(cleaned.OutputPath)
            });
        }
        catch (OperationCanceledException)
        {
            _status.Text = "تم إلغاء التنظيف";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            EndOperation(cancel);
            DeleteQuietly(temp);
        }
    }

    private async Task ExportTextAsync()
    {
        if (string.IsNullOrWhiteSpace(_result.Text)) return;
        var folder = Path.Combine(FileSystem.CacheDirectory, "reports");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"image-forensics-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.txt");
        await File.WriteAllTextAsync(path, _result.Text);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "تقرير Image Forensics",
            File = new ShareFile(path)
        });
    }

    private void BeginOperation(Button cancel)
    {
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        cancel.IsEnabled = true;
        SetBusy(true);
        _progress.Progress = 0;
        _export.IsEnabled = false;
    }

    private void EndOperation(Button cancel)
    {
        cancel.IsEnabled = false;
        SetBusy(false);
    }

    private void SetBusy(bool busy)
    {
        _quick.IsEnabled = !busy;
        _deep.IsEnabled = !busy;
        _compare.IsEnabled = !busy;
        _clean.IsEnabled = !busy;
    }

    private void ShowError(Exception ex)
    {
        _status.Text = "تعذر تنفيذ العملية";
        _result.Text = $"نوع الخطأ: {ex.GetType().Name}\nالتفاصيل: {ex.Message}";
    }

    private static async Task<string> CopyToPrivateCacheAsync(FileResult selected, CancellationToken ct)
    {
        var ext = Path.GetExtension(selected.FileName);
        if (ext.Length > 12 || ext.Any(ch => !char.IsLetterOrDigit(ch) && ch != '.')) ext = ".img";

        var folder = Path.Combine(FileSystem.CacheDirectory, "forensics");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ext);

        try
        {
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
        catch
        {
            DeleteQuietly(path);
            throw;
        }
    }

    private static string Format(
        FileIdentityResult r,
        ImageTechnicalInfo t,
        MetadataInspectionResult? m,
        ContainerInspectionResult? c,
        PerceptualHashResult? h,
        IReadOnlyList<BarcodeHit> barcodes,
        IReadOnlyList<EvidenceItem> indicators,
        IReadOnlyList<HiddenDataFinding> hidden,
        PrivacyRiskReport? privacy)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== OVERVIEW ===");
        sb.AppendLine($"Name: {r.FileName}");
        sb.AppendLine($"Size: {r.SizeBytes:N0} bytes");
        sb.AppendLine($"Detected: {r.DetectedType} ({r.DetectedMime})");
        sb.AppendLine($"Extension mismatch: {r.ExtensionMismatch}");
        sb.AppendLine($"Dimensions: {t.Width}×{t.Height}; Aspect={t.AspectRatio:F4}");
        sb.AppendLine($"Encoding: {t.EncodedFormat}; Color={t.ColorType}; Alpha={t.AlphaType}; Frames={t.FrameCount}");
        sb.AppendLine($"Entropy: {r.EntropyBitsPerByte:F4} bits/byte");
        sb.AppendLine($"SHA-256: {r.Sha256}");
        sb.AppendLine($"SHA-1: {r.Sha1}");
        sb.AppendLine($"MD5: {r.Md5} [comparison only]");
        sb.AppendLine();

        if (m is not null)
        {
            sb.AppendLine("=== METADATA ===");
            sb.AppendLine($"Fields: {m.Fields.Count}");
            if (m.Gps is not null)
                sb.AppendLine($"GPS: {m.Gps.Latitude:F8}, {m.Gps.Longitude:F8}");
            foreach (var field in m.Fields.Take(80))
                sb.AppendLine($"[{field.Directory}] {field.Tag}: {field.ParsedValue}");
            if (m.Fields.Count > 80) sb.AppendLine($"... +{m.Fields.Count - 80} more fields");
            foreach (var error in m.Errors.Take(10)) sb.AppendLine($"Parser warning: {error}");
            sb.AppendLine();
        }

        if (c is not null)
        {
            sb.AppendLine("=== STRUCTURE ===");
            sb.AppendLine($"Container: {c.Format}; Segments={c.Segments.Count}; Trailing={c.TrailingBytes}");
            foreach (var segment in c.Segments.Take(80))
                sb.AppendLine($"0x{segment.Offset:X}: {segment.Type} ({segment.Length} bytes)");
            foreach (var warning in c.Warnings) sb.AppendLine($"Warning: {warning}");
            sb.AppendLine();
        }

        if (h is not null)
        {
            sb.AppendLine("=== PERCEPTUAL HASHES ===");
            sb.AppendLine($"aHash: {h.AHash}");
            sb.AppendLine($"dHash: {h.DHash}");
            sb.AppendLine($"pHash: {h.PHash}");
            sb.AppendLine();
        }

        if (barcodes.Count > 0)
        {
            sb.AppendLine("=== QR / BARCODE ===");
            foreach (var hit in barcodes) sb.AppendLine($"[{hit.Format}] {hit.Text}");
            sb.AppendLine("Links are displayed as text and are never opened automatically.");
            sb.AppendLine();
        }

        if (indicators.Count > 0)
        {
            sb.AppendLine("=== FORENSIC INDICATORS ===");
            foreach (var item in indicators)
            {
                sb.AppendLine($"{item.Confidence}: {item.Title}");
                sb.AppendLine($"  Evidence: {item.Evidence}");
                sb.AppendLine($"  Limitation: {item.Limitation}");
            }
            sb.AppendLine();
        }

        if (hidden.Count > 0)
        {
            sb.AppendLine("=== HIDDEN-DATA HEURISTICS ===");
            foreach (var item in hidden.Take(100))
            {
                sb.AppendLine($"{item.Confidence}: {item.Kind} @ {item.Offset}");
                sb.AppendLine($"  {item.Evidence}");
                sb.AppendLine($"  Limitation: {item.Limitation}");
            }
            sb.AppendLine();
        }

        if (privacy is not null)
        {
            sb.AppendLine("=== PRIVACY RISKS ===");
            if (privacy.Risks.Count == 0) sb.AppendLine("No configured metadata privacy risks were detected.");
            foreach (var risk in privacy.Risks)
                sb.AppendLine($"{risk.Confidence}: {risk.Title} — {risk.Evidence}");
            sb.AppendLine();
        }

        sb.AppendLine("LIMITATION: forensic heuristics are indicators, not final proof that an image is genuine or manipulated.");
        return sb.ToString();
    }

    private static void DeleteQuietly(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
