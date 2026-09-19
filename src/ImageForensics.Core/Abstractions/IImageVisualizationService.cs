namespace ImageForensics.Core.Abstractions;

public interface IImageVisualizationService
{
    Task CreateElaPreviewAsync(string inputPath, string outputPngPath, CancellationToken cancellationToken = default);
    Task CreateBitPlaneAsync(string inputPath, int bitPlane, string outputPngPath, CancellationToken cancellationToken = default);
    Task CreateRgbChannelAsync(string inputPath, string channel, string outputPngPath, CancellationToken cancellationToken = default);
    Task CreateDifferenceMapAsync(string leftPath, string rightPath, string outputPngPath, CancellationToken cancellationToken = default);
}
