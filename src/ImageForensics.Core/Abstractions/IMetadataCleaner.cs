using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IMetadataCleaner
{
    Task<MetadataCleanResult> CreateCleanCopyAsync(
        string sourcePath,
        string destinationDirectory,
        CancellationToken cancellationToken = default);
}
