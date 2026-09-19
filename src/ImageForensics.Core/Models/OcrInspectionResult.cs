namespace ImageForensics.Core.Models;

public sealed record OcrInspectionResult(
    string Text,
    float Confidence,
    string Languages,
    bool Succeeded,
    string? Error);
