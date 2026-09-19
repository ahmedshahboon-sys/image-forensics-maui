using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IPixelForensicsAnalyzer
{
    Task<PixelForensicsResult> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default);
}
