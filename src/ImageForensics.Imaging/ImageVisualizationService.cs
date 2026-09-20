using ImageForensics.Core.Abstractions;
using SkiaSharp;

namespace ImageForensics.Imaging;

public sealed class ImageVisualizationService : IImageVisualizationService
{
    public Task CreateElaPreviewAsync(
        string inputPath,
        string outputPngPath,
        CancellationToken ct = default)
        => Task.Run(
            () => TransformEla(
                inputPath,
                outputPngPath,
                ct),
            ct);

    public Task CreateBitPlaneAsync(
        string inputPath,
        int bitPlane,
        string outputPngPath,
        CancellationToken ct = default)
        => Task.Run(
            () => TransformBitPlane(
                inputPath,
                bitPlane,
                outputPngPath,
                ct),
            ct);

    public Task CreateRgbChannelAsync(
        string inputPath,
        string channel,
        string outputPngPath,
        CancellationToken ct = default)
        => Task.Run(
            () => TransformChannel(
                inputPath,
                channel,
                outputPngPath,
                ct),
            ct);

    public Task CreateEntropyMapAsync(
        string inputPath,
        string outputPngPath,
        CancellationToken ct = default)
        => Task.Run(
            () => TransformEntropyMap(
                inputPath,
                outputPngPath,
                ct),
            ct);

    public Task CreateHistogramAsync(
        string inputPath,
        string outputPngPath,
        CancellationToken ct = default)
        => Task.Run(
            () => TransformHistogram(
                inputPath,
                outputPngPath,
                ct),
            ct);

    public Task CreateDifferenceMapAsync(
        string leftPath,
        string rightPath,
        string outputPngPath,
        CancellationToken ct = default)
        => Task.Run(
            () => TransformDifference(
                leftPath,
                rightPath,
                outputPngPath,
                ct),
            ct);

    public Task CreateComparisonOverlayAsync(
        string leftPath,
        string rightPath,
        string outputPngPath,
        CancellationToken ct = default)
        => Task.Run(
            () => TransformComparisonOverlay(
                leftPath,
                rightPath,
                outputPngPath,
                ct),
            ct);

    public Task CreateComparisonHeatmapAsync(
        string leftPath,
        string rightPath,
        string outputPngPath,
        CancellationToken ct = default)
        => Task.Run(
            () => TransformComparisonHeatmap(
                leftPath,
                rightPath,
                outputPngPath,
                ct),
            ct);

    public Task CreateComparisonContactSheetAsync(
        string leftPath,
        string rightPath,
        string outputPngPath,
        CancellationToken ct = default)
        => Task.Run(
            () => TransformComparisonContactSheet(
                leftPath,
                rightPath,
                outputPngPath,
                ct),
            ct);

    private static void TransformEla(
        string input,
        string output,
        CancellationToken ct)
    {
        using var src = LoadPreview(
            input,
            1024);
        using var img = SKImage.FromBitmap(src);
        using var jpg =
            img.Encode(
                SKEncodedImageFormat.Jpeg,
                90);
        using var rec = SKBitmap.Decode(jpg)
            ?? throw new InvalidDataException(
                "ELA decode failed.");
        using var dest = new SKBitmap(
            src.Width,
            src.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque);

        for (var y = 0;
             y < src.Height;
             y++)
        {
            ct.ThrowIfCancellationRequested();

            for (var x = 0;
                 x < src.Width;
                 x++)
            {
                var a = src.GetPixel(x, y);
                var b = rec.GetPixel(x, y);

                var r = (byte)Math.Min(
                    255,
                    Math.Abs(a.Red - b.Red) * 8);
                var g = (byte)Math.Min(
                    255,
                    Math.Abs(a.Green - b.Green) * 8);
                var bl = (byte)Math.Min(
                    255,
                    Math.Abs(a.Blue - b.Blue) * 8);

                dest.SetPixel(
                    x,
                    y,
                    new SKColor(
                        r,
                        g,
                        bl));
            }
        }

        SavePng(
            dest,
            output);
    }

