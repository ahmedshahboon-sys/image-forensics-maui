using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface ISteganographyAnalyzer
{
    Task<SteganographyResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
