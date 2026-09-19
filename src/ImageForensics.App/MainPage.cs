using System.Text;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using ImageForensics.Reporting;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace ImageForensics.App;

public sealed class MainPage : ContentPage
{
    private readonly ScanCoordinator _scanner;
    private readonly IMetadataCleaner _cleaner;
    private readonly IImageComparisonService _comparison;
    private readonly IImageVisualizationService _visuals;
    private readonly ReportWriter _writer;
    private readonly Editor _result=new(){IsReadOnly=true,AutoSize=EditorAutoSizeOption.TextChanges,MinimumHeightRequest=380,FlowDirection=FlowDirection.LeftToRight};
    private readonly Label _status=new(){Text="جاهز"};
    private readonly ProgressBar _progress=new(){Progress=0};
    private ScanReport? _last;
    private string? _lastSource;
    private CancellationTokenSource? _cts;

    public MainPage(ScanCoordinator scanner,IMetadataCleaner cleaner,IImageComparisonService comparison,IImageVisualizationService visuals,ReportWriter writer)
    {
        _scanner=scanner;_cleaner=cleaner;_comparison=comparison;_visuals=visuals;_writer=writer;
        Title="فاحص الصور"; FlowDirection=FlowDirection.RightToLeft;

        var title=new Label{Text="Image Forensics — فاحص الصور",FontSize=26,FontAttributes=FontAttributes.Bold};
        var subtitle=new Label{Text="Offline افتراضيًا — الحقائق منفصلة عن المؤشرات الاحتمالية.",FontSize=14};
        var deep=new Button{Text="فحص عميق"};
        var compare=new Button{Text="مقارنة صورتين"};
        var batch=new Button{Text="فحص مجموعة + CSV"};
        var clean=new Button{Text="إنشاء نسخة نظيفة"};
        var export=new Button{Text="تصدير ومشاركة التقرير"};
        var gps=new Button{Text="فتح GPS في الخرائط"};
        var ela=new Button{Text="إنشاء ELA مساعد"};
        var cancel=new Button{Text="إلغاء العملية",IsEnabled=false};
        var theme=new Button{Text="تبديل Light / Dark"};

        deep.Clicked+=async(_,_)=>await ScanPickedAsync(deep,cancel);
        compare.Clicked+=async(_,_)=>await CompareAsync(cancel);
        batch.Clicked+=async(_,_)=>await BatchAsync(cancel);
        clean.Clicked+=async(_,_)=>await CleanAsync(cancel);
        export.Clicked+=async(_,_)=>await ExportAsync();
        gps.Clicked+=async(_,_)=>await OpenGpsAsync();
        ela.Clicked+=async(_,_)=>await CreateElaAsync();
        cancel.Clicked+=(_,_)=>_cts?.Cancel();
        theme.Clicked+=(_,_)=>Application.Current!.UserAppTheme=Application.Current.UserAppTheme==AppTheme.Dark?AppTheme.Light:AppTheme.Dark;

        Content=new ScrollView{Content=new VerticalStackLayout
        {
            Padding=20,Spacing=10,
            Children={title,subtitle,deep,compare,batch,clean,export,gps,ela,cancel,theme,_progress,_status,_result}
        }};
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        var uri=SharedImageInbox.Take();
        if(uri is not null)
        {
            try
            {
                var cache=Path.Combine(FileSystem.CacheDirectory,"forensics");
                Directory.CreateDirectory(cache);
                var path=Path.Combine(cache,Guid.NewGuid().ToString("N")+".shared");
                await using var input=Android.App.Application.Context.ContentResolver!.OpenInputStream(uri)
                    ?? throw new IOException("تعذر فتح الصورة المشتركة.");
                await using var output=File.Create(path);
                await input.CopyToAsync(output);
                await RunDeepAsync(path,null,null);
            }
            catch(Exception ex){_status.Text="تعذر قراءة الصورة المشتركة";_result.Text=ex.Message;}
        }
#endif
    }

    private async Task ScanPickedAsync(Button action,Button cancel)
    {
        var selected=await FilePicker.Default.PickAsync(new PickOptions{PickerTitle="اختر صورة للفحص"});
        if(selected is null)return;
        string? temp=null;
        try
        {
            SetBusy(true,cancel); temp=await CopyToCacheAsync(selected,_cts!.Token);
            await RunDeepAsync(temp,selected.ContentType,selected.FileName);
        }
        catch(OperationCanceledException){_status.Text="تم الإلغاء";}
        catch(Exception ex){ShowError(ex);}
        finally{SetBusy(false,cancel); if(temp is not null)TryDelete(temp);}
    }

