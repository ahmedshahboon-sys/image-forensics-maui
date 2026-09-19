using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IContainerInspector
{
    Task<ContainerInspectionResult> InspectAsync(string filePath, CancellationToken cancellationToken = default);
}
