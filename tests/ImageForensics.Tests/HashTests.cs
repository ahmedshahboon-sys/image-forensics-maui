using ImageForensics.Core.Models;
using ImageForensics.Forensics.Hashing;
using Xunit;

namespace ImageForensics.Tests;

public sealed class HashTests
{
    [Fact]
    public void Crc32MatchesKnownVector()
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes("123456789");
        Assert.Equal(0xCBF43926u, Crc32.Compute(bytes));
    }

    [Fact]
    public void PerceptualSimilarityUsesHammingDistance()
    {
        Assert.Equal(1.0, PerceptualHashResult.Similarity64("0000000000000000", "0000000000000000"));
        Assert.Equal(0.0, PerceptualHashResult.Similarity64("0000000000000000", "ffffffffffffffff"));
    }
}
