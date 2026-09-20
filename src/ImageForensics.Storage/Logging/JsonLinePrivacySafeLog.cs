using System.Text.Json;
using ImageForensics.Core.Abstractions;

namespace ImageForensics.Storage.Logging;

public sealed class JsonLinePrivacySafeLog : IPrivacySafeLog
{
    private static readonly string[] BlockedKeyFragments =
    {
        "path",
        "filename",
        "gps",
        "latitude",
        "longitude",
        "ocr",
        "text",
        "image",
        "bytes",
        "content",
        "payload"
    };

    private readonly string _path;
    private readonly object _gate = new();

    public JsonLinePrivacySafeLog(string path)
        => _path = Path.GetFullPath(path);

    public void Info(
        string eventName,
        IReadOnlyDictionary<string, object?>? data = null)
        => Write(
            "info",
            eventName,
            null,
            data);

    public void Error(
        string eventName,
        Exception exception,
        IReadOnlyDictionary<string, object?>? data = null)
        => Write(
            "error",
            eventName,
            exception.GetType().Name,
            data);

    private void Write(
        string level,
        string eventName,
        string? errorType,
        IReadOnlyDictionary<string, object?>? data)
    {
        var safeData =
            SanitizeData(data);

        var json =
            JsonSerializer.Serialize(
                new
                {
                    ts = DateTimeOffset.UtcNow,
                    level,
                    eventName = SanitizeEventName(eventName),
                    errorType,
                    data = safeData
                });

        lock (_gate)
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(_path) ??
                ".");

            File.AppendAllText(
                _path,
                json +
                Environment.NewLine);
        }
    }

    private static IReadOnlyDictionary<string, object?>? SanitizeData(
        IReadOnlyDictionary<string, object?>? data)
    {
        if (data is null ||
            data.Count == 0)
            return null;

        var safe =
            new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var item in data.Take(32))
        {
            var key =
                item.Key
                    .Trim();

            if (key.Length == 0 ||
                BlockedKeyFragments.Any(
                    fragment =>
                        key.Contains(
                            fragment,
                            StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            safe[key[..Math.Min(
                key.Length,
                64)]] =
                ToSafeScalar(
                    item.Value);
        }

        return safe;
    }

    private static object? ToSafeScalar(
        object? value)
        => value switch
        {
            null => null,
            bool b => b,
            byte b => b,
            sbyte b => b,
            short n => n,
            ushort n => n,
            int n => n,
            uint n => n,
            long n => n,
            ulong n => n,
            float n => n,
            double n => n,
            decimal n => n,
            DateTimeOffset time => time,
            DateTime time => time,
            Enum e => e.ToString(),
            string s => s.Length <= 128
                ? s
                : s[..128],
            _ => value.GetType().Name
        };

    private static string SanitizeEventName(
        string eventName)
    {
        if (string.IsNullOrWhiteSpace(
                eventName))
            return "event";

        var chars =
            eventName
                .Where(ch =>
                    char.IsLetterOrDigit(ch) ||
                    ch is '.' or '_' or '-')
                .Take(80)
                .ToArray();

        return chars.Length == 0
            ? "event"
            : new string(chars);
    }
}
