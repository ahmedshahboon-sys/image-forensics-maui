using System.Text;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using Microsoft.Maui.Storage;

namespace ImageForensics.App;

public sealed class MainPage : ContentPage
{
    private readonly IFileIdentityInspector _inspector;
    private readonly Label _status;
    private readonly Editor _result;
    private readonly ProgressBar _progress;
    private CancellationTokenSource? _cts;

    public MainPage(IFileIdentityInspector inspector)
    {
        _inspector = inspector;
        Title = "فاحص الصور";
        FlowDirection = FlowDirection.RightToLeft;

        var title = new Label { Text = "Image Forensics — فاحص الصور", FontSize = 26, FontAttributes = FontAttributes.Bold };
        var subtitle = new Label { Text = "فحص محلي للملف مع فصل الحقائق المؤكدة عن المؤشرات الاحتمالية.", FontSize = 14 };
        var pick = new Button { Text = "اختيار صورة — فحص سريع" };
        var cancel = new Button { Text = "إلغاء الفحص", IsEnabled = false };
        _status = new Label { Text = "جاهز" };
        _progress = new ProgressBar { Progress = 0 };
        _result = new Editor { IsReadOnly = true, AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 320, FlowDirection = FlowDirection.LeftToRight };

        pick.Clicked += async (_, _) =>
        {
            try
            {
                var selected = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "اختر صورة للفحص" });
                if (selected is null) return;

                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                cancel.IsEnabled = true;
                pick.IsEnabled = false;
                _result.Text = string.Empty;

                var progress = new Progress<AnalysisProgress>(p =>
                {
                    _status.Text = $"{p.Stage}: {p.Detail}";
                    _progress.Progress = p.Percent;
                });

                var r = await _inspector.InspectAsync(selected.FullPath, selected.ContentType, progress, _cts.Token);
                _result.Text = Format(r);
                _status.Text = "اكتمل الفحص الأساسي";
            }
            catch (OperationCanceledException) { _status.Text = "تم إلغاء الفحص"; }
            catch (Exception ex)
            {
                _status.Text = "تعذر فحص الملف";
                _result.Text = $"نوع الخطأ: {ex.GetType().Name}\nالتفاصيل: {ex.Message}";
            }
            finally
            {
                cancel.IsEnabled = false;
                pick.IsEnabled = true;
            }
        };

        cancel.Clicked += (_, _) => _cts?.Cancel();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children = { title, subtitle, pick, cancel, _progress, _status, _result }
            }
        };
    }

    private static string Format(FileIdentityResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"الاسم: {r.FileName}");
        sb.AppendLine($"الحجم: {r.SizeBytes:N0} bytes");
        sb.AppendLine($"الامتداد: {r.Extension}");
        sb.AppendLine($"MIME المعلن: {r.DeclaredMime}");
        sb.AppendLine($"النوع الحقيقي: {r.DetectedType} ({r.DetectedMime})");
        sb.AppendLine($"تعارض الامتداد: {(r.ExtensionMismatch ? "نعم" : "لا")}");
        sb.AppendLine($"Entropy: {r.EntropyBitsPerByte:F4} bits/byte");
        sb.AppendLine($"SHA-256: {r.Sha256}");
        sb.AppendLine($"SHA-1: {r.Sha1}");
        sb.AppendLine($"MD5: {r.Md5}  [للمقارنة فقط، ليس للأمان]");
        sb.AppendLine($"Magic bytes: {r.SignatureHex}");
        sb.AppendLine();
        sb.AppendLine("ملاحظة: هذه نتائج تقنية مؤكدة من الملف، وليست حكمًا على أصالة الصورة.");
        return sb.ToString();
    }
}
