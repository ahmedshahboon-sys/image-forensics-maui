using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IBarcodeInspector
{
    Task<IReadOnlyList<BarcodeHit>> InspectAsync(string filePath, CancellationToken cancellationToken = default);
}
