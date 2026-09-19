using System.Text.RegularExpressions;
using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;

namespace ImageForensics.Forensics.VisibleContent;

public sealed partial class VisibleTextEntityExtractor : IVisibleTextEntityExtractor
{
    public IReadOnlyList<VisibleTextEntity> Extract(
        string? ocrText,
        IReadOnlyList<BarcodeHit>? barcodes = null)
    {
        var results = new List<VisibleTextEntity>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(ocrText))
            ExtractFromText(ocrText, "OCR", results, seen);

        if (barcodes is not null)
        {
            foreach (var barcode in barcodes)
            {
                if (string.IsNullOrWhiteSpace(barcode.Text))
                    continue;

                var source = $"Barcode:{barcode.Format}";
                if (barcode.Format.Contains("QR", StringComparison.OrdinalIgnoreCase) &&
                    TryNormalizeHttpUrl(barcode.Text, out var qrUrl))
                {
                    Add("QR URL", qrUrl, source, results, seen);
                }

                ExtractFromText(barcode.Text, source, results, seen);
            }
        }

        return results;
    }

    private static void ExtractFromText(
        string text,
        string source,
        List<VisibleTextEntity> results,
        HashSet<string> seen)
    {
        foreach (Match match in UrlRegex().Matches(text))
        {
            var value = TrimTrailingPunctuation(match.Value);
            if (TryNormalizeHttpUrl(value, out var normalized))
                Add("URL", normalized, source, results, seen);
        }

        foreach (Match match in EmailRegex().Matches(text))
            Add("Email", match.Value, source, results, seen);

        foreach (Match match in PhoneRegex().Matches(text))
        {
            var raw = match.Value.Trim();
            var digitCount = raw.Count(char.IsDigit);
            if (digitCount is >= 7 and <= 18)
                Add("Phone", raw, source, results, seen);
        }
    }

    private static bool TryNormalizeHttpUrl(string raw, out string normalized)
    {
        normalized = raw.Trim();
        if (normalized.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            normalized = "https://" + normalized;

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme is not ("http" or "https"))
            return false;

        normalized = uri.ToString();
        return true;
    }

    private static string TrimTrailingPunctuation(string value)
        => value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '}', '،', '؛');

    private static void Add(
        string kind,
        string value,
        string source,
        List<VisibleTextEntity> results,
        HashSet<string> seen)
    {
        var key = $"{kind}\n{value}\n{source}";
        if (seen.Add(key))
            results.Add(new VisibleTextEntity(kind, value, source));
    }

    [GeneratedRegex(@"(?i)\b(?:https?://|www\.)[^\s<>""']+", RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])\+?\d[\d\s().-]{5,}\d(?![\p{L}\p{N}])", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneRegex();
}
