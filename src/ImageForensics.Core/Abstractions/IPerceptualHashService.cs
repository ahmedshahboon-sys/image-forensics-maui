using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IPerceptualHashService
{
    Task<PerceptualHashResult> ComputeAsync(string filePath, CancellationToken cancellationToken = default);
}
