using System.Text;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using ImageForensics.Reporting;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace ImageForensics.App;

public sealed class MainPage : ContentPage
{
    private readonly ScanCoordinator _scanner;
    private readonly IMetadataCleaner _cleaner;
    private readonly IImageComparisonService _comparison;
    private readonly IImageVisualizationService _visuals;
    private readonly ReportWriter _writer;

    private readonly Editor _result = new()
    {
        IsReadOnly = true,
        AutoSize = EditorAutoSizeOption.TextChanges,
        MinimumHeightRequest = 380,
        FlowDirection = FlowDirection.LeftToRight
    };
    private readonly Label _status = new() { Text = "جاهز" };
    private readonly ProgressBar _progress = new() { Progress = 0 };
    private readonly Image _preview = new() { HeightRequest = 240, Aspect = Aspect.AspectFit };
    private readonly Entry _metadataSearch = new() { Placeholder = "بحث داخل Metadata..." };
    private readonly List<Button> _actions = new();

    private ScanReport? _last;
    private string? _lastSource;
    private CancellationTokenSource? _cts;

    public MainPage(
        ScanCoordinator scanner,
        IMetadataCleaner cleaner,
        IImageComparisonService comparison,
        IImageVisualizationService visuals,
        ReportWriter writer)
    {
        _scanner = scanner;
        _cleaner = cleaner;
        _comparison = comparison;
        _visuals = visuals;
        _writer = writer;

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
            Text = "Offline افتراضيًا — الحقائق منفصلة عن المؤشرات الاحتمالية.",
            FontSize = 14
        };

        var quick = ActionButton("فحص سريع");
        var deep = ActionButton("فحص عميق");
        var compare = ActionButton("مقارنة صورتين");
        var batch = ActionButton("فحص مجموعة + CSV");
        var clean = ActionButton("إنشاء نسخة نظيفة");
        var export = ActionButton("تصدير ومشاركة JSON / TXT / PDF");
        var gps = ActionButton("فتح GPS في الخرائط");\n        var copyGps = ActionButton("نسخ إحداثيات GPS");
        var ela = ActionButton("إنشاء ELA مساعد");
        var red = ActionButton("عرض قناة R");
        var lsb = ActionButton("عرض Bit-plane LSB");
        var search = ActionButton("بحث Metadata");
        var theme = ActionButton("تبديل Light / Dark");
        var cancel = new Button { Text = "إلغاء العملية", IsEnabled = false };

        quick.Clicked += async (_, _) => await ScanPickedAsync(false, cancel);
        deep.Clicked += async (_, _) => await ScanPickedAsync(true, cancel);
        compare.Clicked += async (_, _) => await CompareAsync(cancel);
        batch.Clicked += async (_, _) => await BatchAsync(cancel);
        clean.Clicked += async (_, _) => await CleanAsync(cancel);
        export.Clicked += async (_, _) => await ExportAsync();
        gps.Clicked += async (_, _) => await OpenGpsAsync();\n        copyGps.Clicked += async (_, _) => await CopyGpsAsync();
        ela.Clicked += async (_, _) => await CreateVisualizationAsync("ELA");
        red.Clicked += async (_, _) => await CreateVisualizationAsync("R");
        lsb.Clicked += async (_, _) => await CreateVisualizationAsync("LSB");
        search.Clicked += (_, _) => SearchMetadata();
        cancel.Clicked += (_, _) => _cts?.Cancel();
        theme.Clicked += (_, _) =>
        {
            if (Application.Current is null) return;
            Application.Current.UserAppTheme =
                Application.Current.UserAppTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 10,
                Children =
                {
                    title, subtitle, _preview,
                    quick, deep, compare, batch, clean, export, gps, copyGps, ela, red, lsb,
                    _metadataSearch, search,
                    cancel, theme,
                    _progress, _status, _result
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        var uri = SharedImageInbox.Take();
        if (uri is null) return;

        string? temp = null;
        try
        {
            SetBusy(true, null);
            temp = await CopyAndroidUriToCacheAsync(uri, _cts!.Token);
            await RunScanAsync(temp, null, "shared-image", true);
            if (!string.Equals(_lastSource, temp, StringComparison.Ordinal))
                TryDelete(temp);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "تم إلغاء قراءة الصورة المشتركة";
            TryDelete(temp);
        }
        catch (Exception ex)
        {
            ShowError(ex);
            TryDelete(temp);
        }
        finally
        {
            SetBusy(false, null);
        }
#endif
    }

    private Button ActionButton(string text)
    {
        var button = new Button { Text = text };
        _actions.Add(button);
        return button;
    }

    private async Task ScanPickedAsync(bool deep, Button cancel)
    {
        var selected = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = deep ? "اختر صورة للفحص العميق" : "اختر صورة للفحص السريع"
        });
        if (selected is null) return;

