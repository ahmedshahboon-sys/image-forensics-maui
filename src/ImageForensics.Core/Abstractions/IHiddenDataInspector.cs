using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IHiddenDataInspector
{
    Task<IReadOnlyList<HiddenDataFinding>> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
