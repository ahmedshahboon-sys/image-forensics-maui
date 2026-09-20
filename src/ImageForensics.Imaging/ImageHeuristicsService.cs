using System.Buffers.Binary;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using SkiaSharp;

namespace ImageForensics.Imaging;

public sealed class ImageHeuristicsService : IImageHeuristicsService
{
    private const long MaxDecodedPixels = 60_000_000;
    private const int PreviewMaxSide = 1024;

    private static readonly byte[] StandardLuminanceQuantization =
    {
        16,11,10,16,24,40,51,61,
        12,12,14,19,26,58,60,55,
        14,13,16,24,40,57,69,56,
        14,17,22,29,51,87,80,62,
        18,22,37,56,68,109,103,77,
        24,35,55,64,81,104,113,92,
        49,64,78,87,103,121,120,101,
        72,92,95,98,112,100,103,99
    };

    public Task<ImageHeuristicsResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => Task.Run(() => AnalyzeCore(filePath, cancellationToken), cancellationToken);

    private static ImageHeuristicsResult AnalyzeCore(string path, CancellationToken ct)
    {
        using var preview =
            BoundedImageDecoder.DecodePreview(
                path,
                PreviewMaxSide,
                MaxDecodedPixels,
                ct);

        var pixel = AnalyzePixels(preview, ct);
        var ela = AnalyzeEla(preview, ct);
        var jpeg = ParseJpeg(path);

        var indicators = new List<EvidenceItem>();

        if (pixel.BlockBoundaryRatio > 1.45)
        {
            indicators.Add(new EvidenceItem(
                "jpeg.block_boundaries",
                "حدود 8×8 أقوى من الانتقالات الداخلية",
                $"Boundary/interior ratio={pixel.BlockBoundaryRatio:F3}",
                ForensicConfidence.Possible,
                "Luminance edge energy is elevated on JPEG-style 8-pixel boundaries.",
                "ضغط JPEG العادي، الحواف الطبيعية، sharpening وإعادة التحجيم قد تنتج النمط نفسه."));
        }

        if (jpeg.IsJpeg &&
            pixel.BlockBoundaryRatio > 1.35 &&
            pixel.GridPhaseDominance > 1.30)
        {
            indicators.Add(new EvidenceItem(
                "jpeg.double_compression_grid",
                "نمط شبكي متوافق مع احتمال إعادة ضغط JPEG",
                $"Block ratio={pixel.BlockBoundaryRatio:F3}; phase dominance={pixel.GridPhaseDominance:F3}.",
                ForensicConfidence.Possible,
                "Two independent grid metrics show elevated 8-pixel periodic structure in a JPEG file.",
                "هذا heuristic فقط؛ JPEG أحادي الضغط مع محتوى شبكي أو sharpening قد يعطي النتيجة نفسها. لا يثبت double-JPEG."));
        }

        if (pixel.NoiseCv > 0.55)
        {
            indicators.Add(new EvidenceItem(
                "noise.inconsistency",
                "طاقة الضوضاء/الترددات العالية تختلف بين مناطق الصورة",
                $"Regional coefficient of variation={pixel.NoiseCv:F3}",
                ForensicConfidence.Possible,
                "High-pass residual energy was compared across a 4×4 regional grid.",
                "الإضاءة، العمق، denoising، texture واختلاف أجزاء المشهد قد تسبب تفاوتًا طبيعيًا."));
        }

        if (ela.Mean > 0.10)
        {
            indicators.Add(new EvidenceItem(
                "ela.elevated",
                "متوسط ELA المساعد مرتفع",
                $"Normalized mean difference={ela.Mean:F4}",
                ForensicConfidence.Possible,
                "A normalized preview was re-encoded at JPEG quality 90 and compared.",
                "ELA ليس إثباتًا للتعديل ويتأثر بشدة بالضغط السابق والمحتوى ونوع الصورة."));
        }

        if (ela.RegionalCv > 0.80 && ela.Mean > 0.015)
        {
            indicators.Add(new EvidenceItem(
                "ela.regional_variation",
                "اختلاف واضح في ELA بين مناطق الصورة",
                $"ELA regional CV={ela.RegionalCv:F3}; mean={ela.Mean:F4}.",
                ForensicConfidence.Possible,
                "Recompression difference energy varies substantially across a 4×4 grid.",
                "الحواف، النصوص، السماء، blur ومناطق التفاصيل المختلفة قد تنتج اختلافات ELA مشروعة."));
        }

        if (pixel.ResamplingPeriodicity > 0.48)
        {
            indicators.Add(new EvidenceItem(
                "resampling.periodicity",
                "دورية مكانية قد تتوافق مع إعادة تحجيم/استيفاء",
                $"Periodicity score={pixel.ResamplingPeriodicity:F3}.",
                ForensicConfidence.Possible,
                "Second-difference energy shows phase imbalance for one or more small spatial periods.",
                "الأنماط المتكررة، الشبكات، الأقمشة، النصوص والعمارة قد تعطي دورية مشابهة؛ لا يثبت resampling."));
        }

        if (ela.RegionalCv > 1.00 &&
            pixel.NoiseCv > 0.65)
        {
            indicators.Add(new EvidenceItem(
                "compression.regional_inconsistency",
                "عدة مقاييس تشير لاختلاف خصائص مناطق الصورة",
                $"ELA CV={ela.RegionalCv:F3}; noise CV={pixel.NoiseCv:F3}.",
                ForensicConfidence.Possible,
                "Both recompression residuals and high-frequency residual energy vary strongly by region.",
                "هذا ليس كشف splice قطعيًا؛ اختلاف الإضاءة والملمس والعمق قد يرفع المقياسين معًا."));
        }

        if (pixel.ClonePairs > 0)
        {
            indicators.Add(new EvidenceItem(
                "copy_move.tile_candidates",
                "مناطق غير متجاورة متشابهة بصريًا",
                $"{pixel.ClonePairs} candidate tile pair(s).",
                ForensicConfidence.Possible,
                "Coarse luminance hashes of non-adjacent 8×8-grid regions are very similar.",
                "السماء والجدران والماء والأنماط المتكررة تعطي false positives؛ يلزم فحص بصري وتحليل أدق."));
        }

        if (pixel.HistogramPeakiness > 18)
        {
            indicators.Add(new EvidenceItem(
                "histogram.peakiness",
                "Histogram يحتوي قممًا بارزة",
                $"Peakiness={pixel.HistogramPeakiness:F2}.",
                ForensicConfidence.Unknown,
                "The strongest luminance histogram bin is much denser than a uniform distribution.",
                "الصور الطبيعية منخفضة التباين أو الرسومات أو screenshots قد تظهر قممًا قوية؛ هذا مقياس وصفي وليس علامة تلاعب."));
        }

        return new ImageHeuristicsResult(
            jpeg.Quality,
            jpeg.Subsampling,
            jpeg.QuantizationTableCount,
            jpeg.QuantizationMean,
            pixel.BlockBoundaryRatio,
            pixel.GridPhaseDominance,
            pixel.ResamplingPeriodicity,
            pixel.NoiseCv,
            ela.Mean,
            ela.RegionalCv,
            pixel.EdgeDensity,
            pixel.HistogramPeakiness,
            pixel.ClonePairs,
            indicators);
    }

