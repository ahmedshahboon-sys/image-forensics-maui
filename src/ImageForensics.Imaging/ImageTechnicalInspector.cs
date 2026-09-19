using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using SkiaSharp;

namespace ImageForensics.Imaging;

public sealed class ImageTechnicalInspector : IImageTechnicalInspector
{
    public Task<ImageTechnicalInfo> InspectAsync(string filePath, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var codec = SKCodec.Create(filePath) ?? throw new InvalidDataException("Unsupported or corrupt image.");
            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0)
                throw new InvalidDataException("Image dimensions are invalid.");

            return new ImageTechnicalInfo
            {
                Width = info.Width,
                Height = info.Height,
                AspectRatio = (double)info.Width / info.Height,
                EncodedFormat = codec.EncodedFormat.ToString(),
                ColorType = info.ColorType.ToString(),
                AlphaType = info.AlphaType.ToString(),
                HasAlpha = info.AlphaType != SKAlphaType.Opaque,
                BitsPerPixel = info.BytesPerPixel * 8,
                FrameCount = Math.Max(1, codec.FrameCount),
                Orientation = codec.EncodedOrigin.ToString()
            };
        }, cancellationToken);
}
