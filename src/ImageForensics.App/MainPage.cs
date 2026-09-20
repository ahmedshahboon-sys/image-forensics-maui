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
    private readonly IScanHistoryStore _history;

    private readonly Editor _result = new()
    {
        IsReadOnly = true,
        AutoSize = EditorAutoSizeOption.TextChanges,
        MinimumHeightRequest = 380,
        FlowDirection = FlowDirection.LeftToRight
    };
    private readonly Label _status = new() { Text = "جاهز" };
    private readonly ProgressBar _progress = new() { Progress = 0 };
    private readonly Picker _resultSection = new() { Title = "قسم النتيجة" };
    private readonly Switch _privacyMode = new();
    private readonly Switch _onlineMode = new();
    private readonly Image _preview = new() { HeightRequest = 240, Aspect = Aspect.AspectFit };
    private readonly Entry _metadataSearch = new() { Placeholder = "بحث داخل Metadata..." };
    private readonly Picker _batchFilter = new()
    {
        Title = "فلتر نتائج Batch"
    };
    private IReadOnlyList<BatchReportRow> _lastBatchRows = Array.Empty<BatchReportRow>();
    private readonly List<Button> _actions = new();

    private ScanReport? _last;
    private string? _lastSource;
    private CancellationTokenSource? _cts;

    public MainPage(
        ScanCoordinator scanner,
        IMetadataCleaner cleaner,
        IImageComparisonService comparison,
        IImageVisualizationService visuals,
        ReportWriter writer,
        IScanHistoryStore history)
    {
        _scanner = scanner;
        _cleaner = cleaner;
        _comparison = comparison;
        _visuals = visuals;
        _writer = writer;
        _history = history;

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

        _resultSection.ItemsSource = new[]
        {
            "Overview",
            "Metadata",
            "GPS",
            "Structure",
            "Forensics",
            "OCR",
            "Privacy",
            "Raw"
        };
        _resultSection.SelectedIndex = 0;
        _resultSection.SelectedIndexChanged += (_, _) => RenderSelectedSection();

        _privacyMode.IsToggled =
            Preferences.Default.Get(
                "privacy-mode",
                false);

        _privacyMode.Toggled += (_, args) =>
        {
            Preferences.Default.Set(
                "privacy-mode",
                args.Value);

            _status.Text =
                args.Value
                    ? "Privacy Mode مفعّل — لن يتم حفظ سجل الفحوصات الجديدة"
                    : "Privacy Mode متوقف — يمكن حفظ سجل مختصر";
        };

        _onlineMode.IsToggled =
            Preferences.Default.Get(
                "online-mode",
                false);

        _onlineMode.Toggled += (_, args) =>
        {
            Preferences.Default.Set(
                "online-mode",
                args.Value);

            _status.Text =
                args.Value
                    ? "Online Mode مفعّل — أي خدمة خارجية ستحتاج موافقة إضافية"
                    : "Online Mode متوقف";
        };

        var quick = ActionButton("فحص سريع");
        var deep = ActionButton("فحص عميق");
        var compare = ActionButton("مقارنة صورتين");
        var batch = ActionButton("فحص مجموعة + CSV/JSON");
        var applyBatchFilter = ActionButton("تطبيق فلتر Batch");

        _batchFilter.ItemsSource = new[]
        {
            "الكل",
            "GPS",
            "مخاطر خصوصية",
            "نسخ مطابقة",
            "نسخ متشابهة",
            "أخطاء"
        };
        _batchFilter.SelectedIndex = 0;
        var clean = ActionButton("إنشاء نسخة نظيفة");
        var export = ActionButton("تصدير ومشاركة JSON / TXT / PDF");
        var gps = ActionButton("فتح GPS في الخرائط");
        var copyGps = ActionButton("نسخ إحداثيات GPS");
        var ela = ActionButton("إنشاء ELA مساعد");
        var red = ActionButton("عرض قناة R");
        var lsb = ActionButton("عرض Bit-plane LSB");
        var entropyMap = ActionButton("خريطة Entropy");
        var copyOcr = ActionButton("نسخ نص OCR");
        var search = ActionButton("بحث Metadata");
        var theme = ActionButton("تبديل Light / Dark");
        var historyButton = ActionButton("السجل المحلي");
        var clearHistory = ActionButton("مسح السجل");
        var histogram = ActionButton("Histogram RGB");
        var copyMetadataField = ActionButton("نسخ أول حقل مطابق");
        var explainMetadataField = ActionButton("شرح أول حقل مطابق");
        var reverseSearch = ActionButton("Reverse Image Search — فتح خدمة خارجية");
        var hashReputation = ActionButton("سمعة SHA-256 — فتح خدمة خارجية");
        var cancel = new Button { Text = "إلغاء العملية", IsEnabled = false };

        quick.Clicked += async (_, _) => await ScanPickedAsync(false, cancel);
        deep.Clicked += async (_, _) => await ScanPickedAsync(true, cancel);
        compare.Clicked += async (_, _) => await CompareAsync(cancel);
        batch.Clicked += async (_, _) => await BatchAsync(cancel);
        applyBatchFilter.Clicked += (_, _) => ApplyBatchFilter();
        clean.Clicked += async (_, _) => await CleanAsync(cancel);
        export.Clicked += async (_, _) => await ExportAsync();
        gps.Clicked += async (_, _) => await OpenGpsAsync();
        copyGps.Clicked += async (_, _) => await CopyGpsAsync();
        ela.Clicked += async (_, _) => await CreateVisualizationAsync("ELA");
        red.Clicked += async (_, _) => await CreateVisualizationAsync("R");
        lsb.Clicked += async (_, _) => await CreateVisualizationAsync("LSB");
        entropyMap.Clicked += async (_, _) => await CreateVisualizationAsync("ENTROPY");
        copyOcr.Clicked += async (_, _) => await CopyOcrAsync();
        search.Clicked += (_, _) => SearchMetadata();
        cancel.Clicked += (_, _) => _cts?.Cancel();
        historyButton.Clicked += async (_, _) => await ShowHistoryAsync();
        clearHistory.Clicked += async (_, _) => await ClearHistoryAsync();
        histogram.Clicked += async (_, _) => await CreateVisualizationAsync("HISTOGRAM");
        copyMetadataField.Clicked += async (_, _) => await CopyFirstMetadataMatchAsync();
        explainMetadataField.Clicked += (_, _) => ExplainFirstMetadataMatch();
        reverseSearch.Clicked += async (_, _) => await OpenReverseSearchAsync();
        hashReputation.Clicked += async (_, _) => await OpenHashReputationAsync();
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
                    quick, deep, compare, batch, _batchFilter, applyBatchFilter, clean,
                    historyButton, clearHistory,
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            new Label
                            {
                                Text = "Privacy Mode",
                                VerticalTextAlignment = TextAlignment.Center
                            },
                            _privacyMode
                        }
                    },
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            new Label
                            {
                                Text = "Online Mode (Off افتراضيًا)",
                                VerticalTextAlignment = TextAlignment.Center
                            },
                            _onlineMode
                        }
                    },
                    reverseSearch, hashReputation,
                    export, gps, copyGps, ela, histogram, red, lsb, entropyMap, copyOcr,
                    _metadataSearch, search, copyMetadataField, explainMetadataField,
                    cancel, theme,
                    _progress, _status, _resultSection, _result
                }
            }
        };

        var pinch =
            new PinchGestureRecognizer();

        pinch.PinchUpdated += (_, e) =>
        {
            if (e.Status == GestureStatus.Running)
            {
                _preview.Scale =
                    Math.Clamp(
                        _preview.Scale * e.Scale,
                        1.0,
                        5.0);
            }
            else if (e.Status == GestureStatus.Completed)
            {
                _preview.Scale =
                    Math.Clamp(
                        _preview.Scale,
                        1.0,
                        5.0);
            }
        };

        _preview.GestureRecognizers.Add(
            pinch);
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
        RenderSelectedSection();

        if (!_privacyMode.IsToggled)
        {
            await _history.AddAsync(
                new ScanHistoryEntry(
                    report.ScannedAtUtc,
                    report.Identity.FileName,
                    report.Identity.DetectedType,
                    report.Identity.SizeBytes,
                    report.Identity.Sha256,
                    deep,
                    report.Indicators.Count,
                    report.Privacy?.Risks.Count ?? 0),
                ct);
        }

        _status.Text = deep ? "اكتمل الفحص العميق" : "اكتمل الفحص السريع";
        _progress.Progress = 1;
    }

    private async Task CompareAsync(Button cancel)
    {
        var picked =
            await FilePicker.Default.PickMultipleAsync(
                new PickOptions
                {
                    PickerTitle =
                        "اختر صورتين للمقارنة"
                });

        var items =
            picked?
                .Take(2)
                .ToArray() ??
            Array.Empty<FileResult>();

        if (items.Length != 2)
        {
            _status.Text =
                "اختر صورتين بالضبط";
            return;
        }

        string? a = null;
        string? b = null;

        try
        {
            SetBusy(
                true,
                cancel);

            a =
                await CopyToCacheAsync(
                    items[0],
                    _cts!.Token);

            b =
                await CopyToCacheAsync(
                    items[1],
                    _cts.Token);

            var result =
                await _comparison.CompareAsync(
                    a,
                    b,
                    _cts.Token);

            var dir =
                Path.Combine(
                    FileSystem.AppDataDirectory,
                    "exports");

            Directory.CreateDirectory(
                dir);

            var stamp =
                DateTimeOffset.UtcNow
                    .ToString(
                        "yyyyMMddHHmmss");

            var diffPath =
                Path.Combine(
                    dir,
                    $"compare-difference-{stamp}.png");

            var heatPath =
                Path.Combine(
                    dir,
                    $"compare-heatmap-{stamp}.png");

            var overlayPath =
                Path.Combine(
                    dir,
                    $"compare-overlay-{stamp}.png");

            var contactPath =
                Path.Combine(
                    dir,
                    $"compare-side-by-side-{stamp}.png");

            var jsonPath =
                Path.Combine(
                    dir,
                    $"compare-{stamp}.json");

            var textPath =
                Path.Combine(
                    dir,
                    $"compare-{stamp}.txt");

            await _visuals.CreateDifferenceMapAsync(
                a,
                b,
                diffPath,
                _cts.Token);

            await _visuals.CreateComparisonHeatmapAsync(
                a,
                b,
                heatPath,
                _cts.Token);

            await _visuals.CreateComparisonOverlayAsync(
                a,
                b,
                overlayPath,
                _cts.Token);

            await _visuals.CreateComparisonContactSheetAsync(
                a,
                b,
                contactPath,
                _cts.Token);

            var comparisonText =
                _writer.ComparisonToText(
                    result);

            await File.WriteAllTextAsync(
                jsonPath,
                _writer.ComparisonToJson(
                    result),
                _cts.Token);

            await File.WriteAllTextAsync(
                textPath,
                comparisonText,
                _cts.Token);

            _result.Text =
                comparisonText +
                $"\nArtifacts:\n- {diffPath}\n- {heatPath}\n- {overlayPath}\n- {contactPath}\n- {jsonPath}\n- {textPath}";

            _status.Text =
                "اكتملت مقارنة الصورتين";

            await Share.Default.RequestAsync(
                new ShareMultipleFilesRequest(
                    "Image comparison lab",
                    new List<ShareFile>
                    {
                        new(diffPath),
                        new(heatPath),
                        new(overlayPath),
                        new(contactPath),
                        new(jsonPath),
                        new(textPath)
                    }));
        }
        catch (OperationCanceledException)
        {
            _status.Text =
                "تم الإلغاء";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(
                false,
                cancel);

            TryDelete(a);
            TryDelete(b);
        }
    }

    private async Task BatchAsync(Button cancel)
    {
        var picked =
            await FilePicker.Default.PickMultipleAsync(
                new PickOptions
                {
                    PickerTitle =
                        "اختر صور المجموعة"
                });

        var items =
            picked?
                .Take(50)
                .ToArray() ??
            Array.Empty<FileResult>();

        if (items.Length == 0)
            return;

        var rows =
            new List<BatchReportRow>();

        var exactByHash =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        try
        {
            SetBusy(
                true,
                cancel);

            for (var i = 0;
                 i < items.Length;
                 i++)
            {
                _cts!.Token.ThrowIfCancellationRequested();

                var item =
                    items[i];

                _status.Text =
                    $"Batch {i + 1}/{items.Length}";

                string? temp = null;

                try
                {
                    temp =
                        await CopyToCacheAsync(
                            item,
                            _cts.Token);

                    var report =
                        await _scanner.DeepScanAsync(
                            temp,
                            item.ContentType,
                            null,
                            _cts.Token);

                    string? duplicateOf = null;

                    if (exactByHash.TryGetValue(
                            report.Identity.Sha256,
                            out var firstExact))
                    {
                        duplicateOf =
                            firstExact;
                    }
                    else
                    {
                        exactByHash[
                            report.Identity.Sha256] =
                            item.FileName;
                    }

                    string? nearDuplicateOf = null;

                    if (duplicateOf is null &&
                        report.PerceptualHashes is not null)
                    {
                        var candidate =
                            rows
                                .Where(r =>
                                    string.IsNullOrWhiteSpace(
                                        r.Error) &&
                                    !string.IsNullOrWhiteSpace(
                                        r.PHash))
                                .Select(r => new
                                {
                                    Row = r,
                                    Similarity =
                                        PerceptualHashResult.Similarity64(
                                            r.PHash,
                                            report.PerceptualHashes.PHash)
                                })
                                .Where(x =>
                                    x.Similarity >= 0.95)
                                .OrderByDescending(x =>
                                    x.Similarity)
                                .FirstOrDefault();

                        nearDuplicateOf =
                            candidate?.Row.FileName;
                    }

                    rows.Add(
                        new BatchReportRow(
                            item.FileName,
                            report.Identity.Sha256,
                            report.Identity.DetectedType,
                            report.Identity.SizeBytes,
                            report.Technical?.Width ?? 0,
                            report.Technical?.Height ?? 0,
                            report.Technical?.AspectRatio ?? 0,
                            report.Metadata?.Gps is not null,
                            report.Privacy?.Risks.Count ?? 0,
                            report.Indicators.Count,
                            report.Barcodes.Count,
                            report.Ocr?.Text?.Length ?? 0,
                            report.PerceptualHashes?.AHash ?? string.Empty,
                            report.PerceptualHashes?.DHash ?? string.Empty,
                            report.PerceptualHashes?.PHash ?? string.Empty,
                            duplicateOf,
                            nearDuplicateOf,
                            null));
                }
                catch (Exception ex)
                    when (ex is not OperationCanceledException)
                {
                    rows.Add(
                        new BatchReportRow(
                            item.FileName,
                            string.Empty,
                            "ERROR",
                            0,
                            0,
                            0,
                            0,
                            false,
                            0,
                            0,
                            0,
                            0,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            null,
                            null,
                            $"{ex.GetType().Name}: {ex.Message}"));
                }
                finally
                {
                    TryDelete(temp);
                }
            }

            _lastBatchRows =
                rows.ToArray();

            var dir =
                Path.Combine(
                    FileSystem.AppDataDirectory,
                    "exports");

            Directory.CreateDirectory(
                dir);

            var stamp =
                DateTimeOffset.UtcNow
                    .ToString(
                        "yyyyMMddHHmmss");

            var csvPath =
                Path.Combine(
                    dir,
                    $"batch-{stamp}.csv");

            var jsonPath =
                Path.Combine(
                    dir,
                    $"batch-{stamp}.json");

            await File.WriteAllTextAsync(
                csvPath,
                _writer.BatchToCsv(rows),
                _cts.Token);

            await File.WriteAllTextAsync(
                jsonPath,
                _writer.BatchToJson(rows),
                _cts.Token);

            ApplyBatchFilter();

            _status.Text =
                $"اكتمل Batch: {rows.Count} ملف";

            await Share.Default.RequestAsync(
                new ShareMultipleFilesRequest(
                    "Batch CSV + JSON",
                    new List<ShareFile>
                    {
                        new(csvPath),
                        new(jsonPath)
                    }));
        }
        catch (OperationCanceledException)
        {
            _status.Text =
                "تم إلغاء Batch";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(
                false,
                cancel);
        }
    }

    private void ApplyBatchFilter()
    {
        if (_lastBatchRows.Count == 0)
        {
            _status.Text =
                "لا توجد نتائج Batch بعد";
            return;
        }

        var selected =
            _batchFilter.SelectedItem?
                .ToString() ??
            "الكل";

        IEnumerable<BatchReportRow> filtered =
            selected switch
            {
                "GPS" =>
                    _lastBatchRows.Where(
                        r => r.HasGps),
                "مخاطر خصوصية" =>
                    _lastBatchRows.Where(
                        r => r.PrivacyRiskCount > 0),
                "نسخ مطابقة" =>
                    _lastBatchRows.Where(
                        r => !string.IsNullOrWhiteSpace(
                            r.DuplicateOf)),
                "نسخ متشابهة" =>
                    _lastBatchRows.Where(
                        r => !string.IsNullOrWhiteSpace(
                            r.NearDuplicateOf)),
                "أخطاء" =>
                    _lastBatchRows.Where(
                        r => !string.IsNullOrWhiteSpace(
                            r.Error)),
                _ =>
                    _lastBatchRows
            };

        var rows =
            filtered.ToArray();

        _result.Text =
            $"Batch filter: {selected}\n" +
            $"Matches: {rows.Length}/{_lastBatchRows.Count}\n\n" +
            _writer.BatchToCsv(rows);

        _status.Text =
            $"فلتر Batch: {rows.Length} نتيجة";
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

            var beforeRisks =
                r.PrivacyRiskCodesBefore.Count == 0
                    ? "none"
                    : string.Join(", ", r.PrivacyRiskCodesBefore);

            var afterRisks =
                r.PrivacyRiskCodesAfter.Count == 0
                    ? "none"
                    : string.Join(", ", r.PrivacyRiskCodesAfter);

            var verificationNotes =
                r.VerificationNotes.Count == 0
                    ? "none"
                    : string.Join(" | ", r.VerificationNotes);

            _result.Text =
                $"النسخة النظيفة: {r.OutputPath}\n" +
                $"Format: {r.OutputFormat}\n" +
                $"Metadata before/after: {r.MetadataFieldsBefore}/{r.MetadataFieldsAfter}\n" +
                $"Privacy risks before/after: {r.PrivacyRisksBefore}/{r.PrivacyRisksAfter}\n" +
                $"Risk codes before: {beforeRisks}\n" +
                $"Risk codes after: {afterRisks}\n" +
                $"Trailing bytes before/after: {r.TrailingBytesBefore}/{r.TrailingBytesAfter}\n" +
                $"Orientation: {r.SourceOrientation} → {r.CleanOrientation}; applied={r.OrientationApplied}\n" +
                $"Dimensions: {r.SourceWidth}x{r.SourceHeight} → {r.CleanWidth}x{r.CleanHeight}\n" +
                $"GPS removed: {r.GpsRemoved}\n" +
                $"Source unchanged by hash: {r.OriginalUntouched}\n" +
                $"Verification passed: {r.VerificationPassed}\n" +
                $"Verification notes: {verificationNotes}\n" +
                $"Source SHA-256: {r.OriginalSha256}\n" +
                $"Clean SHA-256: {r.CleanSha256}";

            if (!r.VerificationPassed)
            {
                _status.Text =
                    "تم إنشاء نسخة، لكن فحص الخصوصية بعد التنظيف لم ينجح بالكامل؛ لم يتم فتح المشاركة.";
                return;
            }

            _status.Text =
                "تم إنشاء النسخة النظيفة والتحقق منها";

            await Share.Default.RequestAsync(new ShareFileRequest(
                "Verified clean image",
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

            await File.WriteAllTextAsync(
                json,
                _writer.ToJson(_last));

            await File.WriteAllTextAsync(
                txt,
                _writer.ToText(_last));

            _writer.WritePdf(
                _last,
                pdf);

            var manifest =
                Path.Combine(
                    dir,
                    $"report-{stamp}.sha256.txt");

            static async Task<string> Sha256FileAsync(
                string path)
            {
                await using var stream =
                    File.OpenRead(path);

                var hash =
                    await System.Security.Cryptography.SHA256.HashDataAsync(
                        stream);

                return Convert
                    .ToHexString(hash)
                    .ToLowerInvariant();
            }

            var manifestText =
                $"{await Sha256FileAsync(json)}  {Path.GetFileName(json)}\n" +
                $"{await Sha256FileAsync(txt)}  {Path.GetFileName(txt)}\n" +
                $"{await Sha256FileAsync(pdf)}  {Path.GetFileName(pdf)}\n";

            await File.WriteAllTextAsync(
                manifest,
                manifestText);

            await Share.Default.RequestAsync(
                new ShareMultipleFilesRequest(
                    "Image Forensics report",
                    new List<ShareFile>
                    {
                        new(json),
                        new(txt),
                        new(pdf),
                        new(manifest)
                    }));

            _status.Text =
                "تم تصدير JSON / TXT / PDF + SHA256 manifest";
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

    private async Task CopyOcrAsync()
    {
        var text = _last?.Ocr?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            _status.Text = "لا يوجد نص OCR في نتيجة الفحص الحالي";
            return;
        }

        await Clipboard.Default.SetTextAsync(text);
        _status.Text = "تم نسخ نص OCR";
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
            else if (kind == "ENTROPY")
                await _visuals.CreateEntropyMapAsync(_lastSource, path);
            else if (kind == "HISTOGRAM")
                await _visuals.CreateHistogramAsync(_lastSource, path);
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

    private async Task OpenReverseSearchAsync()
    {
        if (!_onlineMode.IsToggled)
        {
            _status.Text =
                "فعّل Online Mode أولًا";
            return;
        }

        var confirmed =
            await DisplayAlert(
                "خدمة خارجية",
                OnlineFeaturePolicy.ReverseSearchDisclosure,
                "فتح المتصفح",
                "إلغاء");

        if (!confirmed)
            return;

        await Launcher.Default.OpenAsync(
            OnlineFeaturePolicy.ReverseSearchHome());
    }

    private async Task OpenHashReputationAsync()
    {
        if (!_onlineMode.IsToggled)
        {
            _status.Text =
                "فعّل Online Mode أولًا";
            return;
        }

        var hash =
            _last?.Identity.Sha256;

        if (string.IsNullOrWhiteSpace(hash))
        {
            _status.Text =
                "نفّذ فحصًا أولًا للحصول على SHA-256";
            return;
        }

        var confirmed =
            await DisplayAlert(
                "إرسال Hash إلى خدمة خارجية",
                OnlineFeaturePolicy.HashReputationDisclosure,
                "فتح المتصفح",
                "إلغاء");

        if (!confirmed)
            return;

        await Launcher.Default.OpenAsync(
            OnlineFeaturePolicy.HashReputationSearch(hash));
    }

    private void RenderSelectedSection()
    {
        if (_last is null)
            return;

        var section =
            _resultSection.SelectedItem?.ToString() ??
            "Overview";

        var report = _last;
        var sb = new StringBuilder();

        switch (section)
        {
            case "Overview":
                sb.AppendLine($"File: {report.Identity.FileName}");
                sb.AppendLine($"Type: {report.Identity.DetectedType}");
                sb.AppendLine($"Size: {report.Identity.SizeBytes} bytes");
                sb.AppendLine($"SHA-256: {report.Identity.Sha256}");
                if (report.Technical is not null)
                    sb.AppendLine($"Dimensions: {report.Technical.Width}x{report.Technical.Height}");
                sb.AppendLine($"Indicators: {report.Indicators.Count}");
                break;

            case "Metadata":
                if (report.Metadata is not null)
                {
                    foreach (var field in report.Metadata.Fields.Take(500))
                    {
                        sb.AppendLine($"[{field.Directory}] {field.Tag}");
                        sb.AppendLine($"  {field.ParsedValue ?? field.RawValue}");
                        sb.AppendLine($"  Confidence={field.Confidence}; Meaning={field.Meaning}");
                    }
                }
                break;

            case "GPS":
                if (report.Metadata?.Gps is { } gps)
                    sb.AppendLine($"{gps.Latitude:F8}, {gps.Longitude:F8}");
                else
                    sb.AppendLine("لا توجد GPS صريحة قابلة للقراءة.");
                break;

            case "Structure":
                if (report.Container is not null)
                {
                    sb.AppendLine($"Format={report.Container.Format}; trailing={report.Container.TrailingBytes}");
                    foreach (var segment in report.Container.Segments.Take(500))
                        sb.AppendLine($"offset={segment.Offset} size={segment.Length} {segment.Type} — {segment.Description}");
                }
                break;

            case "Forensics":
                foreach (var item in report.Indicators)
                {
                    sb.AppendLine($"[{item.Confidence}] {item.Title}");
                    sb.AppendLine($"Evidence: {item.Evidence}");
                    sb.AppendLine($"Limitation: {item.Limitation}");
                }
                break;

            case "OCR":
                sb.AppendLine(report.Ocr?.Text ?? "لا توجد نتيجة OCR.");
                foreach (var hit in report.Barcodes)
                    sb.AppendLine($"[{hit.Format}] {hit.Text}");
                break;

            case "Privacy":
                if (report.Privacy?.Risks is { Count: > 0 } risks)
                {
                    foreach (var risk in risks)
                        sb.AppendLine($"[{risk.Confidence}] {risk.Title}: {risk.Evidence}");
                }
                else
                {
                    sb.AppendLine("لم تُكتشف مخاطر خصوصية مهيأة.");
                }
                break;

            default:
                sb.Append(_writer.ToText(report));
                break;
        }

        _result.Text = sb.ToString();
    }

    private MetadataField? FirstMetadataMatch()
    {
        if (_last?.Metadata is null)
            return null;

        var q = _metadataSearch.Text?.Trim();

        if (string.IsNullOrWhiteSpace(q))
            return _last.Metadata.Fields.FirstOrDefault();

        return _last.Metadata.Fields.FirstOrDefault(
            x =>
                x.Directory.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Tag.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (x.ParsedValue?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.RawValue?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private async Task CopyFirstMetadataMatchAsync()
    {
        var field = FirstMetadataMatch();

        if (field is null)
        {
            _status.Text = "لا يوجد حقل Metadata مطابق";
            return;
        }

        await Clipboard.Default.SetTextAsync(
            $"{field.Directory} / {field.Tag}: {field.ParsedValue ?? field.RawValue}");

        _status.Text = $"تم نسخ الحقل: {field.Tag}";
    }

    private void ExplainFirstMetadataMatch()
    {
        var field = FirstMetadataMatch();

        if (field is null)
        {
            _status.Text = "لا يوجد حقل Metadata مطابق";
            return;
        }

        _result.Text =
            $"Field: [{field.Directory}] {field.Tag}\n" +
            $"Value: {field.ParsedValue ?? field.RawValue}\n" +
            $"Meaning: {field.Meaning}\n" +
            $"Confidence: {field.Confidence}\n" +
            $"Source: {field.Source}\n\n" +
            "ملاحظة: وجود Metadata لا يثبت وحده مصدر الصورة أو أصالتها.";
    }

    private async Task ShowHistoryAsync()
    {
        var entries = await _history.GetRecentAsync(50);

        if (entries.Count == 0)
        {
            _result.Text = "السجل فارغ.";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"History: {entries.Count} entries");

        foreach (var item in entries)
        {
            sb.AppendLine();
            sb.AppendLine($"{item.ScannedAtUtc:O} — {item.FileName}");
            sb.AppendLine($"Type={item.DetectedType}; Size={item.SizeBytes}; Deep={item.DeepScan}");
            sb.AppendLine($"Indicators={item.IndicatorCount}; Privacy={item.PrivacyRiskCount}");
            sb.AppendLine($"SHA-256={item.Sha256}");
        }

        _result.Text = sb.ToString();
    }

    private async Task ClearHistoryAsync()
    {
        await _history.ClearAsync();
        _result.Text = "تم مسح السجل المحلي.";
        _status.Text = "السجل فارغ";
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
