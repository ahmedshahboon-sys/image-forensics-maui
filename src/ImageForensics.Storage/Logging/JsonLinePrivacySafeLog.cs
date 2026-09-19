using System.Text.Json;
using ImageForensics.Core.Abstractions;

namespace ImageForensics.Storage.Logging;

public sealed class JsonLinePrivacySafeLog : IPrivacySafeLog
{
    private readonly string _path;
    private readonly object _gate = new();

    public JsonLinePrivacySafeLog(string path) => _path = path;

    public void Info(string eventName, IReadOnlyDictionary<string, object?>? data = null)
        => Write("info", eventName, null, data);

    public void Error(string eventName, Exception exception, IReadOnlyDictionary<string, object?>? data = null)
        => Write("error", eventName, exception.GetType().Name, data);

    private void Write(string level, string eventName, string? errorType, IReadOnlyDictionary<string, object?>? data)
    {
        var json = JsonSerializer.Serialize(new { ts = DateTimeOffset.UtcNow, level, eventName, errorType, data });
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
            File.AppendAllText(_path, json + Environment.NewLine);
        }
    }
}
