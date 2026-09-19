namespace ImageForensics.Core.Abstractions;

public interface IPrivacySafeLog
{
    void Info(string eventName, IReadOnlyDictionary<string, object?>? data = null);
    void Error(string eventName, Exception exception, IReadOnlyDictionary<string, object?>? data = null);
}
