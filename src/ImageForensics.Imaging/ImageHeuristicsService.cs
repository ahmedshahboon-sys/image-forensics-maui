using System.Buffers.Binary;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using SkiaSharp;

namespace ImageForensics.Imaging;

public sealed class ImageHeuristicsService : IImageHeuristicsService
{
    private const long MaxDecodedPixels = 60_000_000;

    public Task<ImageHeuristicsResult> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
        => Task.Run(() => AnalyzeCore(filePath, cancellationToken), cancellationToken);

    private static ImageHeuristicsResult AnalyzeCore(string path, CancellationToken ct)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException("Unsupported or corrupt image.");
        if ((long)codec.Info.Width * codec.Info.Height > MaxDecodedPixels)
            throw new InvalidDataException("Deep pixel heuristics skipped: image exceeds safe decoded-pixel limit.");

        using var original = SKBitmap.Decode(path) ?? throw new InvalidDataException("Unable to decode image.");
        using var preview = ResizeWithin(original, 1024);
        var (blockRatio, noiseCv, clonePairs) = AnalyzePixels(preview, ct);
        var ela = CalculateEla(preview, ct);
        var (quality, subsampling) = ParseJpeg(path);

        var indicators = new List<EvidenceItem>();

        if (blockRatio > 1.45)
            indicators.Add(new EvidenceItem(
                "jpeg.block_boundaries",
                "8×8 block boundaries are stronger than nearby interior transitions",
                $"Boundary/interior ratio={blockRatio:F3}",
                ForensicConfidence.Possible,
                "Luminance differences were compared at JPEG-style 8-pixel boundaries.",
                "Natural texture, sharpening and ordinary JPEG compression can produce the same pattern."));

        if (noiseCv > 0.55)
            indicators.Add(new EvidenceItem(
                "noise.inconsistency",
                "Noise/high-frequency energy varies strongly across image quadrants",
                $"Coefficient of variation={noiseCv:F3}",
                ForensicConfidence.Possible,
                "Simple high-pass residual energy was compared between four image regions.",
                "Lighting, depth-of-field, denoising and scene content can legitimately vary noise."));

        if (ela > 0.10)
            indicators.Add(new EvidenceItem(
                "ela.elevated",
                "ELA helper metric is elevated",
                $"Normalized mean difference={ela:F4}",
                ForensicConfidence.Possible,
                "A normalized preview was re-encoded at JPEG quality 90 and compared.",
                "ELA is not proof of editing and is highly sensitive to prior compression and image content."));

        if (clonePairs > 0)
            indicators.Add(new EvidenceItem(
                "copy_move.tile_candidates",
                "Similar non-adjacent tiles were found",
                $"{clonePairs} candidate tile pair(s)",
                ForensicConfidence.Possible,
                "Coarse 8×8 luminance hashes were compared across a 4×4 grid.",
                "Repeated textures, sky, walls and other uniform regions create false positives."));

