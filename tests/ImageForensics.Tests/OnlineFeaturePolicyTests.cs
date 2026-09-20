using ImageForensics.Core.Models;
using Xunit;

namespace ImageForensics.Tests;

public sealed class OnlineFeaturePolicyTests
{
    [Fact]
    public void HashLookupAcceptsOnlySha256AndTransfersNoFilePath()
    {
        var hash =
            new string('a', 64);

        var uri =
            OnlineFeaturePolicy.HashReputationSearch(hash);

        Assert.Contains(hash, uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotContain("file:", uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<ArgumentException>(
            () => OnlineFeaturePolicy.HashReputationSearch("/tmp/photo.jpg"));
    }

    [Fact]
    public void ReverseSearchStartsAtExternalHomeWithoutImagePayload()
    {
        var uri =
            OnlineFeaturePolicy.ReverseSearchHome();

        Assert.Equal("https", uri.Scheme);
        Assert.DoesNotContain("photo", uri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "لن يرفع الصورة",
            OnlineFeaturePolicy.ReverseSearchDisclosure,
            StringComparison.Ordinal);
    }
}
