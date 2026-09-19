using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;

namespace ImageForensics.App;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
[IntentFilter(
    new[] { Intent.ActionSend },
    Categories = new[] { Intent.CategoryDefault },
    DataMimeType = "image/*")]
public sealed class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        CaptureSharedImage(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        CaptureSharedImage(intent);
    }

    private void CaptureSharedImage(Intent? intent)
    {
        if (intent?.Action != Intent.ActionSend) return;

        var uri = Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
            ? intent.GetParcelableExtra(Intent.ExtraStream, Java.Lang.Class.FromType(typeof(Android.Net.Uri))) as Android.Net.Uri
            : intent.GetParcelableExtra(Intent.ExtraStream) as Android.Net.Uri;

        if (uri is null) return;

        try
        {
            var contentType = ContentResolver?.GetType(uri) ?? "image/*";
            var displayName = QueryDisplayName(uri) ?? $"shared-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.img";

            var folder = Path.Combine(CacheDir?.AbsolutePath ?? FileSystem.CacheDirectory, "shared-inbox");
            Directory.CreateDirectory(folder);
            var ext = Path.GetExtension(displayName);
            if (string.IsNullOrWhiteSpace(ext) || ext.Length > 12) ext = ".img";
            var targetPath = Path.Combine(folder, Guid.NewGuid().ToString("N") + ext);

            using var source = ContentResolver?.OpenInputStream(uri);
            if (source is null) return;
            using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

            var buffer = new byte[128 * 1024];
            long total = 0;
            const long max = 512L * 1024 * 1024;
            while (true)
            {
                var read = source.Read(buffer, 0, buffer.Length);
                if (read <= 0) break;
                total += read;
                if (total > max)
                {
                    target.Dispose();
                    File.Delete(targetPath);
                    return;
                }
                target.Write(buffer, 0, read);
            }

            SharedImageInbox.Set(new SharedImageItem(targetPath, displayName, contentType));
        }
        catch
        {
        }
    }

    private string? QueryDisplayName(Android.Net.Uri uri)
    {
        try
        {
            using var cursor = ContentResolver?.Query(uri, new[] { OpenableColumns.DisplayName }, null, null, null);
            if (cursor is null || !cursor.MoveToFirst()) return null;
            var index = cursor.GetColumnIndex(OpenableColumns.DisplayName);
            return index >= 0 ? cursor.GetString(index) : null;
        }
        catch
        {
            return null;
        }
    }
}
