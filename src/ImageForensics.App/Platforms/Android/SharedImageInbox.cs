using Android.Content;
using Android.Net;

namespace ImageForensics.App;

public static class SharedImageInbox
{
    private static readonly object Gate=new();
    private static Uri? _pending;

    public static void Capture(Intent? intent)
    {
        if(intent?.Action!=Intent.ActionSend) return;
#pragma warning disable CS0618
        var uri=intent.GetParcelableExtra(Intent.ExtraStream) as Uri;
#pragma warning restore CS0618
        if(uri is null)return;
        lock(Gate)_pending=uri;
    }

    public static Uri? Take()
    {
        lock(Gate){var value=_pending;_pending=null;return value;}
    }
}