    private static PixelMetrics AnalyzePixels(SKBitmap bitmap, CancellationToken ct)
    {
        var w = bitmap.Width;
        var h = bitmap.Height;

        double boundary = 0;
        double interior = 0;
        long boundaryCount = 0;
        long interiorCount = 0;

        var phaseEnergy = new double[8];
        var phaseCount = new long[8];

        var noiseSums = new double[16];
        var noiseCounts = new long[16];

        var histogram = new long[256];
        long edgeCount = 0;
        long edgeSamples = 0;

        for (var y = 1; y < h - 1; y++)
        {
            if ((y & 31) == 0)
                ct.ThrowIfCancellationRequested();

            for (var x = 1; x < w - 1; x++)
            {
                var center = Luma(bitmap.GetPixel(x, y));
                var left = Luma(bitmap.GetPixel(x - 1, y));
                var right = Luma(bitmap.GetPixel(x + 1, y));
                var up = Luma(bitmap.GetPixel(x, y - 1));
                var down = Luma(bitmap.GetPixel(x, y + 1));

                histogram[Math.Clamp((int)Math.Round(center), 0, 255)]++;

                var dx = Math.Abs(center - left);
                var dy = Math.Abs(center - up);

                if (x % 8 == 0)
                {
                    boundary += dx;
                    boundaryCount++;
                }
                else
                {
                    interior += dx;
                    interiorCount++;
                }

                if (y % 8 == 0)
                {
                    boundary += dy;
                    boundaryCount++;
                }
                else
                {
                    interior += dy;
                    interiorCount++;
                }

                var phase = x & 7;
                phaseEnergy[phase] += dx;
                phaseCount[phase]++;

                if (dx + dy > 48)
                    edgeCount++;
                edgeSamples++;

                var highPass = Math.Abs(center - (left + right + up + down) / 4.0);
                var gx = Math.Min(3, x * 4 / Math.Max(1, w));
                var gy = Math.Min(3, y * 4 / Math.Max(1, h));
                var region = gy * 4 + gx;
                noiseSums[region] += highPass;
                noiseCounts[region]++;
            }
        }

        var boundaryMean = boundaryCount == 0 ? 0 : boundary / boundaryCount;
        var interiorMean = interiorCount == 0 ? 0 : interior / interiorCount;
        var blockRatio = interiorMean <= 0.0001 ? 1.0 : boundaryMean / interiorMean;

        var phaseMeans = phaseEnergy
            .Select((value, i) => phaseCount[i] == 0 ? 0 : value / phaseCount[i])
            .ToArray();
        var phaseAverage = phaseMeans.Average();
        var gridDominance = phaseAverage <= 0.0001 ? 1.0 : phaseMeans.Max() / phaseAverage;

        var regionalNoise = noiseSums
            .Select((value, i) => noiseCounts[i] == 0 ? 0 : value / noiseCounts[i])
            .ToArray();
        var noiseCv = CoefficientOfVariation(regionalNoise);

        var totalHistogram = histogram.Sum();
        var expected = totalHistogram <= 0 ? 1.0 : totalHistogram / 256.0;
        var histogramPeakiness = expected <= 0 ? 0 : histogram.Max() / expected;

        var edgeDensity = edgeSamples == 0 ? 0 : (double)edgeCount / edgeSamples;
        var periodicity = CalculateResamplingPeriodicity(bitmap, ct);
        var clonePairs = CountCloneCandidates(bitmap, ct);

        return new PixelMetrics(
            blockRatio,
            gridDominance,
            periodicity,
            noiseCv,
            edgeDensity,
            histogramPeakiness,
            clonePairs);
    }