    private static void TransformBitPlane(
        string input,
        int plane,
        string output,
        CancellationToken ct)
    {
        if (plane is < 0 or > 7)
            throw new ArgumentOutOfRangeException(
                nameof(plane));

        using var src = LoadPreview(
            input,
            1024);
        using var dest = new SKBitmap(
            src.Width,
            src.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque);

        for (var y = 0;
             y < src.Height;
             y++)
        {
            ct.ThrowIfCancellationRequested();

            for (var x = 0;
                 x < src.Width;
                 x++)
            {
                var p = src.GetPixel(x, y);

                var on =
                    (((p.Red >> plane) & 1) +
                     ((p.Green >> plane) & 1) +
                     ((p.Blue >> plane) & 1)) >= 2;

                var v =
                    (byte)(on ? 255 : 0);

                dest.SetPixel(
                    x,
                    y,
                    new SKColor(v, v, v));
            }
        }

        SavePng(
            dest,
            output);
    }

    private static void TransformChannel(
        string input,
        string channel,
        string output,
        CancellationToken ct)
    {
        var c =
            channel
                .Trim()
                .ToUpperInvariant();

        if (c is not ("R" or "G" or "B"))
            throw new ArgumentException(
                "Channel must be R, G or B.",
                nameof(channel));

        using var src = LoadPreview(
            input,
            1024);
        using var dest = new SKBitmap(
            src.Width,
            src.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque);

        for (var y = 0;
             y < src.Height;
             y++)
        {
            ct.ThrowIfCancellationRequested();

            for (var x = 0;
                 x < src.Width;
                 x++)
            {
                var p = src.GetPixel(x, y);

                var v = c == "R"
                    ? p.Red
                    : c == "G"
                        ? p.Green
                        : p.Blue;

                dest.SetPixel(
                    x,
                    y,
                    new SKColor(v, v, v));
            }
        }

        SavePng(
            dest,
            output);
    }

    private static void TransformEntropyMap(
        string input,
        string output,
        CancellationToken ct)
    {
        using var src = LoadPreview(
            input,
            1024);
        using var dest = new SKBitmap(
            src.Width,
            src.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque);

        const int block = 16;
        var histogram = new int[256];

        for (var by = 0;
             by < src.Height;
             by += block)
        {
            ct.ThrowIfCancellationRequested();

            for (var bx = 0;
                 bx < src.Width;
                 bx += block)
            {
                Array.Clear(
                    histogram,
                    0,
                    histogram.Length);

                var endX =
                    Math.Min(
                        src.Width,
                        bx + block);
                var endY =
                    Math.Min(
                        src.Height,
                        by + block);

                var count = 0;

                for (var y = by;
                     y < endY;
                     y++)
                {
                    for (var x = bx;
                         x < endX;
                         x++)
                    {
                        var p = src.GetPixel(x, y);

                        var luma =
                            Math.Clamp(
                                (int)Math.Round(
                                    0.2126 * p.Red +
                                    0.7152 * p.Green +
                                    0.0722 * p.Blue),
                                0,
                                255);

                        histogram[luma]++;
                        count++;
                    }
                }

                var entropy =
                    CalculateEntropy(
                        histogram,
                        count);

                var value =
                    (byte)Math.Clamp(
                        (int)Math.Round(
                            entropy / 8.0 * 255.0),
                        0,
                        255);

                for (var y = by;
                     y < endY;
                     y++)
                {
                    for (var x = bx;
                         x < endX;
                         x++)
                    {
                        dest.SetPixel(
                            x,
                            y,
                            new SKColor(
                                value,
                                value,
                                value));
                    }
                }
            }
        }

        SavePng(
            dest,
            output);
    }

    private static double CalculateEntropy(
        ReadOnlySpan<int> histogram,
        int count)
    {
        if (count <= 0)
            return 0;

        double entropy = 0;

        for (var i = 0;
             i < histogram.Length;
             i++)
        {
            if (histogram[i] == 0)
                continue;

            var p =
                (double)histogram[i] /
                count;

            entropy -=
                p *
                Math.Log2(p);
        }

        return entropy;
    }

