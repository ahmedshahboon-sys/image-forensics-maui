using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IMetadataInspector
{
    Task<MetadataInspectionResult> InspectAsync(string filePath, CancellationToken cancellationToken = default);
}
