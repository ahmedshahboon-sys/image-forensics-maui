using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IImageHeuristicsService
{
    Task<ImageHeuristicsResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