    private async Task RunDeepAsync(string path,string? mime,string? displayName)
    {
        _lastSource=path;
        var progress=new Progress<AnalysisProgress>(p=>{_status.Text=$"{p.Stage}: {p.Detail}";_progress.Progress=p.Percent;});
        _last=await _scanner.DeepScanAsync(path,mime,progress,_cts?.Token??CancellationToken.None);
        if(!string.IsNullOrWhiteSpace(displayName))
            _last=_last with{Identity=_last.Identity with{FileName=displayName}};
        _result.Text=_writer.ToText(_last);
        _status.Text="اكتمل الفحص العميق";
    }

    private async Task CompareAsync(Button cancel)
    {
        var items=(await FilePicker.Default.PickMultipleAsync(new PickOptions{PickerTitle="اختر صورتين للمقارنة"})).Take(2).ToArray();
        if(items.Length!=2){_status.Text="اختر صورتين بالضبط";return;}
        string? a=null,b=null;
        try
        {
            SetBusy(true,cancel);a=await CopyToCacheAsync(items[0],_cts!.Token);b=await CopyToCacheAsync(items[1],_cts.Token);
            var r=await _comparison.CompareAsync(a,b,_cts.Token);
            var diffPath=Path.Combine(FileSystem.AppDataDirectory,"exports",$"difference-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.png");
            await _visuals.CreateDifferenceMapAsync(a,b,diffPath,_cts.Token);
            _result.Text=$"Exact SHA-256 match: {r.ExactMatch}\nDimensions: {r.LeftWidth}x{r.LeftHeight} vs {r.RightWidth}x{r.RightHeight}\naHash similarity: {r.AHashSimilarity:P2}\ndHash similarity: {r.DHashSimilarity:P2}\npHash similarity: {r.PHashSimilarity:P2}\nMetadata differences: {r.MetadataDifferences.Count}\nDifference map: {diffPath}\n\nالتشابه البصري مؤشر تقريبي وليس إثباتًا على مصدر أو تزوير.";
            _status.Text="اكتملت المقارنة";
        }
        catch(OperationCanceledException){_status.Text="تم الإلغاء";}
        catch(Exception ex){ShowError(ex);}
        finally{SetBusy(false,cancel);TryDelete(a);TryDelete(b);}
    }

    private async Task BatchAsync(Button cancel)
    {
        var items=(await FilePicker.Default.PickMultipleAsync(new PickOptions{PickerTitle="اختر صور المجموعة"})).Take(50).ToArray();
        if(items.Length==0)return;
        var rows=new List<BatchReportRow>();
        try
        {
            SetBusy(true,cancel);
            for(var i=0;i<items.Length;i++)
            {
                _cts!.Token.ThrowIfCancellationRequested();
                _status.Text=$"Batch {i+1}/{items.Length}";
                string? temp=null;
                try
                {
                    temp=await CopyToCacheAsync(items[i],_cts.Token);
                    var report=await _scanner.DeepScanAsync(temp,items[i].ContentType,null,_cts.Token);
                    rows.Add(new BatchReportRow(items[i].FileName,report.Identity.Sha256,report.Identity.DetectedType,report.Identity.SizeBytes,
                        report.Technical?.Width??0,report.Technical?.Height??0,report.Metadata?.Gps is not null,report.Privacy?.Risks.Count??0,report.Indicators.Count));
                }
                catch(Exception ex) when(ex is not OperationCanceledException)
                {
                    rows.Add(new BatchReportRow(items[i].FileName,"ERROR",ex.GetType().Name,0,0,0,false,0,0));
                }
                finally{TryDelete(temp);}
            }
            var dir=Path.Combine(FileSystem.AppDataDirectory,"exports");Directory.CreateDirectory(dir);
            var path=Path.Combine(dir,$"batch-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv");
            await File.WriteAllTextAsync(path,_writer.BatchToCsv(rows),_cts.Token);
            _result.Text=$"تم فحص {rows.Count} ملف.\nCSV: {path}";
            await Share.Default.RequestAsync(new ShareFileRequest("Batch CSV",new ShareFile(path)));
        }
        catch(OperationCanceledException){_status.Text="تم إلغاء Batch";}
        finally{SetBusy(false,cancel);}
    }