        string? temp = null;
        try
        {
            SetBusy(true, cancel);
            temp = await CopyToCacheAsync(selected, _cts!.Token);
            await RunScanAsync(temp, selected.ContentType, selected.FileName, deep);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "تم الإلغاء";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false, cancel);
            if (!string.Equals(_lastSource, temp, StringComparison.Ordinal))
                TryDelete(temp);
        }
    }

    private async Task RunScanAsync(
        string path,
        string? mime,
        string? displayName,
        bool deep)
    {
        var progress = new Progress<AnalysisProgress>(p =>
        {
            _status.Text = $"{p.Stage}: {p.Detail}";
            _progress.Progress = p.Percent;
        });

        var ct = _cts?.Token ?? CancellationToken.None;
        var report = deep
            ? await _scanner.DeepScanAsync(path, mime, progress, ct)
            : await _scanner.QuickScanAsync(path, mime, progress, ct);

        if (!string.IsNullOrWhiteSpace(displayName))
            report = report with { Identity = report.Identity with { FileName = displayName } };

        RememberSource(path);
        _last = report;
        _preview.Source = ImageSource.FromFile(path);
        _result.Text = _writer.ToText(report);
        _status.Text = deep ? "اكتمل الفحص العميق" : "اكتمل الفحص السريع";
        _progress.Progress = 1;
    }

    private async Task CompareAsync(Button cancel)
    {
        var items = (await FilePicker.Default.PickMultipleAsync(
            new PickOptions { PickerTitle = "اختر صورتين للمقارنة" })).Take(2).ToArray();
        if (items.Length != 2)
        {
            _status.Text = "اختر صورتين بالضبط";
            return;
        }

        string? a = null;
        string? b = null;
        try
        {
            SetBusy(true, cancel);
            a = await CopyToCacheAsync(items[0], _cts!.Token);
            b = await CopyToCacheAsync(items[1], _cts.Token);
            var r = await _comparison.CompareAsync(a, b, _cts.Token);

            var dir = Path.Combine(FileSystem.AppDataDirectory, "exports");
            Directory.CreateDirectory(dir);
            var diffPath = Path.Combine(dir, $"difference-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.png");
            await _visuals.CreateDifferenceMapAsync(a, b, diffPath, _cts.Token);

            _result.Text =
                $"Exact SHA-256 match: {r.ExactMatch}\n" +
                $"Dimensions: {r.LeftWidth}x{r.LeftHeight} vs {r.RightWidth}x{r.RightHeight}\n" +
                $"aHash similarity: {r.AHashSimilarity:P2}\n" +
                $"dHash similarity: {r.DHashSimilarity:P2}\n" +
                $"pHash similarity: {r.PHashSimilarity:P2}\n" +
                $"Metadata differences: {r.MetadataDifferences.Count}\n" +
                $"Difference map: {diffPath}\n\n" +
                "التشابه البصري مؤشر تقريبي وليس إثباتًا على مصدر أو تزوير.";
            _status.Text = "اكتملت المقارنة";

            await Share.Default.RequestAsync(new ShareFileRequest(
                "Pixel difference map",
                new ShareFile(diffPath)));
        }
        catch (OperationCanceledException)
        {
            _status.Text = "تم الإلغاء";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false, cancel);
            TryDelete(a);
            TryDelete(b);
        }
    }

    private async Task BatchAsync(Button cancel)
    {
        var items = (await FilePicker.Default.PickMultipleAsync(
            new PickOptions { PickerTitle = "اختر صور المجموعة" })).Take(50).ToArray();
        if (items.Length == 0) return;

        var rows = new List<BatchReportRow>();
        try
        {
            SetBusy(true, cancel);
            for (var i = 0; i < items.Length; i++)
            {
                _cts!.Token.ThrowIfCancellationRequested();
                _status.Text = $"Batch {i + 1}/{items.Length}";
                string? temp = null;
                try
                {
                    temp = await CopyToCacheAsync(items[i], _cts.Token);
                    var report = await _scanner.DeepScanAsync(temp, items[i].ContentType, null, _cts.Token);
                    rows.Add(new BatchReportRow(
                        items[i].FileName,
                        report.Identity.Sha256,
                        report.Identity.DetectedType,
                        report.Identity.SizeBytes,
                        report.Technical?.Width ?? 0,
                        report.Technical?.Height ?? 0,
                        report.Metadata?.Gps is not null,
                        report.Privacy?.Risks.Count ?? 0,
                        report.Indicators.Count));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    rows.Add(new BatchReportRow(
                        items[i].FileName,
                        "ERROR",
                        ex.GetType().Name,
                        0, 0, 0, false, 0, 0));
                }
                finally
                {
                    TryDelete(temp);
                }
            }

            var dir = Path.Combine(FileSystem.AppDataDirectory, "exports");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"batch-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv");
            await File.WriteAllTextAsync(path, _writer.BatchToCsv(rows), _cts.Token);
            _result.Text = $"تم فحص {rows.Count} ملف.\nCSV: {path}";
            _status.Text = "اكتمل Batch";
            await Share.Default.RequestAsync(new ShareFileRequest("Batch CSV", new ShareFile(path)));
        }
        catch (OperationCanceledException)
        {
            _status.Text = "تم إلغاء Batch";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false, cancel);
        }
    }

    private async Task CleanAsync(Button cancel)
    {
        var selected = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "اختر صورة لإنشاء نسخة بدون Metadata حساسة"
        });
        if (selected is null) return;

        string? temp = null;
        try
        {
            SetBusy(true, cancel);
            temp = await CopyToCacheAsync(selected, _cts!.Token);
            var dir = Path.Combine(FileSystem.AppDataDirectory, "cleaned");
            var r = await _cleaner.CreateCleanCopyAsync(temp, dir, _cts.Token);

            _result.Text =
                $"النسخة النظيفة: {r.OutputPath}\n" +
                $"Format: {r.OutputFormat}\n" +
                $"Metadata before/after: {r.MetadataFieldsBefore}/{r.MetadataFieldsAfter}\n" +
                $"GPS removed: {r.GpsRemoved}\n" +
                $"Original untouched: {r.OriginalUntouched}\n" +
                $"SHA-256: {r.CleanSha256}";
            _status.Text = "تم إنشاء النسخة النظيفة";

            await Share.Default.RequestAsync(new ShareFileRequest(
                "Clean image",
                new ShareFile(r.OutputPath)));
        }
        catch (OperationCanceledException)
        {
            _status.Text = "تم الإلغاء";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false, cancel);
            TryDelete(temp);
        }
    }

    private async Task ExportAsync()
    {
        if (_last is null)
        {
            _status.Text = "نفّذ فحصًا أولًا";
            return;
        }

        try
        {
            var dir = Path.Combine(FileSystem.AppDataDirectory, "exports");
            Directory.CreateDirectory(dir);
            var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            var json = Path.Combine(dir, $"report-{stamp}.json");
            var txt = Path.Combine(dir, $"report-{stamp}.txt");
            var pdf = Path.Combine(dir, $"report-{stamp}.pdf");

            await File.WriteAllTextAsync(json, _writer.ToJson(_last));
            await File.WriteAllTextAsync(txt, _writer.ToText(_last));
            _writer.WritePdf(_last, pdf);

            await Share.Default.RequestAsync(new ShareMultipleFilesRequest(
                "Image Forensics report",
                new List<ShareFile> { new(json), new(txt), new(pdf) }));
            _status.Text = "تم تصدير JSON / TXT / PDF";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task OpenGpsAsync()
    {
        var gps = _last?.Metadata?.Gps;
        if (gps is null)
        {
            _status.Text = "لا توجد GPS صريحة في Metadata";
            return;
        }

        try
        {
            await Launcher.Default.OpenAsync(
                $"geo:{gps.Latitude},{gps.Longitude}?q={gps.Latitude},{gps.Longitude}");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task CopyGpsAsync()
    {
        var gps = _last?.Metadata?.Gps;
        if (gps is null)
        {
            _status.Text = "لا توجد GPS صريحة في Metadata";
            return;
        }

        var coordinates = $"{gps.Latitude:F8}, {gps.Longitude:F8}";
        await Clipboard.Default.SetTextAsync(coordinates);
        _status.Text = "تم نسخ إحداثيات GPS";
    }

    private async Task CreateVisualizationAsync(string kind)
    {
        if (_lastSource is null || !File.Exists(_lastSource))
        {
            _status.Text = "نفّذ فحصًا جديدًا أولًا";
            return;
        }

        try
        {
            var dir = Path.Combine(FileSystem.AppDataDirectory, "exports");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(
                dir,
                $"{kind.ToLowerInvariant()}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.png");

            if (kind == "ELA")
                await _visuals.CreateElaPreviewAsync(_lastSource, path);
            else if (kind == "LSB")
                await _visuals.CreateBitPlaneAsync(_lastSource, 0, path);
            else
                await _visuals.CreateRgbChannelAsync(_lastSource, kind, path);

            await Share.Default.RequestAsync(new ShareFileRequest(
                $"{kind} diagnostic visualization",
                new ShareFile(path)));
            _status.Text = $"{kind}: تم إنشاء الصورة المساعدة";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void SearchMetadata()
    {
        if (_last?.Metadata is null)
        {
            _status.Text = "الفحص الحالي لا يحتوي Metadata عميقة";
            return;
        }

        var q = _metadataSearch.Text?.Trim();
        if (string.IsNullOrWhiteSpace(q))
        {
            _result.Text = _writer.ToText(_last);
            return;
        }

        var matches = _last.Metadata.Fields
            .Where(x =>
                x.Directory.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Tag.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (x.ParsedValue?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.RawValue?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(250)
            .ToArray();

        var sb = new StringBuilder();
        sb.AppendLine($"Metadata search: {q}");
        sb.AppendLine($"Matches: {matches.Length}");
        foreach (var field in matches)
        {
            sb.AppendLine($"[{field.Directory}] {field.Tag}");
            sb.AppendLine($"  Parsed: {field.ParsedValue}");
            sb.AppendLine($"  Raw: {field.RawValue}");
            sb.AppendLine($"  Meaning: {field.Meaning}");
            sb.AppendLine($"  Confidence: {field.Confidence}");
        }
        _result.Text = sb.ToString();
    }

    private void SetBusy(bool busy, Button? cancel)
    {
        if (busy)
        {
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _progress.Progress = 0;
        }

        if (cancel is not null) cancel.IsEnabled = busy;
        foreach (var action in _actions) action.IsEnabled = !busy;
    }

    private void RememberSource(string path)
    {
        if (_lastSource is not null &&
            !string.Equals(_lastSource, path, StringComparison.Ordinal))
            TryDelete(_lastSource);

        _lastSource = path;
    }

    private static async Task<string> CopyToCacheAsync(FileResult selected, CancellationToken ct)
    {
        var folder = Path.Combine(FileSystem.CacheDirectory, "forensics");
        Directory.CreateDirectory(folder);

        var ext = Path.GetExtension(selected.FileName);
        if (ext.Length > 12 || ext.Any(ch => !char.IsLetterOrDigit(ch) && ch != '.'))
            ext = ".img";

        var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ext);
        try
        {
            await using var source = await selected.OpenReadAsync();
            await using var target = new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, true);
            await CopyWithLimitAsync(source, target, 512L * 1024 * 1024, ct);
            return path;
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }

#if ANDROID
    private static async Task<string> CopyAndroidUriToCacheAsync(Android.Net.Uri uri, CancellationToken ct)
    {
        var folder = Path.Combine(FileSystem.CacheDirectory, "forensics");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".shared");

        try
        {
            await using var source =
                Android.App.Application.Context.ContentResolver!.OpenInputStream(uri)
                ?? throw new IOException("تعذر فتح الصورة المشتركة.");
            await using var target = new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, true);
            await CopyWithLimitAsync(source, target, 512L * 1024 * 1024, ct);
            return path;
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }
#endif

    private static async Task CopyWithLimitAsync(
        Stream source,
        Stream target,
        long maxBytes,
        CancellationToken ct)
    {
        var buffer = new byte[128 * 1024];
        long total = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var read = await source.ReadAsync(buffer, ct);
            if (read == 0) break;
            total += read;
            if (total > maxBytes)
                throw new InvalidDataException("الملف أكبر من الحد الآمن.");
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    private void ShowError(Exception ex)
    {
        _status.Text = "حدث خطأ";
        _result.Text = $"{ex.GetType().Name}: {ex.Message}";
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
        }
    }
}