    private static double CalculateResamplingPeriodicity(SKBitmap bitmap, CancellationToken ct)
    {
        var scores = new List<double>();

        for (var period = 2; period <= 8; period++)
        {
            var sums = new double[period];
            var counts = new long[period];

            var yStep = Math.Max(1, bitmap.Height / 256);

            for (var y = 1; y < bitmap.Height - 1; y += yStep)
            {
                if ((y & 31) == 0)
                    ct.ThrowIfCancellationRequested();

                for (var x = 1; x < bitmap.Width - 1; x += 2)
                {
                    var a = Luma(bitmap.GetPixel(x - 1, y));
                    var b = Luma(bitmap.GetPixel(x, y));
                    var c = Luma(bitmap.GetPixel(x + 1, y));
                    var secondDifference = Math.Abs(a - 2 * b + c);

                    var phase = x % period;
                    sums[phase] += secondDifference;
                    counts[phase]++;
                }
            }

            var means = sums
                .Select((v, i) => counts[i] == 0 ? 0 : v / counts[i])
                .ToArray();

            scores.Add(CoefficientOfVariation(means));
        }

        return scores.Count == 0 ? 0 : scores.Max();
    }

    private static int CountCloneCandidates(SKBitmap bitmap, CancellationToken ct)
    {
        const int grid = 8;
        var tiles = new List<TileFeature>(grid * grid);

        for (var gy = 0; gy < grid; gy++)
        {
            for (var gx = 0; gx < grid; gx++)
            {
                ct.ThrowIfCancellationRequested();

                var sx = gx * bitmap.Width / grid;
                var sy = gy * bitmap.Height / grid;
                var ex = Math.Max(sx + 1, (gx + 1) * bitmap.Width / grid);
                var ey = Math.Max(sy + 1, (gy + 1) * bitmap.Height / grid);

                var feature = TileHashAndVariance(bitmap, sx, sy, ex, ey);
                if (feature.Variance >= 18)
                    tiles.Add(new TileFeature(gx, gy, feature.Hash, feature.Variance));
            }
        }

        var pairs = 0;

        for (var i = 0; i < tiles.Count; i++)
        {
            for (var j = i + 1; j < tiles.Count; j++)
            {
                var a = tiles[i];
                var b = tiles[j];

                if (Math.Abs(a.X - b.X) <= 1 &&
                    Math.Abs(a.Y - b.Y) <= 1)
                    continue;

                var distance = System.Numerics.BitOperations.PopCount(a.Hash ^ b.Hash);
                var varianceRatio = Math.Min(a.Variance, b.Variance) /
                                    Math.Max(0.0001, Math.Max(a.Variance, b.Variance));

                if (distance <= 2 && varianceRatio > 0.70)
                    pairs++;
            }
        }

        return pairs;
    }

