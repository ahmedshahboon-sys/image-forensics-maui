using Android.Content;
using AndroidUri = Android.Net.Uri;

namespace ImageForensics.App;

public static class SharedImageInbox
{
    private static readonly object Gate = new();
    private static AndroidUri? _pending;

    public static void Capture(Intent? intent)
    {
        if (intent?.Action != Intent.ActionSend) return;

#pragma warning disable CS0618
        var uri = intent.GetParcelableExtra(Intent.ExtraStream) as AndroidUri;
#pragma warning restore CS0618

        if (uri is null) return;
        lock (Gate) _pending = uri;
    }

    public static AndroidUri? Take()
    {
        lock (Gate)
        {
            var value = _pending;
            _pending = null;
            return value;
        }
    }
}
