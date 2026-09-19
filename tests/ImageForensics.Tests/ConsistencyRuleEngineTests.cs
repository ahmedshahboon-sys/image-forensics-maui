using ImageForensics.Core.Models;
using ImageForensics.Forensics.Rules;
using Xunit;

namespace ImageForensics.Tests;

public sealed class ConsistencyRuleEngineTests
{
    [Fact]
    public void ExtensionMismatchIsConfirmedTechnicalFactButNotForgeryVerdict()
    {
        var context = new ForensicAnalysisContext(
            new FileIdentityResult
            {
                FileName = "x.jpg", SizeBytes = 10, Extension = ".jpg", DeclaredMime = "image/jpeg",
                DetectedType = "PNG", DetectedMime = "image/png", ExtensionMismatch = true,
                EntropyBitsPerByte = 1, Sha256 = "a", Sha1 = "b", Md5 = "c", SignatureHex = "89504e47"
            },
            new ImageTechnicalInfo
            {
                Width = 1, Height = 1, AspectRatio = 1, EncodedFormat = "Png",
                ColorType = "Rgba8888", AlphaType = "Opaque", HasAlpha = false,
                BitsPerPixel = 32, FrameCount = 1, Orientation = "TopLeft"
            },
            new MetadataInspectionResult(Array.Empty<MetadataField>(), null, Array.Empty<string>()),
            new ContainerInspectionResult("PNG", Array.Empty<ContainerSegment>(), 0, Array.Empty<string>()));

        var finding = Assert.Single(new ConsistencyRuleEngine().Analyze(context));
        Assert.Equal(ForensicConfidence.Confirmed, finding.Confidence);
        Assert.Contains("لا يثبت", finding.Limitation);
    }
}