    private static ElaMetrics AnalyzeEla(SKBitmap preview, CancellationToken ct)
    {
        using var image = SKImage.FromBitmap(preview);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        using var decoded = SKBitmap.Decode(encoded)
            ?? throw new InvalidDataException("ELA re-decode failed.");

        var regionSums = new double[16];
        var regionCounts = new long[16];
        double total = 0;
        long count = 0;

        var step = Math.Max(1, preview.Width / 512);

        for (var y = 0; y < preview.Height; y += step)
        {
            ct.ThrowIfCancellationRequested();

            for (var x = 0; x < preview.Width; x += step)
            {
                var a = preview.GetPixel(x, y);
                var b = decoded.GetPixel(x, y);

                var diff =
                    (Math.Abs(a.Red - b.Red) +
                     Math.Abs(a.Green - b.Green) +
                     Math.Abs(a.Blue - b.Blue)) / (3.0 * 255.0);

                total += diff;
                count++;

                var gx = Math.Min(3, x * 4 / Math.Max(1, preview.Width));
                var gy = Math.Min(3, y * 4 / Math.Max(1, preview.Height));
                var region = gy * 4 + gx;

                regionSums[region] += diff;
                regionCounts[region]++;
            }
        }

        var means = regionSums
            .Select((v, i) => regionCounts[i] == 0 ? 0 : v / regionCounts[i])
            .ToArray();

        return new ElaMetrics(
            count == 0 ? 0 : total / count,
            CoefficientOfVariation(means));
    }

    private static JpegMetrics ParseJpeg(string path)
    {
        using var fs = File.OpenRead(path);

        if (fs.ReadByte() != 0xFF || fs.ReadByte() != 0xD8)
            return new JpegMetrics(false, null, null, 0, null);

        var quantizationTables = new List<byte[]>();
        string? subsampling = null;
        var lenBytes = new byte[2];

        while (fs.Position < fs.Length)
        {
            var prefix = fs.ReadByte();
            if (prefix < 0) break;
            if (prefix != 0xFF) continue;

            int marker;
            do
            {
                marker = fs.ReadByte();
            }
            while (marker == 0xFF);

            if (marker < 0 || marker == 0xD9 || marker == 0xDA)
                break;

            if (marker is >= 0xD0 and <= 0xD7 || marker == 0x01)
                continue;

            if (fs.Read(lenBytes, 0, lenBytes.Length) != lenBytes.Length)
                break;

            var length = BinaryPrimitives.ReadUInt16BigEndian(lenBytes);
            if (length < 2 || fs.Position + length - 2 > fs.Length)
                break;

            var payload = new byte[length - 2];
            if (fs.Read(payload, 0, payload.Length) != payload.Length)
                break;

            if (marker == 0xDB)
                ParseQuantizationTables(payload, quantizationTables);

            if ((marker is 0xC0 or 0xC1 or 0xC2) &&
                payload.Length >= 15)
                subsampling = ParseSubsampling(payload);
        }

        var flat = quantizationTables.SelectMany(x => x).ToArray();
        double? mean = flat.Length == 0 ? null : flat.Average(v => (double)v);
        double? quality = quantizationTables.Count == 0
            ? null
            : EstimateJpegQuality(quantizationTables[0]);

        return new JpegMetrics(
            true,
            quality,
            subsampling,
            quantizationTables.Count,
            mean);
    }

    private static void ParseQuantizationTables(
        byte[] payload,
        List<byte[]> destination)
    {
        var index = 0;

        while (index < payload.Length)
        {
            var pqTq = payload[index++];
            var precision = pqTq >> 4;
            var size = precision == 0 ? 64 : 128;

            if (index + size > payload.Length)
                break;

            if (precision == 0)
            {
                var table = new byte[64];
                Buffer.BlockCopy(payload, index, table, 0, 64);
                destination.Add(table);
            }

            index += size;
        }
    }

