namespace ImageForensics.App;

public sealed record SharedImageItem(string Path, string DisplayName, string ContentType);

public static class SharedImageInbox
{
    private static readonly object Gate = new();
    private static SharedImageItem? _pending;

    public static void Set(SharedImageItem item)
    {
        lock (Gate)
        {
            if (_pending is { } previous && !string.Equals(previous.Path, item.Path, StringComparison.Ordinal))
            {
                try { File.Delete(previous.Path); } catch { }
            }
            _pending = item;
        }
    }

    public static SharedImageItem? Take()
    {
        lock (Gate)
        {
            var item = _pending;
            _pending = null;
            return item;
        }
    }
}
