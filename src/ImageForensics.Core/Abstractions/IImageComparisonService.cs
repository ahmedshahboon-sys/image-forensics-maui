using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IImageComparisonService
{
    Task<ImageComparisonResult> CompareAsync(
        string leftPath,
        string rightPath,
        CancellationToken cancellationToken = default);
}
