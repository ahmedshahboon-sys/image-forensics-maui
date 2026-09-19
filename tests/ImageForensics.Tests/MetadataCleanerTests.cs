using System.Security.Cryptography;
using ImageForensics.Forensics.Containers;
using ImageForensics.Forensics.Privacy;
using ImageForensics.Imaging;
using ImageForensics.Metadata;
using SkiaSharp;
using Xunit;

namespace ImageForensics.Tests;

public sealed class MetadataCleanerTests
{
    [Fact]
    public async Task CleanCopyRemovesTrailingDataAndLeavesSourceUnchanged()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "image-forensics-cleaner-tests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        var source =
            Path.Combine(
                root,
                "source.jpg");

        var outputDir =
            Path.Combine(
                root,
                "cleaned");

        try
        {
            await CreateJpegAsync(
                source,
                8,
                6);

            await File.AppendAllBytesAsync(
                source,
                new byte[]
                {
                    0x50, 0x4B, 0x03, 0x04,
                    1, 2, 3, 4, 5
                });

            var sourceBytesBefore =
                await File.ReadAllBytesAsync(
                    source);

            var cleaner =
                new MetadataCleaner(
                    new MetadataInspector(),
                    new SafeContainerInspector(),
                    new PrivacyRiskAnalyzer());

            var result =
                await cleaner.CreateCleanCopyAsync(
                    source,
                    outputDir);

            var sourceBytesAfter =
                await File.ReadAllBytesAsync(
                    source);

            Assert.True(result.OriginalUntouched);
            Assert.Equal(
                sourceBytesBefore,
                sourceBytesAfter);

            Assert.True(
                result.TrailingBytesBefore > 0);

            Assert.Equal(
                0,
                result.TrailingBytesAfter);

            Assert.Contains(
                "privacy.trailing",
                result.PrivacyRiskCodesBefore);

            Assert.DoesNotContain(
                "privacy.trailing",
                result.PrivacyRiskCodesAfter);

            Assert.True(
                result.VerificationPassed,
                string.Join(
                    " | ",
                    result.VerificationNotes));

            Assert.True(
                File.Exists(
                    result.OutputPath));

            Assert.NotEqual(
                result.OriginalSha256,
                result.CleanSha256);
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task CleanCopyHashMatchesWrittenFile()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "image-forensics-cleaner-tests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        var source =
            Path.Combine(
                root,
                "source.jpg");

        try
        {
            await CreateJpegAsync(
                source,
                5,
                4);

            var cleaner =
                new MetadataCleaner(
                    new MetadataInspector(),
                    new SafeContainerInspector(),
                    new PrivacyRiskAnalyzer());

            var result =
                await cleaner.CreateCleanCopyAsync(
                    source,
                    Path.Combine(root, "out"));

            var bytes =
                await File.ReadAllBytesAsync(
                    result.OutputPath);

            var actual =
                Convert
                    .ToHexString(
                        SHA256.HashData(bytes))
                    .ToLowerInvariant();

            Assert.Equal(
                actual,
                result.CleanSha256);

            Assert.Equal(
                "TopLeft",
                result.CleanOrientation);
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch
            {
            }
        }
    }

    private static async Task CreateJpegAsync(
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
                bitmap.SetPixel(
                    x,
                    y,
                    new SKColor(
                        (byte)(20 + x * 10),
                        (byte)(30 + y * 15),
                        (byte)(40 + x + y)));
            }
        }

        using var image =
            SKImage.FromBitmap(bitmap);

        using var data =
            image.Encode(
                SKEncodedImageFormat.Jpeg,
                90)
            ?? throw new InvalidOperationException(
                "Test JPEG encode failed.");

        await using var stream =
            File.Create(path);

        data.SaveTo(stream);

        await stream.FlushAsync();
    }
}