    private async Task CleanAsync(Button cancel)
    {
        var selected=await FilePicker.Default.PickAsync(new PickOptions{PickerTitle="اختر صورة لإنشاء نسخة بدون Metadata حساسة"});
        if(selected is null)return;
        string? temp=null;
        try
        {
            SetBusy(true,cancel);temp=await CopyToCacheAsync(selected,_cts!.Token);
            var dir=Path.Combine(FileSystem.AppDataDirectory,"cleaned");
            var r=await _cleaner.CreateCleanCopyAsync(temp,dir,_cts.Token);
            _result.Text=$"النسخة النظيفة: {r.OutputPath}\nFormat: {r.OutputFormat}\nMetadata before/after: {r.MetadataFieldsBefore}/{r.MetadataFieldsAfter}\nGPS removed: {r.GpsRemoved}\nOriginal untouched: {r.OriginalUntouched}\nSHA-256: {r.CleanSha256}";
            _status.Text="تم إنشاء النسخة النظيفة";
            await Share.Default.RequestAsync(new ShareFileRequest("Clean image",new ShareFile(r.OutputPath)));
        }
        catch(OperationCanceledException){_status.Text="تم الإلغاء";}
        catch(Exception ex){ShowError(ex);}
        finally{SetBusy(false,cancel);TryDelete(temp);}
    }

    private async Task ExportAsync()
    {
        if(_last is null){_status.Text="نفّذ فحصًا أولًا";return;}
        var dir=Path.Combine(FileSystem.AppDataDirectory,"exports");Directory.CreateDirectory(dir);
        var stamp=DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var json=Path.Combine(dir,$"report-{stamp}.json");
        var txt=Path.Combine(dir,$"report-{stamp}.txt");
        var pdf=Path.Combine(dir,$"report-{stamp}.pdf");
        await File.WriteAllTextAsync(json,_writer.ToJson(_last));
        await File.WriteAllTextAsync(txt,_writer.ToText(_last));
        _writer.WritePdf(_last,pdf);
        await Share.Default.RequestAsync(new ShareMultipleFilesRequest("Image Forensics report",
            new List<ShareFile>{new(json),new(txt),new(pdf)}));
        _status.Text="تم تصدير JSON/TXT/PDF";
    }

    private async Task OpenGpsAsync()
    {
        var gps=_last?.Metadata?.Gps;
        if(gps is null){_status.Text="لا توجد GPS صريحة في Metadata";return;}
        await Launcher.Default.OpenAsync($"geo:{gps.Latitude},{gps.Longitude}?q={gps.Latitude},{gps.Longitude}");
    }

    private async Task CreateElaAsync()
    {
        if(_lastSource is null||!File.Exists(_lastSource)){_status.Text="نفّذ فحصًا جديدًا ثم أنشئ ELA";return;}
        var dir=Path.Combine(FileSystem.AppDataDirectory,"exports");Directory.CreateDirectory(dir);
        var path=Path.Combine(dir,$"ela-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.png");
        await _visuals.CreateElaPreviewAsync(_lastSource,path);
        await Share.Default.RequestAsync(new ShareFileRequest("ELA helper image",new ShareFile(path)));
    }

    private void SetBusy(bool busy,Button cancel)
    {
        if(busy){_cts?.Dispose();_cts=new CancellationTokenSource();_progress.Progress=0;}
        cancel.IsEnabled=busy;
    }

    private static async Task<string> CopyToCacheAsync(FileResult selected,CancellationToken ct)
    {
        var folder=Path.Combine(FileSystem.CacheDirectory,"forensics");Directory.CreateDirectory(folder);
        var ext=Path.GetExtension(selected.FileName);
        if(ext.Length>12||ext.Any(ch=>!char.IsLetterOrDigit(ch)&&ch!='.'))ext=".img";
        var path=Path.Combine(folder,Guid.NewGuid().ToString("N")+ext);
        await using var source=await selected.OpenReadAsync();
        await using var target=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,128*1024,true);
        var buffer=new byte[128*1024];long total=0;const long max=512L*1024*1024;
        while(true){ct.ThrowIfCancellationRequested();var read=await source.ReadAsync(buffer,ct);if(read==0)break;total+=read;if(total>max)throw new InvalidDataException("الملف أكبر من الحد الآمن.");await target.WriteAsync(buffer.AsMemory(0,read),ct);}
        return path;
    }

    private void ShowError(Exception ex){_status.Text="حدث خطأ";_result.Text=$"{ex.GetType().Name}: {ex.Message}";}
    private static void TryDelete(string? path){if(string.IsNullOrWhiteSpace(path))return;try{File.Delete(path);}catch{}}
}
