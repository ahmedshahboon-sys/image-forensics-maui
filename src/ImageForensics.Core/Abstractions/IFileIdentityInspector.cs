using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IFileIdentityInspector
{
    Task<FileIdentityResult> InspectAsync(
        string filePath,
        string? declaredMime = null,
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