        return new ImageHeuristicsResult(quality, subsampling, blockRatio, noiseCv, ela, clonePairs, indicators);
    }

    private static (double blockRatio, double noiseCv, int clonePairs) AnalyzePixels(SKBitmap b, CancellationToken ct)
    {
        var w=b.Width; var h=b.Height;
        double boundary=0, interior=0; long bc=0, ic=0;
        for (var y=1; y<h; y++)
        {
            if ((y & 63)==0) ct.ThrowIfCancellationRequested();
            for (var x=1; x<w; x++)
            {
                var l=Luma(b.GetPixel(x,y));
                var lx=Luma(b.GetPixel(x-1,y));
                var ly=Luma(b.GetPixel(x,y-1));
                var dx=Math.Abs(l-lx); var dy=Math.Abs(l-ly);
                if (x%8==0){boundary+=dx;bc++;} else {interior+=dx;ic++;}
                if (y%8==0){boundary+=dy;bc++;} else {interior+=dy;ic++;}
            }
        }
        var boundaryMean=bc==0?0:boundary/bc;
        var interiorMean=ic==0?0:interior/ic;
        var ratio=interiorMean<=0?0:boundaryMean/interiorMean;

        var q=new double[4]; var qc=new long[4];
        for (var y=1;y<h-1;y+=2)
        for (var x=1;x<w-1;x+=2)
        {
            var c=Luma(b.GetPixel(x,y));
            var avg=(Luma(b.GetPixel(x-1,y))+Luma(b.GetPixel(x+1,y))+Luma(b.GetPixel(x,y-1))+Luma(b.GetPixel(x,y+1)))/4.0;
            var qi=(y<h/2?0:2)+(x<w/2?0:1);
            q[qi]+=Math.Abs(c-avg); qc[qi]++;
        }
        var means=q.Select((v,i)=>qc[i]==0?0:v/qc[i]).ToArray();
        var mean=means.Average();
        var sd=Math.Sqrt(means.Select(v=>(v-mean)*(v-mean)).Average());
        var cv=mean<=0?0:sd/mean;

        var tileHashes=new List<(int x,int y,ulong hash)>();
        var tileW=Math.Max(8,w/4); var tileH=Math.Max(8,h/4);
        for(var gy=0;gy<4;gy++)
        for(var gx=0;gx<4;gx++)
        {
            var sx=Math.Min(w-1,gx*tileW); var sy=Math.Min(h-1,gy*tileH);
            var ex=Math.Min(w,sx+tileW); var ey=Math.Min(h,sy+tileH);
            tileHashes.Add((gx,gy,TileHash(b,sx,sy,ex,ey)));
        }
        var pairs=0;
        for(var i=0;i<tileHashes.Count;i++)
        for(var j=i+1;j<tileHashes.Count;j++)
        {
            var a=tileHashes[i]; var d=tileHashes[j];
            if(Math.Abs(a.x-d.x)<=1 && Math.Abs(a.y-d.y)<=1) continue;
            if(System.Numerics.BitOperations.PopCount(a.hash^d.hash)<=3) pairs++;
        }

        return (ratio,cv,pairs);
    }

    private static double CalculateEla(SKBitmap preview, CancellationToken ct)
    {
        using var image=SKImage.FromBitmap(preview);
        using var data=image.Encode(SKEncodedImageFormat.Jpeg,90);
        using var decoded=SKBitmap.Decode(data) ?? throw new InvalidDataException("ELA re-decode failed.");
        long sum=0,count=0;
        var step=Math.Max(1,preview.Width/512);
        for(var y=0;y<preview.Height;y+=step)
        {
            ct.ThrowIfCancellationRequested();
            for(var x=0;x<preview.Width;x+=step)
            {
                var a=preview.GetPixel(x,y); var b=decoded.GetPixel(x,y);
                sum+=Math.Abs(a.Red-b.Red)+Math.Abs(a.Green-b.Green)+Math.Abs(a.Blue-b.Blue);
                count+=3;
            }
        }
        return count==0?0:(double)sum/count/255.0;
    }

    private static (double? quality,string? subsampling) ParseJpeg(string path)
    {
        using var fs=File.OpenRead(path);
        if(fs.ReadByte()!=0xFF || fs.ReadByte()!=0xD8) return (null,null);
        var quant=new List<byte>();
        string? subsampling=null;

        while(fs.Position<fs.Length)
        {
            var p=fs.ReadByte(); if(p<0)break; if(p!=0xFF)continue;
            int marker; do{marker=fs.ReadByte();}while(marker==0xFF); if(marker<0||marker==0xD9||marker==0xDA)break;
            if(marker is >=0xD0 and <=0xD7 || marker==0x01)continue;
            Span<byte> lenBytes=stackalloc byte[2]; if(fs.Read(lenBytes)!=2)break;
            var len=BinaryPrimitives.ReadUInt16BigEndian(lenBytes); if(len<2)break;
            var payload=new byte[len-2]; if(fs.Read(payload,0,payload.Length)!=payload.Length)break;
            if(marker==0xDB)
            {
                var i=0;
                while(i<payload.Length)
                {
                    var pqTq=payload[i++]; var precision=pqTq>>4; var size=precision==0?64:128;
                    if(i+size>payload.Length)break;
                    if(precision==0) quant.AddRange(payload.AsSpan(i,size).ToArray());
                    i+=size;
                }
            }
            if((marker is 0xC0 or 0xC1 or 0xC2) && payload.Length>=9)
            {
                var comps=payload[5];
                if(comps>=3 && payload.Length>=6+3*comps)
                {
                    var ySampling=payload[7];
                    var cbSampling=payload[10];
                    var crSampling=payload[13];
                    var yh=ySampling>>4; var yv=ySampling&0xF;
                    var cbh=cbSampling>>4; var cbv=cbSampling&0xF;
                    var crh=crSampling>>4; var crv=crSampling&0xF;
                    if(cbh==1&&cbv==1&&crh==1&&crv==1)
                        subsampling=(yh,yv) switch{(2,2)=>"4:2:0",(2,1)=>"4:2:2",(1,1)=>"4:4:4",_=>$"Y {yh}x{yv}; C 1x1"};
                }
            }
        }

        if(quant.Count==0)return(null,subsampling);
        var avg=quant.Average(x=>(double)x);
        var quality=Math.Clamp(100.0-(avg-1.0)*0.42,1,100);
        return(quality,subsampling);
    }

    private static SKBitmap ResizeWithin(SKBitmap source,int max)
    {
        if(source.Width<=max&&source.Height<=max)return source.Copy();
        var scale=Math.Min((double)max/source.Width,(double)max/source.Height);
        var info=new SKImageInfo(Math.Max(1,(int)(source.Width*scale)),Math.Max(1,(int)(source.Height*scale)));
        return source.Resize(info,SKSamplingOptions.Default) ?? throw new InvalidDataException("Unable to resize preview.");
    }

    private static double Luma(SKColor c)=>0.2126*c.Red+0.7152*c.Green+0.0722*c.Blue;

    private static ulong TileHash(SKBitmap b,int sx,int sy,int ex,int ey)
    {
        Span<double> cells=stackalloc double[64];
        double total=0;
        for(var cy=0;cy<8;cy++)
        for(var cx=0;cx<8;cx++)
        {
            var x=sx+(int)((cx+0.5)*(ex-sx)/8.0);
            var y=sy+(int)((cy+0.5)*(ey-sy)/8.0);
            x=Math.Clamp(x,0,b.Width-1); y=Math.Clamp(y,0,b.Height-1);
            var v=Luma(b.GetPixel(x,y)); cells[cy*8+cx]=v; total+=v;
        }
        var mean=total/64.0; ulong hash=0;
        for(var i=0;i<64;i++) if(cells[i]>=mean) hash|=1UL<<i;
        return hash;
    }
}
