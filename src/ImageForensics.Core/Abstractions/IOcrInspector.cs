using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IOcrInspector
{
    Task<OcrInspectionResult> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
