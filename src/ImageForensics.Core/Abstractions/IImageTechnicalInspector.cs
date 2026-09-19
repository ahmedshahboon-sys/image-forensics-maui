using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IImageTechnicalInspector
{
    Task<ImageTechnicalInfo> InspectAsync(string filePath, CancellationToken cancellationToken = default);
}
