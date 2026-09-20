namespace ImageForensics.Core.Models;

public static class OnlineFeaturePolicy
{
    public const string ReverseSearchDisclosure =
        "سيتم فتح خدمة خارجية في المتصفح فقط. التطبيق لن يرفع الصورة. إذا أردت البحث، اختر الصورة بنفسك داخل الموقع الخارجي.";

    public const string HashReputationDisclosure =
        "سيتم فتح خدمة خارجية في المتصفح وتمرير SHA-256 فقط داخل رابط البحث. لن يرفع التطبيق ملف الصورة.";

    public static Uri ReverseSearchHome()
        => new("https://tineye.com/");

    public static Uri HashReputationSearch(string sha256)
    {
        if (string.IsNullOrWhiteSpace(sha256) ||
            sha256.Length != 64 ||
            sha256.Any(ch => !Uri.IsHexDigit(ch)))
        {
            throw new ArgumentException(
                "SHA-256 must be exactly 64 hexadecimal characters.",
                nameof(sha256));
        }

        return new Uri(
            "https://www.virustotal.com/gui/search/" +
            sha256.ToLowerInvariant());
    }
}
