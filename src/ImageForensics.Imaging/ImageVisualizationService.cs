using ImageForensics.Core.Abstractions;
using SkiaSharp;

namespace ImageForensics.Imaging;

public sealed class ImageVisualizationService : IImageVisualizationService
{
    public Task CreateElaPreviewAsync(string inputPath,string outputPngPath,CancellationToken ct=default)
        => Task.Run(()=>TransformEla(inputPath,outputPngPath,ct),ct);

    public Task CreateBitPlaneAsync(string inputPath,int bitPlane,string outputPngPath,CancellationToken ct=default)
        => Task.Run(()=>TransformBitPlane(inputPath,bitPlane,outputPngPath,ct),ct);

    public Task CreateRgbChannelAsync(string inputPath,string channel,string outputPngPath,CancellationToken ct=default)
        => Task.Run(()=>TransformChannel(inputPath,channel,outputPngPath,ct),ct);

    public Task CreateDifferenceMapAsync(string leftPath,string rightPath,string outputPngPath,CancellationToken ct=default)
        => Task.Run(()=>TransformDifference(leftPath,rightPath,outputPngPath,ct),ct);

    private static void TransformEla(string input,string output,CancellationToken ct)
    {
        using var src=LoadPreview(input,1024);
        using var img=SKImage.FromBitmap(src);
        using var jpg=img.Encode(SKEncodedImageFormat.Jpeg,90);
        using var rec=SKBitmap.Decode(jpg) ?? throw new InvalidDataException("ELA decode failed.");
        using var dest=new SKBitmap(src.Width,src.Height,SKColorType.Rgba8888,SKAlphaType.Opaque);
        for(var y=0;y<src.Height;y++)
        {
            ct.ThrowIfCancellationRequested();
            for(var x=0;x<src.Width;x++)
            {
                var a=src.GetPixel(x,y);var b=rec.GetPixel(x,y);
                var r=(byte)Math.Min(255,Math.Abs(a.Red-b.Red)*8);
                var g=(byte)Math.Min(255,Math.Abs(a.Green-b.Green)*8);
                var bl=(byte)Math.Min(255,Math.Abs(a.Blue-b.Blue)*8);
                dest.SetPixel(x,y,new SKColor(r,g,bl));
            }
        }
        SavePng(dest,output);
    }

    private static void TransformBitPlane(string input,int plane,string output,CancellationToken ct)
    {
        if(plane is <0 or >7)throw new ArgumentOutOfRangeException(nameof(plane));
        using var src=LoadPreview(input,1024);
        using var dest=new SKBitmap(src.Width,src.Height,SKColorType.Rgba8888,SKAlphaType.Opaque);
        for(var y=0;y<src.Height;y++)
        {
            ct.ThrowIfCancellationRequested();
            for(var x=0;x<src.Width;x++)
            {
                var p=src.GetPixel(x,y);
                var on=(((p.Red>>plane)&1)+((p.Green>>plane)&1)+((p.Blue>>plane)&1))>=2;
                var v=(byte)(on?255:0); dest.SetPixel(x,y,new SKColor(v,v,v));
            }
        }
        SavePng(dest,output);
    }

    private static void TransformChannel(string input,string channel,string output,CancellationToken ct)
    {
        var c=channel.Trim().ToUpperInvariant();
        if(c is not ("R" or "G" or "B"))throw new ArgumentException("Channel must be R, G or B.",nameof(channel));
        using var src=LoadPreview(input,1024);
        using var dest=new SKBitmap(src.Width,src.Height,SKColorType.Rgba8888,SKAlphaType.Opaque);
        for(var y=0;y<src.Height;y++)
        {
            ct.ThrowIfCancellationRequested();
            for(var x=0;x<src.Width;x++)
            {
                var p=src.GetPixel(x,y); var v=c=="R"?p.Red:c=="G"?p.Green:p.Blue;
                dest.SetPixel(x,y,new SKColor(v,v,v));
            }
        }
        SavePng(dest,output);
    }

    private static void TransformDifference(string left,string right,string output,CancellationToken ct)
    {
        using var a=LoadFixed(left,512,512);
        using var b=LoadFixed(right,512,512);
        using var dest=new SKBitmap(512,512,SKColorType.Rgba8888,SKAlphaType.Opaque);
        for(var y=0;y<512;y++)
        {
            ct.ThrowIfCancellationRequested();
            for(var x=0;x<512;x++)
            {
                var p=a.GetPixel(x,y);var q=b.GetPixel(x,y);
                var d=(byte)Math.Min(255,(Math.Abs(p.Red-q.Red)+Math.Abs(p.Green-q.Green)+Math.Abs(p.Blue-q.Blue))/3*3);
                dest.SetPixel(x,y,new SKColor(d,d,d));
            }
        }
        SavePng(dest,output);
    }

    private static SKBitmap LoadPreview(string path,int max)
    {
        using var src=SKBitmap.Decode(path)??throw new InvalidDataException("Unable to decode image.");
        if(src.Width<=max&&src.Height<=max)return src.Copy();
        var scale=Math.Min((double)max/src.Width,(double)max/src.Height);
        return src.Resize(new SKImageInfo(Math.Max(1,(int)(src.Width*scale)),Math.Max(1,(int)(src.Height*scale))),SKSamplingOptions.Default)
            ?? throw new InvalidDataException("Unable to resize image.");
    }

    private static SKBitmap LoadFixed(string path,int w,int h)
    {
        using var src=SKBitmap.Decode(path)??throw new InvalidDataException("Unable to decode image.");
        return src.Resize(new SKImageInfo(w,h),SKSamplingOptions.Default)??throw new InvalidDataException("Unable to normalize image.");
    }

    private static void SavePng(SKBitmap bitmap,string output)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(output)??".");
        using var image=SKImage.FromBitmap(bitmap);
        using var data=image.Encode(SKEncodedImageFormat.Png,100)??throw new InvalidOperationException("PNG encode failed.");
        using var fs=File.Create(output); data.SaveTo(fs);
    }
}