    private static void TransformHistogram(
        string input,
        string output,
        CancellationToken ct)
    {
        using var src = LoadPreview(input, 1024);

        var redValues = new int[256];
        var greenValues = new int[256];
        var blueValues = new int[256];

        for (var y = 0; y < src.Height; y++)
        {
            ct.ThrowIfCancellationRequested();

            for (var x = 0; x < src.Width; x++)
            {
                var pixel = src.GetPixel(x, y);
                redValues[pixel.Red]++;
                greenValues[pixel.Green]++;
                blueValues[pixel.Blue]++;
            }
        }

        var max =
            Math.Max(
                1,
                Math.Max(
                    redValues.Max(),
                    Math.Max(
                        greenValues.Max(),
                        blueValues.Max())));

        const int width = 768;
        const int height = 420;
        const int margin = 28;

        using var dest = new SKBitmap(
            width,
            height,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque);

        using var canvas = new SKCanvas(dest);
        canvas.Clear(SKColors.Black);

        using var redPaint = new SKPaint
        {
            Color = SKColors.Red,
            IsAntialias = true,
            StrokeWidth = 2
        };
        using var greenPaint = new SKPaint
        {
            Color = SKColors.Lime,
            IsAntialias = true,
            StrokeWidth = 2
        };
        using var bluePaint = new SKPaint
        {
            Color = SKColors.DodgerBlue,
            IsAntialias = true,
            StrokeWidth = 2
        };

        DrawHistogramLine(canvas, redValues, max, redPaint, width, height, margin);
        DrawHistogramLine(canvas, greenValues, max, greenPaint, width, height, margin);
        DrawHistogramLine(canvas, blueValues, max, bluePaint, width, height, margin);

        SavePng(dest, output);
    }

    private static void DrawHistogramLine(
        SKCanvas canvas,
        IReadOnlyList<int> values,
        int max,
        SKPaint paint,
        int width,
        int height,
        int margin)
    {
        using var path = new SKPath();

        for (var i = 0; i < 256; i++)
        {
            var x =
                margin +
                i / 255f *
                (width - margin * 2);

            var y =
                height -
                margin -
                values[i] /
                (float)max *
                (height - margin * 2);

            if (i == 0)
                path.MoveTo(x, y);
            else
                path.LineTo(x, y);
        }

        canvas.DrawPath(path, paint);
    }

    private static void TransformDifference(
        string left,
        string right,
        string output,
        CancellationToken ct)
    {
        using var a = LoadFixed(
            left,
            512,
            512);
        using var b = LoadFixed(
            right,
            512,
            512);
        using var dest = new SKBitmap(
            512,
            512,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque);

        for (var y = 0;
             y < 512;
             y++)
        {
            ct.ThrowIfCancellationRequested();

            for (var x = 0;
                 x < 512;
                 x++)
            {
                var p = a.GetPixel(x, y);
                var q = b.GetPixel(x, y);

                var d =
                    (byte)Math.Min(
                        255,
                        (Math.Abs(p.Red - q.Red) +
                         Math.Abs(p.Green - q.Green) +
                         Math.Abs(p.Blue - q.Blue)) /
                        3 *
                        3);

                dest.SetPixel(
                    x,
                    y,
                    new SKColor(d, d, d));
            }
        }

        SavePng(
            dest,
            output);
    }

    private static void TransformComparisonOverlay(
        string left,
        string right,
        string output,
        CancellationToken ct)
    {
        using var a = LoadFixed(left, 768, 768);
        using var b = LoadFixed(right, 768, 768);
        using var dest = new SKBitmap(
            768,
            768,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque);

        for (var y = 0; y < 768; y++)
        {
            ct.ThrowIfCancellationRequested();

            for (var x = 0; x < 768; x++)
            {
                var p = a.GetPixel(x, y);
                var q = b.GetPixel(x, y);

                dest.SetPixel(
                    x,
                    y,
                    new SKColor(
                        (byte)((p.Red + q.Red) / 2),
                        (byte)((p.Green + q.Green) / 2),
                        (byte)((p.Blue + q.Blue) / 2)));
            }
        }

        SavePng(dest, output);
    }

