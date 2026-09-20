using System.Diagnostics;
using ImageForensics.Forensics.Containers;
using ImageForensics.Forensics.FileIdentity;
using ImageForensics.Forensics.HiddenData;
using ImageForensics.Imaging;
using ImageForensics.Metadata;
using Xunit;

namespace ImageForensics.Tests;

public sealed class PerformanceBudgetTests
{
    [Fact]
    public async Task FastScanPrimitivesCompleteWithinThreeSecondsForOrdinaryImage()
    {
        var root =
            TestCorpusFactory.CreateRoot();

        try
        {
            var path =
                await TestCorpusFactory.CreateJpegAsync(
                    root,
                    "ordinary-2mp.jpg",
                    1600,
                    1200,
                    90);

            var stopwatch =
                Stopwatch.StartNew();

            var identity =
                await new SafeFileIdentityInspector()
                    .InspectAsync(path);

            var technical =
                await new ImageTechnicalInspector()
                    .InspectAsync(path);

            stopwatch.Stop();

            Assert.Equal(
                "JPEG",
                identity.DetectedType);

            Assert.Equal(
                1600,
                technical.Width);

            Assert.True(
                stopwatch.Elapsed <
                TimeSpan.FromSeconds(3),
                $"Fast primitives took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
        }
        finally
        {
            TestCorpusFactory.DeleteRoot(root);
        }
    }

    [Fact]
    public async Task PortableDeepPrimitivesStayWithinGenerousCiBudget()
    {
        var root =
            TestCorpusFactory.CreateRoot();

        try
        {
            var path =
                await TestCorpusFactory.CreateJpegAsync(
                    root,
                    "deep.jpg",
                    1024,
                    768,
                    88);

            var stopwatch =
                Stopwatch.StartNew();

            _ = await new MetadataInspector()
                .InspectAsync(path);

            _ = await new SafeContainerInspector()
                .InspectAsync(path);

            _ = await new PerceptualHashService()
                .ComputeAsync(path);

            _ = await new BarcodeInspector()
                .InspectAsync(path);

            _ = await new HiddenDataInspector()
                .InspectAsync(path);

            _ = await new SteganographyAnalyzer()
                .AnalyzeAsync(path);

            _ = await new ImageHeuristicsService()
                .AnalyzeAsync(path);

            stopwatch.Stop();

            Assert.True(
                stopwatch.Elapsed <
                TimeSpan.FromSeconds(15),
                $"Portable deep primitives took {stopwatch.Elapsed.TotalSeconds:F2} s.");
        }
        finally
        {
            TestCorpusFactory.DeleteRoot(root);
        }
    }

    [Fact]
    public async Task PreviewBasedAnalysesHonorPreCancelledToken()
    {
        var root =
            TestCorpusFactory.CreateRoot();

        try
        {
            var path =
                await TestCorpusFactory.CreateJpegAsync(
                    root,
                    "cancel.jpg",
                    800,
                    600);

            using var cts =
                new CancellationTokenSource();

            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () =>
                    new PerceptualHashService()
                        .ComputeAsync(
                            path,
                            cts.Token));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () =>
                    new ImageHeuristicsService()
                        .AnalyzeAsync(
                            path,
                            cts.Token));
        }
        finally
        {
            TestCorpusFactory.DeleteRoot(root);
        }
    }
}
