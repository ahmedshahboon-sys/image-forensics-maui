using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IVisibleTextEntityExtractor
{
    IReadOnlyList<VisibleTextEntity> Extract(
        string? ocrText,
        IReadOnlyList<BarcodeHit>? barcodes = null);
}
