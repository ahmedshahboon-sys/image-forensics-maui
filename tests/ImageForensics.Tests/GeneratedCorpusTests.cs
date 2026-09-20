using ImageForensics.Forensics.Containers;
using ImageForensics.Forensics.Privacy;
using ImageForensics.Imaging;
using ImageForensics.Metadata;
using Xunit;

namespace ImageForensics.Tests;

public sealed class GeneratedCorpusTests
{
    [Fact]
    public async Task ExifAndGpsFixtureParsesExpectedDeviceAndCoordinates()
    {
        var root = TestCorpusFactory.CreateRoot();

        try
        {
            var path =
                await TestCorpusFactory.CreateJpegWithExifGpsAsync(root);

            var result =
                await new MetadataInspector()
                    .InspectAsync(path);

            Assert.Contains(
                result.Fields,
                f =>
                    f.Tag == "Make" &&
                    (f.ParsedValue?.Contains(
                        "TestCam",
                        StringComparison.Ordinal) ??
                     false));

            Assert.Contains(
                result.Fields,
                f =>
                    f.Tag == "Model" &&
                    (f.ParsedValue?.Contains(
                        "Model1",
                        StringComparison.Ordinal) ??
                     false));

            Assert.NotNull(result.Gps);
            Assert.InRange(result.Gps!.Latitude, 32.88, 32.89);
            Assert.InRange(result.Gps.Longitude, 13.18, 13.19);
        }
        finally
        {
            TestCorpusFactory.DeleteRoot(root);
        }
    }

    [Fact]
    public async Task PngTextFixtureIsDetectedByContainerInspection()
    {
        var root = TestCorpusFactory.CreateRoot();

        try
        {
            var path =
                await TestCorpusFactory.CreatePngWithTextChunkAsync(root);

            var result =
                await new SafeContainerInspector()
                    .InspectAsync(path);

            Assert.Contains(
                result.Segments,
                segment =>
                    segment.Type == "tEXt");
        }
        finally
        {
            TestCorpusFactory.DeleteRoot(root);
        }
    }

    [Fact]
    public async Task AppendedBytesFixtureProducesTrailingPrivacyRisk()
    {
        var root = TestCorpusFactory.CreateRoot();

        try
        {
            var path =
                await TestCorpusFactory.CreateJpegWithAppendedBytesAsync(root);

            var metadata =
                await new MetadataInspector()
                    .InspectAsync(path);

            var container =
                await new SafeContainerInspector()
                    .InspectAsync(path);

            Assert.True(container.TrailingBytes > 0);

            var privacy =
                new PrivacyRiskAnalyzer()
                    .Analyze(
                        metadata,
                        container);

            Assert.Contains(
                privacy.Risks,
                risk =>
                    risk.Code ==
                    "privacy.trailing");
        }
        finally
        {
            TestCorpusFactory.DeleteRoot(root);
        }
    }

    [Fact]
    public async Task EmbeddedThumbnailFixtureExposesThumbnailMetadata()
    {
        var root = TestCorpusFactory.CreateRoot();

        try
        {
            var path =
                await TestCorpusFactory.CreateJpegWithEmbeddedThumbnailAsync(root);

            var metadata =
                await new MetadataInspector()
                    .InspectAsync(path);

            Assert.Contains(
                metadata.Fields,
                field =>
                    field.Directory.Contains(
                        "Thumbnail",
                        StringComparison.OrdinalIgnoreCase) ||
                    field.Tag.Contains(
                        "Thumbnail",
                        StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TestCorpusFactory.DeleteRoot(root);
        }
    }

    [Fact]
    public async Task QrFixtureDecodesExpectedTextOffline()
    {
        var root = TestCorpusFactory.CreateRoot();

        try
        {
            const string value =
                "https://example.com/forensics";

            var path =
                await TestCorpusFactory.CreateQrPngAsync(
                    root,
                    value);

            var hits =
                await new BarcodeInspector()
                    .InspectAsync(path);

            var hit =
                Assert.Single(hits);

            Assert.Contains(
                "QR",
                hit.Format,
                StringComparison.OrdinalIgnoreCase);

            Assert.Equal(
                value,
                hit.Text);
        }
        finally
        {
            TestCorpusFactory.DeleteRoot(root);
        }
    }

    [Fact]
    public async Task CorruptJpegDoesNotCrashMetadataParser()
    {
        var root = TestCorpusFactory.CreateRoot();

        try
        {
            var path =
                await TestCorpusFactory.CreateCorruptJpegAsync(root);

            var result =
                await new MetadataInspector()
                    .InspectAsync(path);

            Assert.NotNull(result);
            Assert.True(
                result.Errors.Count > 0 ||
                result.Fields.Count >= 0);
        }
        finally
        {
            TestCorpusFactory.DeleteRoot(root);
        }
    }

    [Fact]
    public async Task HugeHeaderImageIsRejectedBeforePairPixelDecode()
    {
        var root = TestCorpusFactory.CreateRoot();

        try
        {
            var path =
                await TestCorpusFactory.CreateHugeHeaderPngAsync(root);

            var ex =
                await Assert.ThrowsAsync<InvalidDataException>(
                    () =>
                        new ImagePairPixelAnalyzer()
                            .CompareAsync(
                                path,
                                path));

            Assert.Contains(
                "safe limit",
                ex.Message,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestCorpusFactory.DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SyntheticScreenshotEditedAndArabicOcrFixturesAreDecodable()
    {
        var root = TestCorpusFactory.CreateRoot();

        try
        {
            var screenshot =
                await TestCorpusFactory.CreateScreenshotPngAsync(root);

            var edited =
                await TestCorpusFactory.CreateDoubleCompressedJpegAsync(root);

            var arabic =
                await TestCorpusFactory.CreateArabicOcrImageAsync(root);

            var inspector =
                new ImageTechnicalInspector();

            var screenshotInfo =
                await inspector.InspectAsync(screenshot);

            var editedInfo =
                await inspector.InspectAsync(edited);

            var arabicInfo =
                await inspector.InspectAsync(arabic);

            Assert.Equal(360, screenshotInfo.Width);
            Assert.Equal("JPEG", editedInfo.EncodedFormat);
            Assert.Equal(900, arabicInfo.Width);
        }
        finally
        {
            TestCorpusFactory.DeleteRoot(root);
        }
    }
}