    private static string? ParseSubsampling(byte[] payload)
    {
        if (payload.Length < 15) return null;

        var components = payload[5];
        if (components < 3 || payload.Length < 6 + 3 * components)
            return null;

        var ySampling = payload[7];
        var cbSampling = payload[10];
        var crSampling = payload[13];

        var yh = ySampling >> 4;
        var yv = ySampling & 0xF;
        var cbh = cbSampling >> 4;
        var cbv = cbSampling & 0xF;
        var crh = crSampling >> 4;
        var crv = crSampling & 0xF;

        if (cbh != 1 || cbv != 1 || crh != 1 || crv != 1)
            return $"Y {yh}x{yv}; Cb {cbh}x{cbv}; Cr {crh}x{crv}";

        return (yh, yv) switch
        {
            (2, 2) => "4:2:0",
            (2, 1) => "4:2:2",
            (1, 1) => "4:4:4",
            _ => $"Y {yh}x{yv}; C 1x1"
        };
    }

    private static double EstimateJpegQuality(byte[] table)
    {
        var ratios = new List<double>(64);

        for (var i = 0; i < Math.Min(64, table.Length); i++)
        {
            var standard = StandardLuminanceQuantization[i];
            if (standard == 0) continue;

            ratios.Add(table[i] * 100.0 / standard);
        }

        if (ratios.Count == 0)
            return 0;

        ratios.Sort();
        var scale = ratios[ratios.Count / 2];

        var quality = scale <= 100
            ? (200 - scale) / 2.0
            : 5000.0 / Math.Max(scale, 1);

        return Math.Clamp(quality, 1, 100);
    }

    private static double CoefficientOfVariation(IReadOnlyList<double> values)
    {
        var usable = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToArray();
        if (usable.Length == 0) return 0;

        var mean = usable.Average();
        if (Math.Abs(mean) <= 0.000001) return 0;

        var variance = usable.Select(v => (v - mean) * (v - mean)).Average();
        return Math.Sqrt(variance) / Math.Abs(mean);
    }

    private static SKBitmap ResizeWithin(SKBitmap source, int max)
    {
        if (source.Width <= max && source.Height <= max)
            return source.Copy();

        var scale = Math.Min(
            (double)max / source.Width,
            (double)max / source.Height);

        var info = new SKImageInfo(
            Math.Max(1, (int)(source.Width * scale)),
            Math.Max(1, (int)(source.Height * scale)));

        return source.Resize(info, SKSamplingOptions.Default)
               ?? throw new InvalidDataException("Unable to resize preview.");
    }

    private static double Luma(SKColor c)
        => 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue;

    private static (ulong Hash, double Variance) TileHashAndVariance(
        SKBitmap bitmap,
        int sx,
        int sy,
        int ex,
        int ey)
    {
        Span<double> cells = stackalloc double[64];
        double total = 0;

        for (var cy = 0; cy < 8; cy++)
        {
            for (var cx = 0; cx < 8; cx++)
            {
                var x = sx + (int)((cx + 0.5) * (ex - sx) / 8.0);
                var y = sy + (int)((cy + 0.5) * (ey - sy) / 8.0);

                x = Math.Clamp(x, 0, bitmap.Width - 1);
                y = Math.Clamp(y, 0, bitmap.Height - 1);

                var value = Luma(bitmap.GetPixel(x, y));
                cells[cy * 8 + cx] = value;
                total += value;
            }
        }

        var mean = total / 64.0;
        double variance = 0;
        ulong hash = 0;

        for (var i = 0; i < 64; i++)
        {
            var delta = cells[i] - mean;
            variance += delta * delta;

            if (cells[i] >= mean)
                hash |= 1UL << i;
        }

        return (hash, variance / 64.0);
    }

    private sealed record PixelMetrics(
        double BlockBoundaryRatio,
        double GridPhaseDominance,
        double ResamplingPeriodicity,
        double NoiseCv,
        double EdgeDensity,
        double HistogramPeakiness,
        int ClonePairs);

    private sealed record ElaMetrics(
        double Mean,
        double RegionalCv);

    private sealed record JpegMetrics(
        bool IsJpeg,
        double? Quality,
        string? Subsampling,
        int QuantizationTableCount,
        double? QuantizationMean);

    private sealed record TileFeature(
        int X,
        int Y,
        ulong Hash,
        double Variance);
}
