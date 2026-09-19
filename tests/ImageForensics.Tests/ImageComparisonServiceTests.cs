using ImageForensics.Forensics.Comparison;
using ImageForensics.Forensics.FileIdentity;
using ImageForensics.Imaging;
using ImageForensics.Metadata;
using SkiaSharp;
using Xunit;

namespace ImageForensics.Tests;

public sealed class ImageComparisonServiceTests
{
    [Fact]
    public async Task IdenticalFilesProduceExactAndPerfectComparison()
    {
        var root =
            CreateRoot();

        var left =
            Path.Combine(root, "left.png");

        var right =
            Path.Combine(root, "right.png");

        try
        {
            await CreatePatternAsync(
                left,
                120,
                90);

            File.Copy(
                left,
                right);

            var result =
                await CreateService()
                    .CompareAsync(
                        left,
                        right);

            Assert.True(result.ExactMatch);
            Assert.Equal(
                1,
                result.AHashSimilarity,
                6);

            Assert.Equal(
                1,
                result.DHashSimilarity,
                6);

            Assert.Equal(
                1,
                result.PHashSimilarity,
                6);

            Assert.Equal(
                1,
                result.PixelMetrics.NormalizedRgbSimilarity,
                6);

            Assert.Equal(
                "Same dimensions",
                result.DimensionRelation);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task UniformResizeIsReportedAsCandidateNotProof()
    {
        var root =
            CreateRoot();

        var left =
            Path.Combine(root, "left.png");

        var right =
            Path.Combine(root, "right.png");

        try
        {
            await CreatePatternAsync(
                left,
                160,
                120);

            using var source =
                SKBitmap.Decode(left)
                ?? throw new InvalidOperationException();

            using var resized =
                source.Resize(
                    new SKImageInfo(
                        80,
                        60),
                    SKSamplingOptions.Default)
                ?? throw new InvalidOperationException();

            await SavePngAsync(
                resized,
                right);

            var result =
                await CreateService()
                    .CompareAsync(
                        left,
                        right);

            Assert.False(
                result.ExactMatch);

            Assert.True(
                result.UniformResizeCandidate);

            var indicator =
                Assert.Single(
                    result.Indicators,
                    x =>
                        x.Code ==
                        "compare.uniform_resize_candidate");

            Assert.Equal(
                ImageForensics.Core.Models.ForensicConfidence.Possible,
                indicator.Confidence);

            Assert.Contains(
                "does not prove",
                indicator.Limitation,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task PixelMetricsAreBoundedForDifferentImages()
    {
        var root =
            CreateRoot();

        var left =
            Path.Combine(root, "left.png");

        var right =
            Path.Combine(root, "right.png");

        try
        {
            await CreateSolidAsync(
                left,
                64,
                64,
                new SKColor(
                    10,
                    20,
                    30));

            await CreateSolidAsync(
                right,
                64,
                64,
                new SKColor(
                    220,
                    210,
                    200));

            var metrics =
                await new ImagePairPixelAnalyzer()
                    .CompareAsync(
                        left,
                        right);

            Assert.InRange(
                metrics.NormalizedRgbSimilarity,
                0,
                1);

            Assert.InRange(
                metrics.CenterCropSimilarity,
                0,
                1);

            Assert.True(
                metrics.MeanAbsoluteError > 0);

            Assert.True(
                metrics.RootMeanSquareError > 0);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static ImageComparisonService CreateService()
        => new(
            new SafeFileIdentityInspector(),
            new ImageTechnicalInspector(),
            new PerceptualHashService(),
            new MetadataInspector(),
            new ImageHeuristicsService(),
            new ImagePairPixelAnalyzer());

    private static async Task CreatePatternAsync(
        string path,
        int width,
        int height)
    {
        using var bitmap =
            new SKBitmap(
                width,
                height,
                SKColorType.Rgba8888,
                SKAlphaType.Opaque);

        for (var y = 0;
             y < height;
             y++)
        {
            for (var x = 0;
                 x < width;
                 x++)
            {
                var checker =
                    ((x / 12) +
                     (y / 12)) %
                    2 ==
                    0;

                bitmap.SetPixel(
                    x,
                    y,
                    checker
                        ? new SKColor(
                            (byte)(40 + x % 160),
                            (byte)(50 + y % 150),
                            180)
                        : new SKColor(
                            210,
                            (byte)(30 + x % 120),
                            (byte)(20 + y % 140)));
            }
        }

        await SavePngAsync(
            bitmap,
            path);
    }

    private static async Task CreateSolidAsync(
        string path,
        int width,
        int height,
        SKColor color)
    {
        using var bitmap =
            new SKBitmap(
                width,
                height,
                SKColorType.Rgba8888,
                SKAlphaType.Opaque);

        bitmap.Erase(
            color);

        await SavePngAsync(
            bitmap,
            path);
    }

    private static async Task SavePngAsync(
        SKBitmap bitmap,
        string path)
    {
        using var image =
            SKImage.FromBitmap(
                bitmap);

        using var data =
            image.Encode(
                SKEncodedImageFormat.Png,
                100)
            ?? throw new InvalidOperationException();

        await using var stream =
            File.Create(
                path);

        data.SaveTo(
            stream);

        await stream.FlushAsync();
    }

    private static string CreateRoot()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "image-forensics-compare-tests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            root);

        return root;
    }

    private static void DeleteRoot(
        string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(
                    root,
                    true);
        }
        catch
        {
        }
    }
}