    private static void TransformComparisonHeatmap(
        string left,
        string right,
        string output,
        CancellationToken ct)
    {
        using var a = LoadFixed(left, 768, 768);
        using var b = LoadFixed(right, 768, 768);
        using var dest = new SKBitmap(
            768,
            768,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque);

        for (var y = 0; y < 768; y++)
        {
            ct.ThrowIfCancellationRequested();

            for (var x = 0; x < 768; x++)
            {
                var p = a.GetPixel(x, y);
                var q = b.GetPixel(x, y);

                var mean =
                    (Math.Abs(p.Red - q.Red) +
                     Math.Abs(p.Green - q.Green) +
                     Math.Abs(p.Blue - q.Blue)) /
                    3.0;

                var intensity =
                    (byte)Math.Clamp(
                        (int)Math.Round(mean * 3.0),
                        0,
                        255);

                var green =
                    (byte)Math.Clamp(
                        intensity * 2,
                        0,
                        255);

                dest.SetPixel(
                    x,
                    y,
                    new SKColor(
                        intensity,
                        green,
                        0));
            }
        }

        SavePng(dest, output);
    }

    private static void TransformComparisonContactSheet(
        string left,
        string right,
        string output,
        CancellationToken ct)
    {
        using var a = LoadPreview(left, 720);
        using var b = LoadPreview(right, 720);

        const int gap = 16;
        const int panelWidth = 720;
        const int panelHeight = 720;

        using var dest = new SKBitmap(
            panelWidth * 2 + gap,
            panelHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque);

        using var canvas = new SKCanvas(dest);
        canvas.Clear(SKColors.Black);

        DrawFitted(
            canvas,
            a,
            new SKRect(
                0,
                0,
                panelWidth,
                panelHeight));

        DrawFitted(
            canvas,
            b,
            new SKRect(
                panelWidth + gap,
                0,
                panelWidth * 2 + gap,
                panelHeight));

        ct.ThrowIfCancellationRequested();

        SavePng(dest, output);
    }

    private static void DrawFitted(
        SKCanvas canvas,
        SKBitmap bitmap,
        SKRect bounds)
    {
        var scale =
            Math.Min(
                bounds.Width / bitmap.Width,
                bounds.Height / bitmap.Height);

        var width =
            bitmap.Width * scale;

        var height =
            bitmap.Height * scale;

        var left =
            bounds.Left +
            (bounds.Width - width) / 2;

        var top =
            bounds.Top +
            (bounds.Height - height) / 2;

        canvas.DrawBitmap(
            bitmap,
            new SKRect(
                0,
                0,
                bitmap.Width,
                bitmap.Height),
            new SKRect(
                left,
                top,
                left + width,
                top + height),
            SKSamplingOptions.Default);
    }

    private static SKBitmap LoadPreview(
        string path,
        int max)
    {
        using var src = SKBitmap.Decode(path)
            ?? throw new InvalidDataException(
                "Unable to decode image.");

        if (src.Width <= max &&
            src.Height <= max)
            return src.Copy();

        var scale =
            Math.Min(
                (double)max / src.Width,
                (double)max / src.Height);

        return src.Resize(
                   new SKImageInfo(
                       Math.Max(
                           1,
                           (int)(src.Width * scale)),
                       Math.Max(
                           1,
                           (int)(src.Height * scale))),
                   SKSamplingOptions.Default)
               ?? throw new InvalidDataException(
                   "Unable to resize image.");
    }

    private static SKBitmap LoadFixed(
        string path,
        int w,
        int h)
    {
        using var src = SKBitmap.Decode(path)
            ?? throw new InvalidDataException(
                "Unable to decode image.");

        return src.Resize(
                   new SKImageInfo(w, h),
                   SKSamplingOptions.Default)
               ?? throw new InvalidDataException(
                   "Unable to normalize image.");
    }

    private static void SavePng(
        SKBitmap bitmap,
        string output)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(output) ??
            ".");

        using var image =
            SKImage.FromBitmap(bitmap);
        using var data =
            image.Encode(
                SKEncodedImageFormat.Png,
                100)
            ?? throw new InvalidOperationException(
                "PNG encode failed.");

        using var fs =
            File.Create(output);

        data.SaveTo(fs);
    }
}
