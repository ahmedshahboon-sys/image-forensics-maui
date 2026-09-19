using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IImageDiagnosticGenerator
{
    Task<DiagnosticImageResult> GenerateElaAsync(
        string sourcePath,
        string destinationDirectory,
        int jpegQuality = 90,
        int amplification = 12,
        CancellationToken cancellationToken = default);

    Task<DiagnosticImageResult> GenerateChannelAsync(
        string sourcePath,
        string destinationDirectory,
        char channel,
        CancellationToken cancellationToken = default);

    Task<DiagnosticImageResult> GenerateBitPlaneAsync(
        string sourcePath,
        string destinationDirectory,
        char channel,
        int bit,
        CancellationToken cancellationToken = default);

    Task<DiagnosticImageResult> GenerateDifferenceMapAsync(
        string leftPath,
        string rightPath,
        string destinationDirectory,
        CancellationToken cancellationToken = default);
}
