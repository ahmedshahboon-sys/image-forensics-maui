using ImageForensics.Core.Models;
using Xunit;

namespace ImageForensics.Tests;

public sealed class PixelForensicsMathTests
{
    [Fact]
    public void PerceptualSimilarityIsBounded()
    {
        var value = PerceptualHashResult.Similarity64("0f0f0f0f0f0f0f0f", "00ff00ff00ff00ff");
        Assert.InRange(value, 0, 1);
    }
}
