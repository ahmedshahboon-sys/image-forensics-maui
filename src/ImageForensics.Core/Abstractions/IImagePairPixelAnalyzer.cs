using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IImagePairPixelAnalyzer
{
    Task<PixelComparisonMetrics> CompareAsync(
        string leftPath,
        string rightPath,
        CancellationToken cancellationToken = default);
}
